using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace UserService.Tests;

public class RBACSeleniumTests : IDisposable
{
    private readonly IWebDriver driver;
    private readonly WebDriverWait wait;

    private const string FrontendUrl = "http://localhost:5173";

    private const string AdminEmail = "admin@library.com";
    private const string AdminPassword = "adminpassword";

    private const string UserEmail = "dahamyakulandi21@gmail.com";
    private const string UserPassword = "Pwd123*";

    public RBACSeleniumTests()
    {
        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");

        driver = new ChromeDriver(options);
        wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
    }

    // Admin should be able to access the admin dashboard
    [Fact]
    public void TC_RBAC_001_Admin_Can_Access_Admin_Dashboard()
    {
        LoginAsAdmin();

        Assert.Contains("/admin/dashboard", driver.Url);
    }

    // Normal user should not be able to access the admin dashboard
    [Fact]
    public void TC_RBAC_002_Normal_User_Cannot_Access_Admin_Dashboard()
    {
        LoginAsUser();

        driver.Navigate().GoToUrl($"{FrontendUrl}/admin/dashboard");

        wait.Until(d => !d.Url.Contains("/admin/dashboard"));

        Assert.DoesNotContain("/admin/dashboard", driver.Url);
    }

    // Admin navigation should be visible to an admin
    [Fact]
    public void TC_RBAC_003_Admin_Can_See_Admin_Navigation()
    {
        LoginAsAdmin();

        var adminDashboardLink = wait.Until(d => d.FindElement(By.CssSelector("a[href='/admin/users/pending']")));

        Assert.True(adminDashboardLink.Displayed, "Admin dashboard navigation should be visible to an admin.");
    }

    // Admin navigation should not be visible to a normal user
    [Fact]
    public void TC_RBAC_004_Normal_User_Cannot_See_Admin_Navigation()
    {
        LoginAsUser();

        var adminLinks = driver.FindElements(By.CssSelector("a[href='/admin/dashboard']"));

        Assert.Empty(adminLinks);
    }

    // Normal user should still be able to access their own page
    [Fact]
    public void TC_RBAC_005_Normal_User_Can_Access_User_Home()
    {
        LoginAsUser();

        Assert.Contains("/home", driver.Url);
    }

    // Unauthenticated user should be redirected away from the admin dashboard
    [Fact]
    public void TC_RBAC_006_Unauthenticated_User_Cannot_Access_Admin_Dashboard()
    {
        driver.Navigate().GoToUrl($"{FrontendUrl}/admin/dashboard");

        wait.Until(d => !d.Url.Contains("/admin/dashboard"));

        Assert.DoesNotContain("/admin/dashboard", driver.Url);
        Assert.Contains("/login", driver.Url);
    }

    // ============================================================
    // Helper Method - Admin Login
    // ============================================================

    private void LoginAsAdmin()
    {
        driver.Navigate().GoToUrl($"{FrontendUrl}/login");

        var email = wait.Until(d => d.FindElement(By.Id("email")));
        var password = wait.Until(d => d.FindElement(By.Id("password")));

        email.Clear();
        email.SendKeys(AdminEmail);

        password.Clear();
        password.SendKeys(AdminPassword);

        var loginButton = wait.Until(d => d.FindElement(By.CssSelector("button[type='submit']")));
        loginButton.Click();

        wait.Until(d => d.Url.Contains("/admin/dashboard"));
    }

    // ============================================================
    // Helper Method - Normal User Login
    // ============================================================

    private void LoginAsUser()
    {
        driver.Navigate().GoToUrl($"{FrontendUrl}/login");

        var email = wait.Until(d => d.FindElement(By.Id("email")));
        var password = wait.Until(d => d.FindElement(By.Id("password")));

        email.Clear();
        email.SendKeys(UserEmail);

        password.Clear();
        password.SendKeys(UserPassword);

        var loginButton = wait.Until(d => d.FindElement(By.CssSelector("button[type='submit']")));
        loginButton.Click();

        wait.Until(d => d.Url.Contains("/home"));
    }

    // ============================================================
    // Dispose
    // ============================================================

    public void Dispose()
    {
        try
        {
            driver.Quit();
        }
        catch
        {
            // Ignore browser cleanup errors
        }

        driver.Dispose();
    }
}