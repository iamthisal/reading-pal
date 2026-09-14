using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;
using Xunit;

namespace InventoryService.SeleniumTests
{
    public class BookAvailabilitySelenium : IDisposable
    {
        private readonly IWebDriver driver;
        private readonly WebDriverWait wait;

        private const string FrontendUrl = "http://localhost:5173";
        private const string AdminEmail = "admin@library.com";
        private const string AdminPassword = "adminpassword";
        private const string UserEmail = "dilum@icloud.com";
        private const string UserPassword = "0768005064";

        public BookAvailabilitySelenium()
        {
            var options = WebDriverHelper.CreateOptions();
            driver = new ChromeDriver(options);
            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
        }

        private void LoginAs(string email, string password)
        {
            driver.Navigate().GoToUrl($"{FrontendUrl}/login");
            var emailInput = wait.Until(d => d.FindElement(By.Id("email")));
            emailInput.Clear();
            emailInput.SendKeys(email);

            var passwordInput = driver.FindElement(By.Id("password"));
            passwordInput.Clear();
            passwordInput.SendKeys(password);

            driver.FindElement(By.CssSelector("button[type='submit']")).Click();
        }

        private void LoginAsAdmin()
        {
            LoginAs(AdminEmail, AdminPassword);
            wait.Until(d => d.Url.Contains("/admin/dashboard"));
        }

        private void LoginAsUser()
        {
            LoginAs(UserEmail, UserPassword);
            wait.Until(d => d.Url.Contains("/home"));
        }

        private void OpenAdminBooksPage()
        {
            var bookInventoryLink = wait.Until(d =>
                d.FindElement(By.XPath("//a[.//h3[contains(normalize-space(.), 'Book Inventory')]]"))
            );
            bookInventoryLink.Click();

            wait.Until(d =>
                d.Url.Contains("/admin/books") ||
                d.PageSource.Contains("Book Inventory Management")
            );
        }

        private void GoToPublicCatalogueFromDashboard()
        {
            var publicCatalogueLink = wait.Until(d =>
                d.FindElement(By.XPath("//a[.//h3[contains(normalize-space(.), 'Public Catalogue')]]"))
            );
            publicCatalogueLink.Click();

            wait.Until(d => d.Url.Contains("/home") || d.PageSource.Contains("Discover") || d.PageSource.Contains("Available Books"));
        }

        private void GoToCatalogue()
        {
            try
            {
                if (driver.Url.Contains("/admin/books"))
                {
                    var dashboardLink = wait.Until(d =>
                        d.FindElement(By.XPath("//a[contains(@href, '/admin/dashboard') or contains(normalize-space(.), 'Dashboard')]"))
                    );
                    dashboardLink.Click();
                    wait.Until(d => d.Url.Contains("/admin/dashboard"));
                }

                GoToPublicCatalogueFromDashboard();
            }
            catch
            {
                driver.Navigate().GoToUrl($"{FrontendUrl}/home");
                wait.Until(d => d.Url.Contains("/home") || d.PageSource.Contains("Discover") || d.PageSource.Contains("Available Books"));
            }
        }

        private string CreateBookViaAdminUi(string title, int copies)
        {
            LoginAsAdmin();
            OpenAdminBooksPage();

            var isbn = $"QA-{Guid.NewGuid():N}".Substring(0, 18);

            var titleInput = wait.Until(d => d.FindElement(By.Id("book-title")));
            titleInput.Clear();
            titleInput.SendKeys(title);

            driver.FindElement(By.Id("book-author")).Clear();
            driver.FindElement(By.Id("book-author")).SendKeys("QA Availability Author");

            driver.FindElement(By.Id("book-isbn")).Clear();
            driver.FindElement(By.Id("book-isbn")).SendKeys(isbn);

            driver.FindElement(By.Id("book-genre")).Clear();
            driver.FindElement(By.Id("book-genre")).SendKeys("QA Availability");

            var copiesInput = driver.FindElement(By.Id("book-copies"));
            copiesInput.Clear();
            copiesInput.SendKeys(copies.ToString());

            driver.FindElement(By.CssSelector("button[type='submit']")).Click();

            wait.Until(d => d.PageSource.Contains("Successfully added") || d.PageSource.Contains(title));
            return isbn;
        }

        private void MarkBookUnavailable(string title)
        {
            wait.Until(d =>
                d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).Any()
            );

            var row = driver.FindElement(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]"));
            var actionButton = row.FindElements(By.XPath(".//button[contains(., 'Unavailable')]"))
                .FirstOrDefault();

            Assert.NotNull(actionButton);
            actionButton.Click();

            try
            {
                var alert = wait.Until(d =>
                {
                    try
                    {
                        return d.SwitchTo().Alert();
                    }
                    catch (NoAlertPresentException)
                    {
                        return null;
                    }
                });
                alert?.Accept();
            }
            catch (WebDriverTimeoutException)
            {
                // Some flows do not show a browser alert before the patch request completes.
            }

            wait.Until(d => d.PageSource.Contains("is now marked as unavailable") || d.PageSource.Contains(title));
        }

        private IWebElement FindFeaturedBookCard(string title)
        {
            // Wait until book cards are loaded on the catalogue page
            wait.Until(d => d.FindElements(By.CssSelector(".discover-featured-book")).Count > 0);

            // Search for element with class 'discover-featured-book' containing the book name
            var cards = driver.FindElements(By.CssSelector(".discover-featured-book"));
            var card = cards.FirstOrDefault(c =>
                c.FindElements(By.XPath($".//*[contains(normalize-space(.), '{title}')]")).Any() ||
                c.Text.Contains(title)
            );

            // If not immediately visible, search using the catalogue search input
            if (card == null)
            {
                var searchInputs = driver.FindElements(By.CssSelector(".discover-search-input input"));
                if (searchInputs.Any())
                {
                    var searchInput = searchInputs.First();
                    searchInput.Clear();
                    searchInput.SendKeys(title);

                    var searchButtons = driver.FindElements(By.CssSelector("button.discover-search-button"));
                    if (searchButtons.Any())
                    {
                        searchButtons.First().Click();
                    }

                    card = wait.Until(d =>
                    {
                        var filteredCards = d.FindElements(By.CssSelector(".discover-featured-book"));
                        return filteredCards.FirstOrDefault(c =>
                            c.FindElements(By.XPath($".//*[contains(normalize-space(.), '{title}')]")).Any() ||
                            c.Text.Contains(title)
                        );
                    });
                }
            }

            // Search for book name, if not found fail
            Assert.True(card != null, $"Book '{title}' was not found in any div with class 'discover-featured-book'.");
            return card!;
        }

        // TC-BOOK-AVAILABILITY-001: Happy - A user can see a book marked available on the Discover page.
        [Fact]
        public void TC_BOOK_AVAILABILITY_001_UserSeesAvailableBookStatus_OnHomePage()
        {
            var title = $"QA Available {Guid.NewGuid():N}";
            CreateBookViaAdminUi(title, 3);

            GoToCatalogue();

            // Search for book name in div with class "discover-featured-book", if not found fail
            var card = FindFeaturedBookCard(title);

            // If found check the word "available" inside the same div with class, if found success
            Assert.Contains(title, card.Text);
            Assert.Contains("available", card.Text, StringComparison.OrdinalIgnoreCase);
        }

        // TC-BOOK-AVAILABILITY-002: Unhappy - A user can see a book marked unavailable on the Discover page.
        [Fact]
        public void TC_BOOK_AVAILABILITY_002_UserSeesUnavailableBookStatus_OnHomePage()
        {
            var title = $"QA Unavailable {Guid.NewGuid():N}";
            CreateBookViaAdminUi(title, 1);
            MarkBookUnavailable(title);

            GoToCatalogue();

            // Search for book name in div with class "discover-featured-book", if not found fail
            var card = FindFeaturedBookCard(title);

            // If found check the word "Not available now" inside the same div with class, if found success
            Assert.Contains(title, card.Text);
            Assert.Contains("Not available now", card.Text);
        }

        public void Dispose()
        {
            driver.Quit();
            driver.Dispose();
        }
    }
}
