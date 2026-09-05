using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Xunit;

namespace UserService.SeleniumTests
{
    public class LoginTests
    {
        private IWebDriver CreateDriver()
        {
            var options = new ChromeOptions();

            options.AddArgument("--start-maximized");

            return new ChromeDriver(options);
        }

        // TC-LOGIN-001 - Valid login
        // Expected: User successfully reaches the dashboard
        [Fact]
        public void Login_WithValidCredentials_ReachesDashboard()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/login");

            driver.FindElement(By.Id("email"))
                .SendKeys("dahamyakulandi21@gmail.com");

            driver.FindElement(By.Id("password"))
                .SendKeys("Pwd123*");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            Thread.Sleep(2000);

            Assert.Contains("/home", driver.Url);
        }


        // TC-LOGIN-002 - Wrong password
        // Expected: Login rejected and error message displayed
        [Fact]
        public void Login_WithWrongPassword_ShowsError()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/login");

            driver.FindElement(By.Id("email"))
                .SendKeys("dahamyakulandi21@gmail.com");

            driver.FindElement(By.Id("password"))
                .SendKeys("123456");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            Thread.Sleep(1000);

            var error = driver.FindElement(By.ClassName("error-message"));

            Assert.Contains("Invalid email or password", error.Text);
        }


        // TC-LOGIN-003 - Non-existent email
        // Expected: Login rejected and error message displayed
        [Fact]
        public void Login_WithNonExistentEmail_ShowsError()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/login");

            driver.FindElement(By.Id("email"))
                .SendKeys("hello@gmail.com");

            driver.FindElement(By.Id("password"))
                .SendKeys("Password123");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            Thread.Sleep(1000);

            var error = driver.FindElement(By.ClassName("error-message"));

            Assert.Contains("Invalid email or password", error.Text);
        }


        // TC-LOGIN-004 - Empty credentials
        // Expected: Validation prevents login
        [Fact]
        public void Login_WithEmptyCredentials_DoesNotSubmit()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/login");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            Thread.Sleep(500);

            Assert.Contains("/login", driver.Url);
        }
    }
}