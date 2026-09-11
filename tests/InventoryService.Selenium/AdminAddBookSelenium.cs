using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using Xunit;

namespace InventoryService.SeleniumTests
{
    public class AdminAddBookSelenium : IDisposable
    {
        private readonly IWebDriver driver;
        private readonly WebDriverWait wait;

        private const string FrontendUrl = "http://localhost:5173";

        private const string AdminEmail = "admin@library.com";
        private const string AdminPassword = "adminpassword";

        public AdminAddBookSelenium()
        {
            var options = WebDriverHelper.CreateOptions();

            driver = new ChromeDriver(options);
            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
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

        // TC-BOOK-UI-001: Admin can add a book successfully
        [Fact]
        public void Admin_Can_Add_Book_Successfully()
        {
            LoginAsAdmin();

            ClickDashboardLink("/admin/books");

            // Fill the add book form
            driver.FindElement(By.Id("book-title")).SendKeys("Selenium Test Book");
            driver.FindElement(By.Id("book-author")).SendKeys("Test Author");
            var isbn = "TEST-ISBN-" + Guid.NewGuid().ToString().Substring(0, 8);
            driver.FindElement(By.Id("book-isbn")).SendKeys(isbn);
            driver.FindElement(By.Id("book-genre")).SendKeys("Testing");
            driver.FindElement(By.Id("book-copies")).Clear();
            driver.FindElement(By.Id("book-copies")).SendKeys("3");

            driver.FindElement(By.CssSelector("button[type='submit']")).Click();

            // Wait for either success or error message
            wait.Until(d => d.PageSource.Contains("Successfully added") || d.FindElements(By.ClassName("error-message")).Count > 0);

            Assert.Contains("Successfully added", driver.PageSource);
            Assert.Contains("Selenium Test Book", driver.PageSource);
            // Check that available copies shown equals 3 somewhere on page
            Assert.Contains("Available Copies: 3", driver.PageSource); // fallback text check used in successMessage
        }

        // TC-BOOK-UI-002: Missing required fields should show validation error
        [Fact]
        public void AddBook_MissingRequiredFields_ShowsError()
        {
            LoginAsAdmin();

            ClickDashboardLink("/admin/books");

            // Leave title empty and submit
            driver.FindElement(By.Id("book-title")).Clear();
            driver.FindElement(By.Id("book-author")).SendKeys("Author X");
            driver.FindElement(By.Id("book-isbn")).SendKeys("ISBN-MISSING-TITLE");
            driver.FindElement(By.Id("book-genre")).SendKeys("Genre");
            driver.FindElement(By.Id("book-copies")).Clear();
            driver.FindElement(By.Id("book-copies")).SendKeys("1");

            driver.FindElement(By.CssSelector("button[type='submit']")).Click();

            // Browser native validation prevents submission when required fields are missing.
            // Assert the title input is invalid according to HTML5 constraint validation.
            var titleValid = (bool)((IJavaScriptExecutor)driver).ExecuteScript("return document.getElementById('book-title').checkValidity();");
            Assert.False(titleValid, "Expected title input to be invalid when left empty");
        }

        // TC-BOOK-UI-003: Total copies must be at least 1
        [Fact]
        public void AddBook_InvalidTotalCopies_ShowsError()
        {
            LoginAsAdmin();

            ClickDashboardLink("/admin/books");

            driver.FindElement(By.Id("book-title")).SendKeys("Bad Copies Book");
            driver.FindElement(By.Id("book-author")).SendKeys("Author Y");
            driver.FindElement(By.Id("book-isbn")).SendKeys("ISBN-BAD-COPIES");
            driver.FindElement(By.Id("book-genre")).SendKeys("Genre");
            driver.FindElement(By.Id("book-copies")).Clear();
            driver.FindElement(By.Id("book-copies")).SendKeys("0");

            driver.FindElement(By.CssSelector("button[type='submit']")).Click();

            // The input has a min="1" attribute so the browser prevents submission.
            // Assert the copies input is invalid per HTML5 validation.
            var copiesValid = (bool)((IJavaScriptExecutor)driver).ExecuteScript("return document.getElementById('book-copies').validity.valid;");
            Assert.False(copiesValid, "Expected book-copies input to be invalid when set to 0");
        }

        public void Dispose()
        {
            driver.Quit();
            driver.Dispose();
        }
    }
}
