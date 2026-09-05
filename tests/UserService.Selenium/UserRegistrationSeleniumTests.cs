using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Chrome;
using Xunit;

namespace UserService.SeleniumTests
{
    public class RegistrationSeleniumTests
    {
        private IWebDriver CreateDriver()
        {
            var options = new ChromeOptions();

            return new ChromeDriver(options);
        }

         // TC_REG_001 - Successful Registration
        [Fact]
        public void TC_REG_001_SuccessfulRegistration()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/register");

            driver.FindElement(By.Id("firstName"))
                .SendKeys("Dahamya");

            driver.FindElement(By.Id("lastName"))
                .SendKeys("Wickramasinghe");

            driver.FindElement(By.Id("email"))
                .SendKeys("gkdkwick@gmail.com");

            driver.FindElement(By.Id("password"))
                .SendKeys("Password123*");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));

            var successMessage =
                wait.Until(d => d.FindElement(By.ClassName("error-message")));

            Assert.Contains(
                "Registration successful",
                successMessage.Text
            );
        }

        // TC_REG_002 - Duplicate Email
        [Fact]
        public void TC_REG_002_DuplicateEmail()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/register");

            driver.FindElement(By.Id("firstName"))
                .SendKeys("Dahamya");

            driver.FindElement(By.Id("lastName"))
                .SendKeys("Wickramasinghe");

            driver.FindElement(By.Id("email"))
                .SendKeys("dahamwicky@gmail.com");

            driver.FindElement(By.Id("password"))
                .SendKeys("Password123*");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));

            var errorMessage =
                wait.Until(d => d.FindElement(By.ClassName("error-message")));

            Assert.NotEmpty(errorMessage.Text);
        }


        // TC_REG_003 - Invalid Email
        [Fact]
        public void TC_REG_003_InvalidEmail()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/register");

            driver.FindElement(By.Id("firstName"))
                .SendKeys("Dahamya");

            driver.FindElement(By.Id("lastName"))
                .SendKeys("Wickramasinghe");

            driver.FindElement(By.Id("email"))
                .SendKeys("daham.com");

            driver.FindElement(By.Id("password"))
                .SendKeys("Password123*");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            var emailField =
                driver.FindElement(By.Id("email"));

            Assert.NotEmpty(
                emailField.GetAttribute("validationMessage")
            );
        }


        // TC_REG_004 - Empty First Name
        [Fact]
        public void TC_REG_004_EmptyFirstName()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/register");

            driver.FindElement(By.Id("lastName"))
                .SendKeys("Wickramasinghe");

            driver.FindElement(By.Id("email"))
                .SendKeys("dahamya@gmail.com");

            driver.FindElement(By.Id("password"))
                .SendKeys("Password123*");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            var firstNameField =
                driver.FindElement(By.Id("firstName"));

            Assert.NotEmpty(
                firstNameField.GetAttribute("validationMessage")
            );
        }


        // TC_REG_005 - Empty Last Name
        [Fact]
        public void TC_REG_005_EmptyLastName()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/register");

            driver.FindElement(By.Id("firstName"))
                .SendKeys("Dahamya");

            driver.FindElement(By.Id("email"))
                .SendKeys("dahamya@gmail.com");

            driver.FindElement(By.Id("password"))
                .SendKeys("Password123*");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            var lastNameField =
                driver.FindElement(By.Id("lastName"));

            Assert.NotEmpty(
                lastNameField.GetAttribute("validationMessage")
            );
        }


        // TC_REG_006 - Empty Email
        [Fact]
        public void TC_REG_006_EmptyEmail()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/register");

            driver.FindElement(By.Id("firstName"))
                .SendKeys("Dahamya");

            driver.FindElement(By.Id("lastName"))
                .SendKeys("Wickramasinghe");

            driver.FindElement(By.Id("password"))
                .SendKeys("Password123*");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            var emailField =
                driver.FindElement(By.Id("email"));

            Assert.NotEmpty(
                emailField.GetAttribute("validationMessage")
            );
        }


        // TC_REG_007 - Empty Password
        [Fact]
        public void TC_REG_007_EmptyPassword()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/register");

            driver.FindElement(By.Id("firstName"))
                .SendKeys("Dahamya");

            driver.FindElement(By.Id("lastName"))
                .SendKeys("Wickramasinghe");

            driver.FindElement(By.Id("email"))
                .SendKeys("dahamya@gmail.com");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            var passwordField =
                driver.FindElement(By.Id("password"));

            Assert.NotEmpty(
                passwordField.GetAttribute("validationMessage")
            );
        }


        // TC_REG_008 - Password Less Than 6 Characters
        [Fact]
        public void TC_REG_008_ShortPassword()
        {
            using var driver = CreateDriver();

            driver.Navigate().GoToUrl("http://localhost:5173/register");

            driver.FindElement(By.Id("firstName"))
                .SendKeys("Dahamya");

            driver.FindElement(By.Id("lastName"))
                .SendKeys("Wickramasinghe");

            driver.FindElement(By.Id("email"))
                .SendKeys("dahamya@gmail.com");

            driver.FindElement(By.Id("password"))
                .SendKeys("12345");

            driver.FindElement(By.CssSelector("button[type='submit']"))
                .Click();

            var passwordField =
                driver.FindElement(By.Id("password"));

            Assert.NotEmpty(
                passwordField.GetAttribute("validationMessage")
            );
        }
    }
}

