using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;
using Xunit;

namespace InventoryService.SeleniumTests
{
    public class AdminUpdateBookSelenium : IDisposable
    {
        private readonly IWebDriver driver;
        private readonly WebDriverWait wait;
        private const string FrontendUrl = "http://localhost:5173";

        private const string AdminEmail = "admin@library.com";
        private const string AdminPassword = "adminpassword";

        public AdminUpdateBookSelenium()
        {
            var options = WebDriverHelper.CreateOptions();
            driver = new ChromeDriver(options);
            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
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

        [Fact]
        public void Admin_Can_Update_Book()
        {
            LoginAsAdmin();

            ClickDashboardLink("/admin/books");

            // wait for table rows to appear
            wait.Until(d => d.FindElements(By.CssSelector("table tbody tr")).Count >= 0);

            var rows = driver.FindElements(By.CssSelector("table tbody tr")).ToList();

            if (rows.Count == 0)
            {
                // create a book using the add form on the page
                var isbn = "UPD-" + Guid.NewGuid().ToString().Substring(0, 8);
                driver.FindElement(By.Id("book-title")).SendKeys("Temp Update Book");
                driver.FindElement(By.Id("book-author")).SendKeys("Temp Author");
                driver.FindElement(By.Id("book-isbn")).SendKeys(isbn);
                driver.FindElement(By.Id("book-genre")).SendKeys("TempGenre");
                var copies = driver.FindElement(By.Id("book-copies"));
                copies.Clear();
                copies.SendKeys("1");
                driver.FindElement(By.CssSelector("button[type='submit']")).Click();

                wait.Until(d => d.FindElements(By.XPath($"//td[contains(normalize-space(.),'{isbn}')]")).Count > 0);
            }

            // re-query rows and pick the last one
            var beforeRows = driver.FindElements(By.CssSelector("table tbody tr")).ToList();
            var lastRow = beforeRows.Last();

            var editBtn = lastRow.FindElements(By.XPath(".//button[contains(.,'Edit') or contains(.,'Update')]")).FirstOrDefault();
            Assert.NotNull(editBtn);
            editBtn.Click();

            // Wait for edit dialog fields
            wait.Until(d => d.FindElements(By.Id("edit-book-title-input")).Any());
            var editTitle = driver.FindElement(By.Id("edit-book-title-input"));
            editTitle.Clear();
            editTitle.SendKeys("Updated Title QA");
            var editCopies = driver.FindElement(By.Id("edit-book-copies"));
            editCopies.Clear();
            editCopies.SendKeys("4");

            var saveBtn = driver.FindElements(By.XPath("//button[contains(.,'Save Changes') or contains(.,'Save')]")).FirstOrDefault();
            Assert.NotNull(saveBtn);
            saveBtn.Click();

            // wait for updated title to appear in table
            wait.Until(d => d.FindElements(By.XPath("//td[contains(normalize-space(.),'Updated Title QA')]")).Any());
            Assert.True(driver.FindElements(By.XPath("//td[contains(normalize-space(.),'Updated Title QA')]")).Any());
        }

        [Fact]
        public void UpdateBook_MissingRequiredFields_ShowsError()
        {
            LoginAsAdmin();

            ClickDashboardLink("/admin/books");

            // ensure at least one book exists
            wait.Until(d => d.FindElements(By.CssSelector("table tbody tr")).Count >= 0);
            var rows = driver.FindElements(By.CssSelector("table tbody tr")).ToList();
            if (rows.Count == 0)
            {
                var isbn = "UPD-MISS-" + Guid.NewGuid().ToString().Substring(0, 8);
                driver.FindElement(By.Id("book-title")).SendKeys("Temp Missing Book");
                driver.FindElement(By.Id("book-author")).SendKeys("Temp Author");
                driver.FindElement(By.Id("book-isbn")).SendKeys(isbn);
                driver.FindElement(By.Id("book-genre")).SendKeys("TempGenre");
                driver.FindElement(By.Id("book-copies")).Clear();
                driver.FindElement(By.Id("book-copies")).SendKeys("1");
                driver.FindElement(By.CssSelector("button[type='submit']")).Click();
                wait.Until(d => d.FindElements(By.XPath($"//td[contains(normalize-space(.),'{isbn}')]")).Count > 0);
            }

            // open edit for last row
            var lastRow = driver.FindElements(By.CssSelector("table tbody tr")).Last();
            var editBtn = lastRow.FindElements(By.XPath(".//button[contains(.,'Edit') or contains(.,'Update')]")).FirstOrDefault();
            Assert.NotNull(editBtn);
            editBtn.Click();

            // Wait for edit dialog fields
            wait.Until(d => d.FindElements(By.Id("edit-book-title-input")).Any());
            var editTitle = driver.FindElement(By.Id("edit-book-title-input"));
            editTitle.Clear();
            // Clear another required field
            var editIsbn = driver.FindElement(By.Id("edit-book-isbn"));
            editIsbn.Clear();

            var saveBtn = driver.FindElements(By.XPath("//button[contains(.,'Save Changes') or contains(.,'Save')]")).FirstOrDefault();
            Assert.NotNull(saveBtn);
            saveBtn.Click();

            // HTML5 validity should indicate required fields are invalid
            var titleValid = (bool)((IJavaScriptExecutor)driver).ExecuteScript("return document.getElementById('edit-book-title-input').checkValidity();");
            var isbnValid = (bool)((IJavaScriptExecutor)driver).ExecuteScript("return document.getElementById('edit-book-isbn').checkValidity();");
            Assert.False(titleValid || isbnValid, "Expected at least one required edit field to be invalid");
        }

        [Fact]
        public void UpdateBook_DuplicateISBN_ShowsConflict()
        {
            LoginAsAdmin();

            ClickDashboardLink("/admin/books");

            // create a book that will be used as duplicate target
            var dupIsbn = "DUP-" + Guid.NewGuid().ToString().Substring(0, 8);
            driver.FindElement(By.Id("book-title")).SendKeys("Dup Target Book");
            driver.FindElement(By.Id("book-author")).SendKeys("Dup Author");
            driver.FindElement(By.Id("book-isbn")).SendKeys(dupIsbn);
            driver.FindElement(By.Id("book-genre")).SendKeys("DupGenre");
            driver.FindElement(By.Id("book-copies")).Clear();
            driver.FindElement(By.Id("book-copies")).SendKeys("1");
            driver.FindElement(By.CssSelector("button[type='submit']")).Click();
            wait.Until(d => d.FindElements(By.XPath($"//td[contains(normalize-space(.),'{dupIsbn}')]")).Count > 0);

            // ensure another book exists to edit
            var rows = driver.FindElements(By.CssSelector("table tbody tr")).ToList();
            Assert.True(rows.Count >= 2, "Need at least two books to test duplicate ISBN behavior");

            // pick last row (not the duplicate) to edit
            var lastRow = rows.Last();
            // if lastRow already has dupIsbn, pick the second last
            if (lastRow.Text.Contains(dupIsbn) && rows.Count >= 2)
                lastRow = rows[rows.Count - 2];

            var editBtn = lastRow.FindElements(By.XPath(".//button[contains(.,'Edit') or contains(.,'Update')]")).FirstOrDefault();
            Assert.NotNull(editBtn);
            editBtn.Click();

            wait.Until(d => d.FindElements(By.Id("edit-book-isbn")).Any());
            var editIsbn = driver.FindElement(By.Id("edit-book-isbn"));
            editIsbn.Clear();
            editIsbn.SendKeys(dupIsbn);

            var saveBtn = driver.FindElements(By.XPath("//button[contains(.,'Save Changes') or contains(.,'Save')]")).FirstOrDefault();
            Assert.NotNull(saveBtn);
            saveBtn.Click();

            // wait for edit error message indicating duplicate
            wait.Until(d => d.FindElements(By.CssSelector(".error-message")).Any());
            var err = driver.FindElements(By.CssSelector(".error-message")).FirstOrDefault();
            Assert.NotNull(err);
            Assert.Contains("already exists", err.Text, StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            try { driver.Quit(); } catch { }
            driver.Dispose();
        }
    }
}
