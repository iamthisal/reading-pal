using OpenQA.Selenium;
using Xunit;

namespace ReadingPal.E2E.Support;

/// <summary>
/// Base for every end-to-end test: one browser, the API for set-up, and a record of everything the test
/// created so it can be removed afterwards (TEST-PLAN.md §3, "Isolation").
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    /// <summary>The genre every test book uses. It is shared by the whole run and left in place.</summary>
    public const string TestGenre = "E2E Testing";

    private const string SecondAdminEmail = "e2e.admin.b@library.test";
    private const string SecondAdminPassword = "E2e-admin-b-123";

    private readonly List<Browser> _browsers = [];
    private readonly List<int> _books = [];
    private readonly List<int> _authors = [];
    private readonly List<string> _lists = [];
    private readonly List<int> _genres = [];
    private readonly List<string> _members = [];
    private readonly List<string> _restoreContainers = [];
    private readonly List<string> _pausedContainers = [];
    private Browser? _browser;
    private static bool _genreReady;

    protected ReadingPalApi Api { get; } = new();

    /// <summary>The test's main browser, opened on first use.</summary>
    protected Browser Browser => _browser ??= OpenBrowser();

    protected Browser OpenBrowser(int width = 1440, int height = 900)
    {
        var browser = new Browser(width, height);
        _browsers.Add(browser);
        return browser;
    }

    public virtual async Task InitializeAsync()
    {
        if (_genreReady) return;
        if (await Api.GenreIdAsync(TestGenre) == null) await Api.CreateGenreAsync(TestGenre);
        _genreReady = true;
    }

    protected Task<string> AdminTokenAsync() => Api.AdminTokenAsync();

    // ---------- Test data ----------

    protected async Task<Member> NewMemberAsync(string? firstName = null)
    {
        var member = await Api.CreateApprovedMemberAsync(firstName);
        _members.Add(member.Email);
        return member;
    }

    protected async Task<Member> NewPendingMemberAsync(string? firstName = null)
    {
        var member = await Api.CreatePendingMemberAsync(firstName);
        _members.Add(member.Email);
        return member;
    }

    /// <summary>Registers a member through the API but leaves cleanup to the test's own record.</summary>
    protected void TrackMember(string email) => _members.Add(email);

    protected static string UniqueTitle(string label = "Book") => $"E2E {label} {TestIds.Short()}";

    protected async Task<BookInfo> NewBookAsync(int copies = 2, string? title = null, string? author = null,
        string genre = TestGenre, string? description = null, string? cover = null)
    {
        var book = await Api.CreateBookAsync(title ?? UniqueTitle(), author ?? $"E2E Author {TestIds.Short()}", TestIds.Isbn(),
            genre, copies, description, cover);
        _books.Add(book.Id);
        return book;
    }

    /// <summary>A book with no copies on the shelf: created, then marked unavailable.</summary>
    protected async Task<BookInfo> NewUnavailableBookAsync()
    {
        var book = await NewBookAsync(1);
        await Api.MarkUnavailableAsync(book.Id);
        return book;
    }

    protected void TrackBook(int id) => _books.Add(id);

    protected async Task<int> NewAuthorAsync(string name, string? description = null, string? imageUrl = null)
    {
        var result = await Api.CreateAuthorAsync(name, description, imageUrl);
        var id = result.Body?["id"]?.GetValue<int>() ?? throw new InvalidOperationException($"Create author failed: {result.Message}");
        _authors.Add(id);
        return id;
    }

    protected void TrackAuthor(int id) => _authors.Add(id);
    protected void TrackList(string key) => _lists.Add(key);
    protected void TrackGenre(int id) => _genres.Add(id);

    protected async Task<string> NewListAsync(string itemType = "Books", params int[] items)
    {
        var key = $"e2e-{TestIds.Short()}";
        var created = await Api.CreateListAsync(key, $"E2E List {key[4..]}", itemType);
        if (!created.Ok) throw new InvalidOperationException($"Create list failed: {created.Message}");
        _lists.Add(key);
        if (items.Length > 0) await Api.SetListItemsAsync(key, items);
        return key;
    }

    /// <summary>
    /// A second admin for the "other admins are told" cases. Admins cannot register, so a member is
    /// registered once and promoted in the database; the account is kept for later runs.
    /// </summary>
    protected async Task<string> SecondAdminTokenAsync()
    {
        var login = await Api.LoginAsync(SecondAdminEmail, SecondAdminPassword);
        if (login.Ok && (string?)login.Body?["role"] == "Admin") return login.Body!["token"]!.GetValue<string>();
        if (!login.Ok) await Api.RegisterAsync("Second", "Admin", SecondAdminEmail, SecondAdminPassword);
        await Database.PromoteToAdminAsync(SecondAdminEmail);
        return await Api.TokenForAsync(SecondAdminEmail, SecondAdminPassword);
    }

    protected const string SecondAdminAddress = SecondAdminEmail;

    // ---------- Outages ----------

    /// <summary>Stops a container for an outage case; it is started again after the test even if the test fails.</summary>
    protected void StopContainer(string container)
    {
        _restoreContainers.Add(container);
        Containers.Stop(container);
    }

    /// <summary>Freezes a container so its requests hang (a slow service); it is resumed after the test.</summary>
    protected void PauseContainer(string container)
    {
        _pausedContainers.Add(container);
        Containers.Pause(container);
    }

    protected void ResumeContainer(string container)
    {
        Containers.Unpause(container);
        _pausedContainers.Remove(container);
    }

    protected async Task StartContainerAsync(string container)
    {
        await Containers.StartAsync(container);
        _restoreContainers.Remove(container);
    }

    // ---------- Assertions used everywhere ----------

    protected static void AssertContains(string expected, string actual) =>
        Assert.True(actual.Contains(expected, StringComparison.Ordinal), $"Expected to find \"{expected}\" in:\n{actual}");

    protected static By Aria(string label) => Browser.ByAria(label);

    // ---------- Cleanup ----------

    public virtual async Task DisposeAsync()
    {
        foreach (var browser in _browsers) browser.Dispose();
        foreach (var container in _pausedContainers.ToList())
        {
            try { Containers.Unpause(container); } catch { /* best effort */ }
        }
        foreach (var container in _restoreContainers.ToList())
        {
            try { await Containers.StartAsync(container); } catch { /* best effort */ }
        }
        try { await CleanUpAsync(); } catch { /* cleanup must never hide the test result */ }
    }

    private async Task CleanUpAsync()
    {
        if (_books.Count > 0)
        {
            // Open requests and loans would block deleting the books, so close them first.
            foreach (var pending in await Api.PendingReservationsAsync())
            {
                if (_books.Contains(pending!["bookId"]!.GetValue<int>()))
                {
                    var id = pending["id"]!.GetValue<int>();
                    if ((string?)pending["status"] == "Accepting") await Api.AcceptReservationAsync(id);
                    else await Api.RejectReservationAsync(id);
                }
            }
            foreach (var loan in await Api.BorrowedAsync())
            {
                if (_books.Contains(loan!["bookId"]!.GetValue<int>())) await Api.ReturnAsync(loan["id"]!.GetValue<int>());
            }
            foreach (var id in _books.Distinct()) await Api.DeleteBookAsync(id);
        }
        foreach (var key in _lists.Distinct()) await Api.DeleteListAsync(key);
        foreach (var id in _authors.Distinct()) await Api.DeleteAuthorAsync(id);
        foreach (var id in _genres.Distinct()) await Api.DeleteGenreAsync(id);
        foreach (var email in _members.Distinct()) await Api.DeleteMemberAsync(email);
    }
}
