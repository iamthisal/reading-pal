using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MySqlConnector;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace LendingService.SeleniumTests;

/// <summary>
/// Shared browser setup and helpers for the lending workflow (reserve → accept/reject → return,
/// plus member-side cancel and admin history). The tests drive the real frontend against a fully
/// running stack (frontend + user/inventory/lending services + database); nothing is mocked.
///
/// Configuration (environment variables):
///   SELENIUM_FRONTEND_URL       default http://localhost:5173 (the local docker-compose stack)
///   SELENIUM_ADMIN_EMAIL / _PASSWORD   default: the local development admin
///   SELENIUM_USER_EMAIL / _PASSWORD    required: an approved "User"-role account on that environment
///
/// Every test creates its own uniquely named book, so runs leave data behind: point the suite at a
/// local or test environment, not production. The API/DB helpers below only work against the local
/// stack.
/// </summary>
public abstract class LendingSeleniumTestBase : IDisposable
{
    protected static readonly string FrontendUrl =
        Environment.GetEnvironmentVariable("SELENIUM_FRONTEND_URL")?.TrimEnd('/') ?? "http://localhost:5173";

    protected static readonly string AdminEmail =
        Environment.GetEnvironmentVariable("SELENIUM_ADMIN_EMAIL") ?? "admin@library.com";
    protected static readonly string AdminPassword =
        Environment.GetEnvironmentVariable("SELENIUM_ADMIN_PASSWORD") ?? "adminpassword";
    protected static string UserEmail => Required("SELENIUM_USER_EMAIL");
    protected static string UserPassword => Required("SELENIUM_USER_PASSWORD");

    protected static readonly string UserServiceUrl =
        Environment.GetEnvironmentVariable("SELENIUM_USER_SERVICE_URL") ?? "http://localhost:5000";
    protected static readonly string InventoryServiceUrl =
        Environment.GetEnvironmentVariable("SELENIUM_INVENTORY_SERVICE_URL") ?? "http://localhost:5001";
    protected static readonly string LendingDbConnectionString =
        Environment.GetEnvironmentVariable("SELENIUM_LENDING_DB_CONNECTION_STRING")
        ?? "Server=localhost;Port=3307;Database=ReadingPal_lending;Uid=readingpal_user;Pwd=readingpal_password;";

    protected readonly IWebDriver Driver;
    protected readonly WebDriverWait Wait;

    protected LendingSeleniumTestBase()
    {
        Driver = new ChromeDriver(WebDriverHelper.CreateOptions());
        Wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(25));
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Set {name}: an approved member account on the environment under test.");

    protected static By RowFor(string title) => By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]");

    protected void LoginAs(string email, string password)
    {
        Driver.Navigate().GoToUrl($"{FrontendUrl}/login");
        SetReactInputValue(Wait.Until(d => d.FindElement(By.Id("email"))), email);
        SetReactInputValue(Driver.FindElement(By.Id("password")), password);
        Driver.FindElement(By.CssSelector("button[type='submit']")).Click();
    }

    protected void LoginAsAdmin()
    {
        LoginAs(AdminEmail, AdminPassword);
        Wait.Until(d => d.Url.Contains("/admin/dashboard"));
    }

    protected void LoginAsUser()
    {
        LoginAs(UserEmail, UserPassword);
        Wait.Until(d => d.Url.Contains("/home"));
    }

    /// <summary>Opens a route directly. Protected pages keep the signed-in user on a full page load (YRP-214).</summary>
    protected void NavigateTo(string path)
    {
        Driver.Navigate().GoToUrl($"{FrontendUrl}{path}");
        Wait.Until(d => d.Url.Contains(path.Split('?')[0]));
    }

    /// <summary>The single open in-page dialog (Add a book, Manage genres, …).</summary>
    protected IWebElement OpenDialog() => Wait.Until(d => d.FindElements(By.CssSelector("dialog[open]")).FirstOrDefault());

    /// <summary>Creates a fresh, uniquely named, available book through the Admin UI and returns its ISBN.</summary>
    protected string CreateReservableBookViaAdminUi(string title, int copies = 3)
    {
        LoginAsAdmin();
        NavigateTo("/admin/books");

        var genreName = CreateGenreViaAdminUi();
        var isbn = $"QA-{Guid.NewGuid():N}"[..18];

        Wait.Until(d => d.FindElement(By.CssSelector("button.admin-primary-action"))).Click();   // "Add new book"
        var dialog = OpenDialog();
        SetReactInputValue(dialog.FindElement(By.Id("book-title")), title);
        SetReactInputValue(dialog.FindElement(By.Id("book-author")), "QA Lending Author");
        SetReactInputValue(dialog.FindElement(By.Id("book-isbn")), isbn);
        Wait.Until(_ => dialog.FindElements(By.CssSelector("#book-genre option")).Any(o => o.Text == genreName));
        new SelectElement(dialog.FindElement(By.Id("book-genre"))).SelectByText(genreName);
        SetReactInputValue(dialog.FindElement(By.Id("book-copies")), copies.ToString());
        dialog.FindElement(By.CssSelector("button[type='submit'].modal-button-primary")).Click();

        Wait.Until(d => d.PageSource.Contains($"Successfully added \"{title}\""));
        return isbn;
    }

    /// <summary>The Add a book form needs a genre to pick from; this adds a fresh one through "Manage genres".</summary>
    protected string CreateGenreViaAdminUi()
    {
        var genreName = $"QA Genre {Guid.NewGuid():N}"[..16];

        Wait.Until(d => d.FindElements(By.XPath("//div[contains(@class,'page-banner-actions')]//button[contains(., 'Manage genres')]")).FirstOrDefault())!.Click();
        var dialog = OpenDialog();
        var nameInput = dialog.FindElement(By.Id("new-genre-name"));
        SetReactInputValue(nameInput, genreName);
        nameInput.FindElement(By.XPath("ancestor::form")).FindElement(By.CssSelector("button[type='submit']")).Click();
        Wait.Until(_ => dialog.Text.Contains(genreName));

        dialog.FindElement(By.CssSelector("button.modal-close")).Click();
        Wait.Until(d => !d.FindElements(By.CssSelector("dialog[open]")).Any());
        return genreName;
    }

    /// <summary>
    /// Sets a React-controlled input's value through the native setter and a real 'input' event so React's
    /// onChange fires. Walks the element's own prototype chain (reading the setter off window.HTMLInputElement
    /// from another realm throws "Illegal invocation").
    /// </summary>
    protected void SetReactInputValue(IWebElement element, string value)
    {
        ((IJavaScriptExecutor)Driver).ExecuteScript(
            "var el = arguments[0]; var value = arguments[1];" +
            "var proto = Object.getPrototypeOf(el);" +
            "while (proto && !Object.prototype.hasOwnProperty.call(proto, 'value')) proto = Object.getPrototypeOf(proto);" +
            "Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, value);" +
            "el.dispatchEvent(new Event('input', { bubbles: true }));",
            element, value);
    }

    /// <summary>Opens the catalogue search for a title, as a member would.</summary>
    protected void SearchFor(string title) => NavigateTo($"/search?q={Uri.EscapeDataString(title)}");

    /// <summary>Finds the book's Borrow button in its current state (available, pending, borrowed or no copies).</summary>
    protected IWebElement WaitForBorrowButton(string title, string ariaLabel) =>
        Wait.Until(d => d.FindElements(By.CssSelector($"button[aria-label=\"{ariaLabel}\"]")).FirstOrDefault())
        ?? throw new NoSuchElementException($"No button labelled '{ariaLabel}' for '{title}'.");

    /// <summary>Reserves (borrows) the given book from catalogue search as whichever member is signed in.</summary>
    protected void ReserveBookAsCurrentUser(string title)
    {
        SearchFor(title);
        WaitForBorrowButton(title, $"Borrow {title}").Click();
        Wait.Until(d => d.PageSource.Contains("Book reserved successfully"));
        WaitForBorrowButton(title, $"Your request for {title} is waiting for approval");
    }

    /// <summary>Accepts a native window.confirm(...) if the action opens one (Return and Cancel do).</summary>
    protected void AcceptConfirmDialogIfPresent()
    {
        try
        {
            Wait.Until(d =>
            {
                try { return d.SwitchTo().Alert(); }
                catch (NoAlertPresentException) { return null; }
            })?.Accept();
        }
        catch (WebDriverTimeoutException)
        {
            // No dialog appeared; nothing to accept.
        }
    }

    /// <summary>Logs in through the Auth API (not the browser) for set-up calls the UI doesn't expose.</summary>
    protected static async Task<string> GetAdminApiTokenAsync()
    {
        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync($"{UserServiceUrl}/api/auth/login", new { email = AdminEmail, password = AdminPassword });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    /// <summary>Resolves the inventory id of a book created by <see cref="CreateReservableBookViaAdminUi"/>.</summary>
    protected static async Task<int> GetBookIdByIsbnAsync(string isbn, string adminToken)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var books = await client.GetFromJsonAsync<JsonElement>($"{InventoryServiceUrl}/api/Books");
        foreach (var book in books.EnumerateArray())
            if (book.GetProperty("isbn").GetString() == isbn)
                return book.GetProperty("id").GetInt32();
        throw new InvalidOperationException($"No inventory book found with ISBN '{isbn}'.");
    }

    /// <summary>Sets a book's available copies to 0 through the admin API (the "Mark unavailable" action).</summary>
    protected static async Task MarkBookUnavailableAsync(int bookId, string adminToken)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        (await client.PatchAsync($"{InventoryServiceUrl}/api/books/{bookId}/mark-unavailable", null)).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Backdates the due date of the book's single active loan directly in the lending database, so a
    /// return can be tested as overdue without waiting real days. It only reaches a state that time
    /// itself would produce; what Return computes from that date is what the test checks.
    /// </summary>
    protected static async Task BackdateActiveLoanDueDateAsync(int bookId, int daysAgo)
    {
        await using var connection = new MySqlConnection(LendingDbConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE BorrowRecords SET DueDate = DATE_SUB(UTC_TIMESTAMP(), INTERVAL @daysAgo DAY) " +
            "WHERE BookId = @bookId AND ReturnDate IS NULL ORDER BY Id DESC LIMIT 1";
        command.Parameters.AddWithValue("@daysAgo", daysAgo);
        command.Parameters.AddWithValue("@bookId", bookId);
        var rows = await command.ExecuteNonQueryAsync();
        if (rows != 1)
            throw new InvalidOperationException($"Expected to backdate exactly 1 active loan for book {bookId}, backdated {rows}.");
    }

    public void Dispose()
    {
        Driver.Quit();
        Driver.Dispose();
    }
}
