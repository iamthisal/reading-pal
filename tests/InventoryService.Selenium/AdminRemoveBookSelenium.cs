using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;
using Xunit;

namespace InventoryService.SeleniumTests
{
    public class AdminRemoveBookSelenium : IDisposable
    {
        private readonly IWebDriver driver;
        private readonly WebDriverWait wait;
        private const string FrontendUrl = "http://localhost:5173";

        private const string AdminEmail = "admin@library.com";
        private const string AdminPassword = "adminpassword";

        public AdminRemoveBookSelenium()
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
        public void Admin_Can_Remove_Book()
        {
            LoginAsAdmin();

            ClickDashboardLink("/admin/books");

            // wait for table rows to appear
            wait.Until(d => d.FindElements(By.CssSelector("table tbody tr")).Count >= 0);

            var rows = driver.FindElements(By.CssSelector("table tbody tr"));

            if (rows.Count == 0)
            {
                // if no rows exist, create one via the add form on the same page
                driver.FindElement(By.Id("book-title")).SendKeys("Temp Delete Book");
                driver.FindElement(By.Id("book-author")).SendKeys("Temp Author");
                var isbn = "REM-" + Guid.NewGuid().ToString().Substring(0, 8);
                driver.FindElement(By.Id("book-isbn")).SendKeys(isbn);
                driver.FindElement(By.Id("book-genre")).SendKeys("TempGenre");
                var copies = driver.FindElement(By.Id("book-copies"));
                copies.Clear();
                copies.SendKeys("1");
                driver.FindElement(By.CssSelector("button[type='submit']")).Click();

                // wait for new row to appear
                wait.Until(d => d.FindElements(By.XPath($"//td[contains(normalize-space(.),'{isbn}')]")).Count > 0);
            }

            // re-query rows and remove the last one
            var beforeRows = driver.FindElements(By.CssSelector("table tbody tr")).ToList();
            var originalCount = beforeRows.Count;
            var lastRow = beforeRows.Last();
            var removeBtn = lastRow.FindElements(By.XPath(".//button[contains(.,'Remove') or contains(.,'Delete')]")).FirstOrDefault();
            Assert.NotNull(removeBtn);
            removeBtn.Click();

            // accept confirmation if present
            try
            {
                wait.Until(d => { try { return d.SwitchTo().Alert() != null; } catch { return false; } });
                var alert = driver.SwitchTo().Alert();
                alert.Accept();
            }
            catch { }

            // wait for count to decrease
            wait.Until(d => d.FindElements(By.CssSelector("table tbody tr")).Count == originalCount - 1);
            var afterRows = driver.FindElements(By.CssSelector("table tbody tr")).ToList();
            Assert.Equal(originalCount - 1, afterRows.Count);
        }

        public void Dispose()
        {
            try { driver.Quit(); } catch { }
            driver.Dispose();
        }
    }
}
