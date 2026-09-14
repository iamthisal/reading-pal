using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace InventoryService.SeleniumTests
{
    public class AdminBookRecord
    {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
    }

    public class BookSearchSelenium : IDisposable
    {
        private readonly IWebDriver driver;
        private readonly WebDriverWait wait;

        private const string FrontendUrl = "http://localhost:5173";
        private const string AdminEmail = "admin@library.com";
        private const string AdminPassword = "adminpassword";
        private const string UserEmail = "dilum@icloud.com";
        private const string UserPassword = "0768005064";

        public BookSearchSelenium()
        {
            var options = WebDriverHelper.CreateOptions();
            driver = new ChromeDriver(options);
            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
        }

        #region Helpers

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
            wait.Until(d => d.PageSource.Contains("Discover") || d.FindElements(By.CssSelector(".discover-featured-book")).Count > 0);
        }

        private void OpenAdminBooksPage()
        {
            var bookInventoryLink = wait.Until(d =>
                d.FindElement(By.XPath("//a[contains(@href, '/admin/books') or .//h3[contains(normalize-space(.), 'Book Inventory')]]"))
            );
            bookInventoryLink.Click();

            wait.Until(d =>
                d.Url.Contains("/admin/books") ||
                d.PageSource.Contains("Book Inventory Management")
            );
        }

        private void Logout()
        {
            try
            {
                if (driver.Url.Contains("/admin/books"))
                {
                    var dashboardLink = driver.FindElements(By.XPath("//a[contains(@href, '/admin/dashboard') or contains(., 'Dashboard')]")).FirstOrDefault();
                    dashboardLink?.Click();
                    wait.Until(d => d.Url.Contains("/admin/dashboard"));
                }

                if (driver.Url.Contains("/admin/dashboard"))
                {
                    var signOutBtn = driver.FindElements(By.XPath("//button[contains(., 'Sign Out') or contains(@class, 'btn-outline')]")).FirstOrDefault();
                    signOutBtn?.Click();
                    wait.Until(d => d.Url.Contains("/login"));
                    return;
                }
                else if (driver.Url.Contains("/home"))
                {
                    var logoutBtn = driver.FindElements(By.CssSelector("button.discover-nav-button")).FirstOrDefault();
                    logoutBtn?.Click();
                    wait.Until(d => d.Url.Contains("/login"));
                    return;
                }
            }
            catch
            {
                // Fallback to script clearing if direct button transition fails
            }

            driver.Navigate().GoToUrl($"{FrontendUrl}/login");
            ((IJavaScriptExecutor)driver).ExecuteScript("localStorage.clear();");
            driver.Navigate().Refresh();
            wait.Until(d => d.Url.Contains("/login") || d.PageSource.Contains("Sign In"));
        }

        private void CreateBookViaAdminUi(string title, string author, string genre, int copies)
        {
            var isbn = $"QA-{Guid.NewGuid():N}".Substring(0, 18);

            var titleInput = wait.Until(d => d.FindElement(By.Id("book-title")));
            titleInput.Clear();
            titleInput.SendKeys(title);

            driver.FindElement(By.Id("book-author")).Clear();
            driver.FindElement(By.Id("book-author")).SendKeys(author);

            driver.FindElement(By.Id("book-isbn")).Clear();
            driver.FindElement(By.Id("book-isbn")).SendKeys(isbn);

            driver.FindElement(By.Id("book-genre")).Clear();
            driver.FindElement(By.Id("book-genre")).SendKeys(genre);

            var copiesInput = driver.FindElement(By.Id("book-copies"));
            copiesInput.Clear();
            copiesInput.SendKeys(copies.ToString());

            driver.FindElement(By.CssSelector("button[type='submit']")).Click();
            wait.Until(d => d.PageSource.Contains("Successfully added") || d.PageSource.Contains(title));
        }

        private List<AdminBookRecord> GetAdminInventoryBooks(int minimumNeeded = 5)
        {
            LoginAsAdmin();
            OpenAdminBooksPage();

            wait.Until(d => d.FindElements(By.CssSelector("table tbody tr")).Count > 0);
            var rows = driver.FindElements(By.CssSelector("table tbody tr"));

            var records = new List<AdminBookRecord>();
            foreach (var row in rows)
            {
                var cells = row.FindElements(By.TagName("td"));
                if (cells.Count >= 7)
                {
                    var title = cells[2].Text.Trim();
                    var author = cells[3].Text.Trim();
                    var genre = cells[5].Text.Trim();

                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(author) && !string.IsNullOrEmpty(genre))
                    {
                        if (!records.Any(r => r.Title.Equals(title, StringComparison.OrdinalIgnoreCase)))
                        {
                            records.Add(new AdminBookRecord
                            {
                                Title = title,
                                Author = author,
                                Genre = genre
                            });
                        }
                    }
                }
            }

            // Ensure test environment always has sufficient books to sample from
            while (records.Count < minimumNeeded)
            {
                var newTitle = $"QA Search {Guid.NewGuid():N}".Substring(0, 16);
                var newAuthor = $"QA Author {records.Count + 1}";
                var newGenre = records.Count % 2 == 0 ? "Fiction" : "Technology";
                CreateBookViaAdminUi(newTitle, newAuthor, newGenre, 3);
                records.Add(new AdminBookRecord { Title = newTitle, Author = newAuthor, Genre = newGenre });
            }

            return records;
        }

        private void PerformSearch(string query)
        {
            wait.Until(d => d.FindElements(By.CssSelector(".discover-search-input input")).Count > 0);
            var searchInput = driver.FindElement(By.CssSelector(".discover-search-input input"));

            searchInput.Click();
            searchInput.Clear();
            searchInput.SendKeys(Keys.Control + "a");
            searchInput.SendKeys(Keys.Backspace);

            // Update React controlled state via JavaScript input event
            ((IJavaScriptExecutor)driver).ExecuteScript(@"
                var input = arguments[0];
                var val = arguments[1];
                var nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                nativeInputValueSetter.call(input, val);
                input.dispatchEvent(new Event('input', { bubbles: true }));
            ", searchInput, query);

            var searchBtn = driver.FindElements(By.CssSelector("button.discover-search-button")).FirstOrDefault();
            searchBtn?.Click();

            Thread.Sleep(300);
        }

        private bool IsBookInSearchResults(string title)
        {
            // Featured book cards: <h3> contains title
            var featuredCards = driver.FindElements(By.CssSelector(".discover-featured-book"));
            foreach (var card in featuredCards)
            {
                if (card.Text.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            // Category book cards: <p> contains title
            var categoryCards = driver.FindElements(By.CssSelector(".discover-category-book"));
            foreach (var card in categoryCards)
            {
                if (card.Text.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            // Fallback: check within catalogue main container
            var main = driver.FindElements(By.CssSelector("main#discover")).FirstOrDefault();
            if (main != null && main.Text.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private static string GetPartialString(string text)
        {
            text = text.Trim();
            var words = text.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1)
            {
                var goodWord = words.FirstOrDefault(w => w.Length >= 3);
                if (goodWord != null) return goodWord;
            }

            if (text.Length > 4)
            {
                return text.Substring(0, text.Length / 2 + 1);
            }
            return text;
        }

        private static string RandomizeCase(string input)
        {
            var chars = input.ToCharArray();
            var rand = new Random();
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsLetter(chars[i]))
                {
                    chars[i] = rand.Next(2) == 0 ? char.ToUpper(chars[i]) : char.ToLower(chars[i]);
                }
            }
            var result = new string(chars);
            if (result.Equals(input, StringComparison.Ordinal) && input.Any(char.IsLetter))
            {
                return input.ToUpperInvariant();
            }
            return result;
        }

        #endregion

        #region Tests

        // Test 1: Login as admin, get 5 books, login as user, search each book by full title, confirm all show up
        [Fact]
        public void TC_SEARCH_001_AdminExtractsBooks_UserSearchesFullTitle_AllFound()
        {
            var books = GetAdminInventoryBooks(5);
            var sample = books.OrderBy(_ => Guid.NewGuid()).Take(5).ToList();

            Logout();
            LoginAsUser();

            foreach (var book in sample)
            {
                PerformSearch(book.Title);
                bool found = IsBookInSearchResults(book.Title);
                Assert.True(found, $"Book '{book.Title}' was not found in results when searching by its full title.");
            }
        }

        // Test 2: Login as admin, get book titles, login as user, search with partial titles and check results
        [Fact]
        public void TC_SEARCH_002_UserSearchesPartialTitle_MatchingBooksFound()
        {
            var books = GetAdminInventoryBooks(5);
            var sample = books.OrderBy(_ => Guid.NewGuid()).Take(3).ToList();

            Logout();
            LoginAsUser();

            foreach (var book in sample)
            {
                string partialTitle = GetPartialString(book.Title);
                PerformSearch(partialTitle);
                bool found = IsBookInSearchResults(book.Title);
                Assert.True(found, $"Book '{book.Title}' was not found in results when searching by partial title '{partialTitle}'.");
            }
        }

        // Test 3: Full title search with randomized letter casing (case-insensitivity check)
        [Fact]
        public void TC_SEARCH_003_UserSearchesFullTitle_RandomCase_Insensitive()
        {
            var books = GetAdminInventoryBooks(5);
            var sample = books.OrderBy(_ => Guid.NewGuid()).Take(3).ToList();

            Logout();
            LoginAsUser();

            foreach (var book in sample)
            {
                string randomCasedTitle = RandomizeCase(book.Title);
                PerformSearch(randomCasedTitle);
                bool found = IsBookInSearchResults(book.Title);
                Assert.True(found, $"Book '{book.Title}' was not found in results when searching with randomized case query '{randomCasedTitle}'.");
            }
        }

        // Test 4: Partial title search with randomized letter casing (case-insensitivity check)
        [Fact]
        public void TC_SEARCH_004_UserSearchesPartialTitle_RandomCase_Insensitive()
        {
            var books = GetAdminInventoryBooks(5);
            var sample = books.OrderBy(_ => Guid.NewGuid()).Take(3).ToList();

            Logout();
            LoginAsUser();

            foreach (var book in sample)
            {
                string partialTitle = GetPartialString(book.Title);
                string randomCasedPartial = RandomizeCase(partialTitle);
                PerformSearch(randomCasedPartial);
                bool found = IsBookInSearchResults(book.Title);
                Assert.True(found, $"Book '{book.Title}' was not found in results when searching with randomized partial query '{randomCasedPartial}'.");
            }
        }

        // Test 5: Login as admin, get 3 authors, login as user, search for each author and confirm their books show up
        [Fact]
        public void TC_SEARCH_005_UserSearchesFullAuthor_BooksFound()
        {
            var books = GetAdminInventoryBooks(5);
            var authorGroups = books.GroupBy(b => b.Author).Take(3).ToList();

            Logout();
            LoginAsUser();

            foreach (var authorGroup in authorGroups)
            {
                string authorName = authorGroup.Key;
                PerformSearch(authorName);

                bool anyBookFound = authorGroup.Any(b => IsBookInSearchResults(b.Title));
                Assert.True(anyBookFound, $"Expected books written by author '{authorName}' to show up in search results.");
            }
        }

        // Test 6: Search with partial author name and confirm related books show up
        [Fact]
        public void TC_SEARCH_006_UserSearchesPartialAuthor_BooksFound()
        {
            var books = GetAdminInventoryBooks(5);
            var authorGroups = books.GroupBy(b => b.Author).Take(3).ToList();

            Logout();
            LoginAsUser();

            foreach (var authorGroup in authorGroups)
            {
                string authorName = authorGroup.Key;
                string partialAuthor = GetPartialString(authorName);
                PerformSearch(partialAuthor);

                bool anyBookFound = authorGroup.Any(b => IsBookInSearchResults(b.Title));
                Assert.True(anyBookFound, $"Expected books written by author '{authorName}' to show up when searching partial author '{partialAuthor}'.");
            }
        }

        // Test 7: Full genre (category) name search and confirm related books show up
        [Fact]
        public void TC_SEARCH_007_UserSearchesFullGenre_BooksFound()
        {
            var books = GetAdminInventoryBooks(5);
            var genreGroups = books.GroupBy(b => b.Genre).Take(2).ToList();

            Logout();
            LoginAsUser();

            foreach (var genreGroup in genreGroups)
            {
                string genre = genreGroup.Key;
                PerformSearch(genre);

                bool anyBookFound = genreGroup.Any(b => IsBookInSearchResults(b.Title));
                Assert.True(anyBookFound, $"Expected books with genre '{genre}' to show up when searching by genre name.");
            }
        }

        // Test 8: Partial genre (category) name search and confirm related books show up
        [Fact]
        public void TC_SEARCH_008_UserSearchesPartialGenre_BooksFound()
        {
            var books = GetAdminInventoryBooks(5);
            var genreGroups = books.GroupBy(b => b.Genre).Take(2).ToList();

            Logout();
            LoginAsUser();

            foreach (var genreGroup in genreGroups)
            {
                string genre = genreGroup.Key;
                string partialGenre = GetPartialString(genre);
                PerformSearch(partialGenre);

                bool anyBookFound = genreGroup.Any(b => IsBookInSearchResults(b.Title));
                Assert.True(anyBookFound, $"Expected books with genre '{genre}' to show up when searching by partial genre '{partialGenre}'.");
            }
        }

        // Test 9: Combined run - search title, author, and category individually; verify title, author, and category metadata show in result cards
        [Fact]
        public void TC_SEARCH_009_SingleRun_SearchTitleAuthorCategory_VerifyCardsDisplayMetadata()
        {
            var books = GetAdminInventoryBooks(5);
            var targetBook = books.First();

            Logout();
            LoginAsUser();

            // 1. Search by book title and verify book card contains title
            PerformSearch(targetBook.Title);
            Assert.True(IsBookInSearchResults(targetBook.Title), $"Search by title '{targetBook.Title}' failed to show target book.");

            // 2. Search by author name and verify book card contains author or shows the book
            PerformSearch(targetBook.Author);
            Assert.True(IsBookInSearchResults(targetBook.Title), $"Search by author '{targetBook.Author}' failed to show target book '{targetBook.Title}'.");

            // Verify author text is displayed in featured cards if present
            var featuredCards = driver.FindElements(By.CssSelector(".discover-featured-book"));
            var matchingFeatured = featuredCards.FirstOrDefault(c => c.Text.IndexOf(targetBook.Title, StringComparison.OrdinalIgnoreCase) >= 0);
            if (matchingFeatured != null)
            {
                Assert.Contains(targetBook.Author, matchingFeatured.Text, StringComparison.OrdinalIgnoreCase);
            }

            // 3. Search by genre/category and verify category tag displays
            PerformSearch(targetBook.Genre);
            Assert.True(IsBookInSearchResults(targetBook.Title), $"Search by category '{targetBook.Genre}' failed to show target book '{targetBook.Title}'.");

            // Verify category/genre tag is displayed in category grid cards if present
            var categoryCards = driver.FindElements(By.CssSelector(".discover-category-book"));
            var matchingCategory = categoryCards.FirstOrDefault(c => c.Text.IndexOf(targetBook.Title, StringComparison.OrdinalIgnoreCase) >= 0);
            if (matchingCategory != null)
            {
                Assert.Contains(targetBook.Genre, matchingCategory.Text, StringComparison.OrdinalIgnoreCase);
            }
        }

        #endregion

        public void Dispose()
        {
            driver.Quit();
            driver.Dispose();
        }
    }
}
