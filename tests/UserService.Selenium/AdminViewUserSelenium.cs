using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace UserService.Tests;

public class AdminViewUserSelenium : IDisposable
{
    private readonly IWebDriver driver;
    private readonly WebDriverWait wait;

    private const string FrontendUrl = "http://localhost:5173";

    private const string AdminEmail = "admin@library.com";
    private const string AdminPassword = "adminpassword";

    public AdminViewUserSelenium()
    {
        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");

        driver = new ChromeDriver(options);
        wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void TC_ADMIN_001_Admin_Can_Login()
    {
        LoginAsAdmin();

        Assert.Contains("/admin/dashboard", driver.Url);
    }

    // No prior login here, so there is no in-memory auth state to lose.
    // A hard GoToUrl is fine — it mirrors a user typing the URL directly.
    [Fact]
    public void TC_ADMIN_002_Normal_User_Cannot_Access_Admin_Dashboard()
    {
        driver.Navigate().GoToUrl($"{FrontendUrl}/admin/dashboard");

        wait.Until(d => !d.Url.Contains("/admin/dashboard"));

        Assert.DoesNotContain("/admin/dashboard", driver.Url);
    }

    [Fact]
    public void TC_ADMIN_003_Admin_Can_View_Active_Users()
    {
        LoginAsAdmin();

        ClickDashboardLink("/admin/users/active");
        WaitForUserListToResolve("Active Users");

        Assert.Contains("/admin/users/active", driver.Url);
    }

    [Fact]
    public void TC_ADMIN_004_Active_User_List_Shows_User_Details()
    {
        LoginAsAdmin();

        ClickDashboardLink("/admin/users/active");
        WaitForUserListToResolve("Active Users");
        
        try {
            Assert.DoesNotContain("Failed to load user list.", driver.PageSource);
            Assert.Contains("Dahamya", driver.PageSource);
            Assert.Contains("dahamku@gmail.com", driver.PageSource); // check email instead of last name
           }
        catch {
            Console.WriteLine("=== FULL VISIBLE TEXT ON FAILURE ===");
            Console.WriteLine(driver.FindElement(By.TagName("body")).Text);
            throw;
            }
    }

    [Fact]
    public void TC_ADMIN_005_Admin_Account_Is_Excluded_From_Active_User_List()
    {
        LoginAsAdmin();

        ClickDashboardLink("/admin/users/active");
        WaitForUserListToResolve("Active Users");

        Assert.DoesNotContain(AdminEmail, driver.PageSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TC_ADMIN_006_Admin_Can_View_Pending_Users()
    {
        LoginAsAdmin();

        ClickDashboardLink("/admin/users/pending");
        WaitForUserListToResolve("Pending Approvals");

        Assert.Contains("/admin/users/pending", driver.Url);
        Assert.Contains("Pending Approvals", driver.PageSource);
    }

    private void LoginAsAdmin()
    {
        driver.Navigate().GoToUrl($"{FrontendUrl}/login");

        driver.FindElement(By.Id("email")).SendKeys(AdminEmail);
        driver.FindElement(By.Id("password")).SendKeys(AdminPassword);
        driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        wait.Until(d => d.Url.Contains("/admin/dashboard"));
    }

    // Clicks the real <a href="..."> rendered by <Link> on the dashboard,
    // instead of driver.Navigate().GoToUrl(), so the SPA never reloads
    // and the in-memory auth token survives.
    private void ClickDashboardLink(string path)
    {
        var link = wait.Until(d => d.FindElement(By.CssSelector($"a[href='{path}']")));
        link.Click();

        wait.Until(d => d.Url.Contains(path));
    }

    // AdminUsersPage renders its heading synchronously on mount, then
    // fetches data async and briefly shows "Loading users...".
    // Requiring the heading's presence rules out the case where the
    // page hasn't rendered anything at all yet (raw HTML shell), which
    // would otherwise make "!Contains(Loading users...)" vacuously true
    // and let assertions run against an empty page.
    private void WaitForUserListToResolve(string expectedHeading)
    {
        wait.Until(d =>
        {
            string page = d.PageSource;

            return page.Contains(expectedHeading) && !page.Contains("Loading users...");
        });
    }

    public void Dispose()
    {
        driver.Quit();
        driver.Dispose();
    }
}