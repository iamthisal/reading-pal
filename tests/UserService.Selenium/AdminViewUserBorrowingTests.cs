using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace UserService.Tests;

public class AdminViewUserBorrowingTests : IDisposable
{
    private readonly IWebDriver driver;
    private readonly WebDriverWait wait;

    private const string FrontendUrl = "http://localhost:5173";

    private const string AdminEmail = "admin@library.com";
    private const string AdminPassword = "adminpassword";

    // Known seeded active user, same one TC_ADMIN_004 asserts on.
    private const string TargetUserName = "Dahamya";

    public AdminViewUserBorrowingTests()
    {
        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");

        driver = new ChromeDriver(options);
        wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void TC_BORROW_001_BorrowingInformationSectionsAreDisplayed()
    {
        LoginAsAdmin();

        ClickDashboardLink("/admin/users/active");
        WaitForUserListToResolve("Active Users");

        // Click on the user's name itself (not a generic button) to open
        // their details/borrowings view.
        var nameElement = wait.Until(d =>
            d.FindElement(By.XPath($"//*[contains(text(),'{TargetUserName}')]")));

        nameElement.Click();

        wait.Until(d =>
            d.FindElements(By.XPath("//*[contains(text(),'Current Borrowings')]")).Any());

        Assert.True(
            driver.FindElements(By.XPath("//*[contains(text(),'Current Borrowings')]")).Any());

        Assert.True(
            driver.FindElements(By.XPath("//*[contains(text(),'Past Borrowings')]")).Any());
    }

    private void LoginAsAdmin()
    {
        driver.Navigate().GoToUrl($"{FrontendUrl}/login");

        driver.FindElement(By.Id("email")).SendKeys(AdminEmail);
        driver.FindElement(By.Id("password")).SendKeys(AdminPassword);
        driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        wait.Until(d => d.Url.Contains("/admin/dashboard"));
    }

    private void ClickDashboardLink(string path)
    {
        var link = wait.Until(d => d.FindElement(By.CssSelector($"a[href='{path}']")));
        link.Click();

        wait.Until(d => d.Url.Contains(path));
    }

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