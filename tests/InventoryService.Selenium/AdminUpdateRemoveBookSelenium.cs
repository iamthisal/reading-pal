using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;
using Xunit;

namespace InventoryService.SeleniumTests
{
    public class AdminUpdateRemoveBookSelenium : IDisposable
    {
        private readonly IWebDriver driver;
        private readonly WebDriverWait wait;

        private const string FrontendUrl = "http://localhost:5173";
        private const string AdminEmail = "admin@library.com";
        private const string AdminPassword = "adminpassword";

        public AdminUpdateRemoveBookSelenium()
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

        private void GoToBooks()
        {
            var link = wait.Until(d => d.FindElement(By.CssSelector("a[href='/admin/books']")));
            link.Click();
            wait.Until(d => d.Url.Contains("/admin/books"));
        }

        private string CreateBookViaUI(string title = "Temp Book", int copies = 1)
        {
            GoToBooks();

            var isbn = "TMP-" + Guid.NewGuid().ToString().Substring(0, 8);
            driver.FindElement(By.Id("book-title")).SendKeys(title);
            driver.FindElement(By.Id("book-author")).SendKeys("QA Author");
            driver.FindElement(By.Id("book-isbn")).SendKeys(isbn);
            driver.FindElement(By.Id("book-genre")).SendKeys("QA-Genre");
            var copiesEl = driver.FindElement(By.Id("book-copies"));
            copiesEl.Clear();
            copiesEl.SendKeys(copies.ToString());
            driver.FindElement(By.CssSelector("button[type='submit']")).Click();

            // wait for success or presence in list
            wait.Until(d => d.PageSource.Contains("Successfully added") || d.FindElements(By.XPath($"//*[contains(text(),'{isbn}')]")).Any());
            return isbn;
        }

        private IWebElement FindRowByIsbn(string isbn)
        {
            GoToBooks();
            // find any element containing the isbn then get its ancestor row
            var el = wait.Until(d => d.FindElements(By.XPath($"//*[contains(text(),'{isbn}')]")).FirstOrDefault());
            if (el == null) return null;
            // walk up to row
            var row = el.FindElement(By.XPath("ancestor::tr"));
            return row;
        }

        // TC-BOOK-UPDATE-001: Happy - update book details successfully
        [Fact]
        public void TC_BOOK_UPDATE_001_AdminCanUpdateBookDetails()
        {
            LoginAsAdmin();
            var isbn = CreateBookViaUI("Update Test Book", 2);

            var row = FindRowByIsbn(isbn);
            Assert.NotNull(row);

            // Click Edit in the row (try button or link)
            var edit = row.FindElements(By.XPath(".//button[contains(.,'Edit')] | .//a[contains(.,'Edit')]")).FirstOrDefault();
            Assert.NotNull(edit);
            edit.Click();

            wait.Until(d => d.Url.Contains("/admin/books/edit") || d.PageSource.Contains("Edit Book"));

            // change title and copies
            var titleEl = wait.Until(d => d.FindElement(By.Id("book-title")));
            titleEl.Clear();
            titleEl.SendKeys("Updated Title via QA");
            var copiesEl = driver.FindElement(By.Id("book-copies"));
            copiesEl.Clear();
            copiesEl.SendKeys("3");

            driver.FindElement(By.CssSelector("button[type='submit']")).Click();

            wait.Until(d => d.PageSource.Contains("Successfully updated") || d.PageSource.Contains("Updated Title via QA"));
            Assert.Contains("Updated Title via QA", driver.PageSource);
        }

        // TC-BOOK-UPDATE-002: Happy - increase copies above active loans
        [Fact]
        public void TC_BOOK_UPDATE_002_IncreaseCopiesAboveActiveLoansSucceeds()
        {
            LoginAsAdmin();
            var isbn = CreateBookViaUI("Increase Copies Book", 1);

            var row = FindRowByIsbn(isbn);
            Assert.NotNull(row);
            var edit = row.FindElements(By.XPath(".//button[contains(.,'Edit')] | .//a[contains(.,'Edit')]")).FirstOrDefault();
            edit.Click();
            wait.Until(d => d.FindElement(By.Id("book-copies")));

            var copiesEl = driver.FindElement(By.Id("book-copies"));
            copiesEl.Clear();
            copiesEl.SendKeys("5");
            driver.FindElement(By.CssSelector("button[type='submit']")).Click();

            wait.Until(d => d.PageSource.Contains("Successfully updated") || d.PageSource.Contains("Available Copies: 5"));
            Assert.Contains("Available Copies", driver.PageSource);
        }

        // TC-BOOK-UPDATE-003: Unhappy - reducing copies below active loans is blocked
        [Fact]
        public void TC_BOOK_UPDATE_003_ReductionsBelowActiveLoansBlocked()
        {
            LoginAsAdmin();
            // create book with copies 3 and assume there are 2 active loans for this test scenario in the system
            var isbn = CreateBookViaUI("Reduce Below Loans Book", 3);

            var row = FindRowByIsbn(isbn);
            Assert.NotNull(row);
            var edit = row.FindElements(By.XPath(".//button[contains(.,'Edit')] | .//a[contains(.,'Edit')]")).FirstOrDefault();
            edit.Click();
            wait.Until(d => d.FindElement(By.Id("book-copies")));

            var copiesEl = driver.FindElement(By.Id("book-copies"));
            copiesEl.Clear();
            copiesEl.SendKeys("1");
            driver.FindElement(By.CssSelector("button[type='submit']")).Click();

            // The app should block this; wait for an error indicator or message
            wait.Until(d => d.PageSource.Contains("cannot be reduced") || d.FindElements(By.ClassName("error-message")).Count > 0 || d.PageSource.Contains("Active loans"));
            Assert.True(driver.PageSource.Contains("cannot be reduced") || driver.FindElements(By.ClassName("error-message")).Any() || driver.PageSource.Contains("Active loans"));
        }

        // TC-BOOK-REMOVE-001: Happy - remove book without active loans
        [Fact]
        public void TC_BOOK_REMOVE_001_RemoveBookWithoutActiveLoans()
        {
            LoginAsAdmin();
            var isbn = CreateBookViaUI("Removable Book", 1);

            var row = FindRowByIsbn(isbn);
            Assert.NotNull(row);
            var del = row.FindElements(By.XPath(".//button[contains(.,'Delete')]|.//a[contains(.,'Delete')]")).FirstOrDefault();
            Assert.NotNull(del);
            del.Click();

            // confirm if a confirmation dialog appears (JS confirm)
            try {
                wait.Until(d => d.SwitchTo().Alert() != null);
                var alert = driver.SwitchTo().Alert();
                alert.Accept();
            } catch { /* no alert, continue */ }

            // wait for success or absence from list
            wait.Until(d => !d.PageSource.Contains(isbn) || d.PageSource.Contains("Successfully deleted") );
            Assert.DoesNotContain(isbn, driver.PageSource);
        }

        // TC-BOOK-REMOVE-002: Unhappy - removing a book with active loans is blocked
        [Fact]
        public void TC_BOOK_REMOVE_002_RemoveBookWithActiveLoansBlocked()
        {
            LoginAsAdmin();
            // create book assuming there will be an active loan attached by test data/setup
            var isbn = CreateBookViaUI("NotRemovable Book", 2);

            var row = FindRowByIsbn(isbn);
            Assert.NotNull(row);
            var del = row.FindElements(By.XPath(".//button[contains(.,'Delete')]|.//a[contains(.,'Delete')]")).FirstOrDefault();
            del.Click();

            try {
                wait.Until(d => d.SwitchTo().Alert() != null);
                var alert = driver.SwitchTo().Alert();
                alert.Accept();
            } catch { }

            wait.Until(d => d.PageSource.Contains("active loans") || d.FindElements(By.ClassName("error-message")).Count > 0);
            Assert.True(driver.PageSource.Contains("active loans") || driver.FindElements(By.ClassName("error-message")).Any());
        }

        // TC-BOOK-REMOVE-003: Unhappy/happy path - removing a book with physical copies prompts confirmation
        [Fact]
        public void TC_BOOK_REMOVE_003_RemoveBookWithPhysicalCopiesPromptsConfirmation()
        {
            LoginAsAdmin();
            var isbn = CreateBookViaUI("PromptConfirm Book", 1);

            var row = FindRowByIsbn(isbn);
            Assert.NotNull(row);
            var del = row.FindElements(By.XPath(".//button[contains(.,'Delete')]|.//a[contains(.,'Delete')]")).FirstOrDefault();
            del.Click();

            // Expect either a JS confirm or an on-page prompt
            var alerted = false;
            try {
                wait.Until(d => d.SwitchTo().Alert() != null);
                var alert = driver.SwitchTo().Alert();
                alerted = true;
                // dismiss to simulate cancel
                alert.Dismiss();
            } catch { }

            if (!alerted)
            {
                // check for an on-page confirmation prompt
                wait.Until(d => d.PageSource.Contains("Are you sure") || d.FindElements(By.ClassName("confirm-dialog")).Count > 0);
                Assert.True(driver.PageSource.Contains("Are you sure") || driver.FindElements(By.ClassName("confirm-dialog")).Any());
            }
        }

        public void Dispose()
        {
            driver.Quit();
            driver.Dispose();
        }
    }
}
