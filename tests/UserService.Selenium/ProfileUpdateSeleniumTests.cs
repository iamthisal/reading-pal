using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace UserService.Tests
{
    public class ProfileUpdateSeleniumTests : IDisposable
    {
        private readonly IWebDriver driver;
        private readonly WebDriverWait wait;

        private const string FrontendUrl = "http://localhost:5173";

       
        private const string OriginalEmail =
            "dahamyakulandi21@gmail.com";

        private const string OriginalPassword =
            "Pwd123*";

        private const string OriginalFirstName =
            "Dahamya";

        private const string OriginalLastName =
            "Kulandi";

        // ============================================================
        // Constructor
    
        public ProfileUpdateSeleniumTests()
        {
            var options = new ChromeOptions();

            // Uncomment this if you want Selenium to run without opening Chrome
            // options.AddArgument("--headless");

            options.AddArgument("--start-maximized");

            driver = new ChromeDriver(options);

            wait = new WebDriverWait(
                driver,
                TimeSpan.FromSeconds(15)
            );
        }

        // Helper Method - Wait For Element

        private IWebElement WaitForElement(By locator)
        {
            return wait.Until(d =>
            {
                try
                {
                    var element = d.FindElement(locator);

                    if (element.Displayed)
                    {
                        return element;
                    }

                    return null;
                }
                catch (NoSuchElementException)
                {
                    return null;
                }
                catch (StaleElementReferenceException)
                {
                    return null;
                }
            });
        }

        // ============================================================
        // Helper Method - Login
        // ============================================================

        private void Login()
        {
            // Open login page
            driver.Navigate().GoToUrl(
                $"{FrontendUrl}/login"
            );

            // Wait for login form
            var email = WaitForElement(
                By.Id("email")
            );

            var password = WaitForElement(
                By.Id("password")
            );

            // Enter actual login credentials
            email.Clear();
            email.SendKeys(OriginalEmail);

            password.Clear();
            password.SendKeys(OriginalPassword);

            // Find Login button
            var loginButton = WaitForElement(
                By.CssSelector("form button[type='submit']")
            );

            loginButton.Click();

            // Wait until login finishes
            wait.Until(d =>
                !d.Url.Contains("/login")
            );

            // ========================================================
            // After login, HomePage is displayed.
            // Click "My Profile" instead of directly navigating.
            // ========================================================

            var profileButton = WaitForElement(
                By.CssSelector("a[href='/profile']")
            );

            profileButton.Click();

            // Wait until Profile page opens
            wait.Until(d =>
                d.Url.Contains("/profile")
            );

            // Wait until profile data loads
            WaitForElement(
                By.Id("firstName")
            );
        }

        // ============================================================
        // Helper Method - Get First Name
        // ============================================================

        private string GetFirstName()
        {
            return WaitForElement(
                By.Id("firstName")
            ).GetAttribute("value");
        }

        // ============================================================
        // Helper Method - Get Last Name
        // ============================================================

        private string GetLastName()
        {
            return WaitForElement(
                By.Id("lastName")
            ).GetAttribute("value");
        }

        // ============================================================
        // Helper Method - Get Email
        // ============================================================

        private string GetEmail()
        {
            return WaitForElement(
                By.Id("email")
            ).GetAttribute("value");
        }

        // ============================================================
        // Helper Method - Click Save Changes
        // ============================================================

        private void ClickUpdate()
        {
            var updateButton = WaitForElement(
                By.CssSelector("button[type='submit']")
            );

            updateButton.Click();
        }

        // ============================================================
        // Helper Method - Check Success Message
        // ============================================================

        private bool SuccessMessageDisplayed()
        {
            try
            {
                return wait.Until(d =>
                {
                    var elements = d.FindElements(
                        By.XPath(
                            "//*[contains(text(),'Profile updated successfully!')]"
                        )
                    );

                    return elements.Any(
                        e => e.Displayed
                    );
                });
            }
            catch (WebDriverTimeoutException)
            {
                return false;
            }
        }

        // ============================================================
        // Helper Method - Get Error Message
        // ============================================================

        private string GetErrorMessage()
        {
            try
            {
                return wait.Until(d =>
                {
                    var elements = d.FindElements(
                        By.CssSelector(".error-message")
                    );

                    foreach (var element in elements)
                    {
                        if (element.Displayed &&
                            !string.IsNullOrWhiteSpace(element.Text))
                        {
                            return element.Text;
                        }
                    }

                    return null;
                });
            }
            catch (WebDriverTimeoutException)
            {
                return string.Empty;
            }
        }

        // ============================================================
        // Helper Method - Restore Original Profile
        // ============================================================

        private void RestoreOriginalProfile()
        {
            try
            {
                if (!driver.Url.Contains("/profile"))
                {
                    Login();
                }

                var firstName = WaitForElement(
                    By.Id("firstName")
                );

                var lastName = WaitForElement(
                    By.Id("lastName")
                );

                var email = WaitForElement(
                    By.Id("email")
                );

                firstName.Clear();
                firstName.SendKeys(OriginalFirstName);

                lastName.Clear();
                lastName.SendKeys(OriginalLastName);

                email.Clear();
                email.SendKeys(OriginalEmail);

                // Leave password empty so the current password
                // remains unchanged.

                ClickUpdate();

                wait.Until(d =>
                    d.FindElements(
                        By.XPath(
                            "//*[contains(text(),'Profile updated successfully!')]"
                        )
                    ).Any()
                );
            }
            catch
            {
                // Ignore cleanup errors.
            }
        }

        // ============================================================
        // SEL-PROFILE-001
        // View Profile With Valid Login
        // ============================================================

        [Fact]
        public void ViewProfile_WithValidLogin_DisplaysUserDetails()
        {
            // Arrange
            Login();

            // Act
            var firstName = WaitForElement(
                By.Id("firstName")
            );

            var lastName = WaitForElement(
                By.Id("lastName")
            );

            var email = WaitForElement(
                By.Id("email")
            );

            // Assert
            Assert.Equal(
                OriginalFirstName,
                firstName.GetAttribute("value")
            );

            Assert.Equal(
                OriginalLastName,
                lastName.GetAttribute("value")
            );

            Assert.Equal(
                OriginalEmail,
                email.GetAttribute("value")
            );
        }

        // ============================================================
        // SEL-PROFILE-002
        // Update Profile With Valid Details
        // ============================================================

        [Fact]
        public void UpdateProfile_WithValidDetails_UpdatesProfile()
        {
            try
            {
                // Arrange
                Login();

                var firstName = WaitForElement(
                    By.Id("firstName")
                );

                var lastName = WaitForElement(
                    By.Id("lastName")
                );

                var email = WaitForElement(
                    By.Id("email")
                );

                // Act
                firstName.Clear();
                firstName.SendKeys("DahamyaUpdated");

                lastName.Clear();
                lastName.SendKeys("KulandiUpdated");

                email.Clear();
                email.SendKeys("dahamyaupdated@gmail.com");

                ClickUpdate();

                // Assert
                Assert.True(
                    SuccessMessageDisplayed(),
                    "Profile success message was not displayed."
                );

                Assert.Equal(
                    "DahamyaUpdated",
                    GetFirstName()
                );

                Assert.Equal(
                    "KulandiUpdated",
                    GetLastName()
                );

                Assert.Equal(
                    "dahamyaupdated@gmail.com",
                    GetEmail()
                );
            }
            finally
            {
                RestoreOriginalProfile();
            }
        }

        // ============================================================
        // SEL-PROFILE-003
        // Invalid Email
        // ============================================================

        [Fact]
        public void UpdateProfile_WithInvalidEmail_DisplaysValidationError()
        {
            // Arrange
            Login();

            var email = WaitForElement(
                By.Id("email")
            );

            // Act
            email.Clear();
            email.SendKeys("dahamya12");

            // Check HTML5 email validation
            bool isValid = (bool)(
                ((IJavaScriptExecutor)driver).ExecuteScript(
                    "return arguments[0].checkValidity();",
                    email
                )
            );

            // Assert
            Assert.False(
                isValid,
                "Invalid email should fail HTML5 validation."
            );
        }

        // ============================================================
        // SEL-PROFILE-004
        // Missing Required Fields
        // ============================================================

        [Fact]
        public void UpdateProfile_WithMissingRequiredFields_DisplaysValidationError()
        {
            // Arrange
            Login();

            var firstName = WaitForElement(
                By.Id("firstName")
            );

            // Act
            firstName.Clear();

            bool isValid = (bool)(
                ((IJavaScriptExecutor)driver).ExecuteScript(
                    "return arguments[0].checkValidity();",
                    firstName
                )
            );

            // Assert
            Assert.False(
                isValid,
                "Empty first name should fail required-field validation."
            );
        }

        // ============================================================
        // SEL-PROFILE-005
        // Short Password
        // ============================================================

        [Fact]
        public void UpdateProfile_WithShortPassword_DisplaysValidationError()
        {
            // Arrange
            Login();

            var password = WaitForElement(
                By.Id("password")
            );

            // Act
            password.Clear();
            password.SendKeys("123");

            bool isValid = (bool)(
                ((IJavaScriptExecutor)driver).ExecuteScript(
                    "return arguments[0].checkValidity();",
                    password
                )
            );

            // Assert
            Assert.False(
                isValid,
                "Password shorter than 6 characters should fail validation."
            );
        }

        // ============================================================
        // SEL-PROFILE-006
        // Update Without Changing Password
        // ============================================================

        [Fact]
        public void UpdateProfile_WithoutPassword_UpdatesOtherDetails()
        {
            try
            {
                // Arrange
                Login();

                var firstName = WaitForElement(
                    By.Id("firstName")
                );

                var lastName = WaitForElement(
                    By.Id("lastName")
                );

                // Act
                firstName.Clear();
                firstName.SendKeys("Dahamya");

                lastName.Clear();
                lastName.SendKeys("Updated");

                // Password intentionally left empty.
                ClickUpdate();

                // Assert
                Assert.True(
                    SuccessMessageDisplayed(),
                    "Profile should update successfully without changing password."
                );
            }
            finally
            {
                RestoreOriginalProfile();
            }
        }

        // ============================================================
        // SEL-PROFILE-007
        // Update With Valid Password
        // ============================================================

        [Fact]
        public void UpdateProfile_WithValidPassword_UpdatesSuccessfully()
        {
            try
            {
                // Arrange
                Login();

                var password = WaitForElement(
                    By.Id("password")
                );

                // Act
                password.Clear();
                password.SendKeys("NewPwd123*");

                ClickUpdate();

                // Assert
                Assert.True(
                    SuccessMessageDisplayed(),
                    "Profile should update successfully with a valid password."
                );
            }
            finally
            {
                // Restore the original password.
                try
                {
                    var password = WaitForElement(
                        By.Id("password")
                    );

                    password.Clear();
                    password.SendKeys(OriginalPassword);

                    ClickUpdate();

                    wait.Until(d =>
                        d.FindElements(
                            By.XPath(
                                "//*[contains(text(),'Profile updated successfully!')]"
                            )
                        ).Any()
                    );
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }
        }

        // ============================================================
        // SEL-PROFILE-008
        // Existing User Email
        // ============================================================

        [Fact]
        public void UpdateProfile_WithExistingEmail_DisplaysError()
        {
            // Arrange
            Login();

            var email = WaitForElement(
                By.Id("email")
            );

            // Act
            email.Clear();
            email.SendKeys("lithu12@gmail.com");

            ClickUpdate();

            // Assert
            var errorMessage = GetErrorMessage();

            Assert.False(
                string.IsNullOrWhiteSpace(errorMessage),
                "An error message should be displayed."
            );

            Assert.Contains(
                "email",
                errorMessage.ToLower()
            );
        }

        // ============================================================
        // SEL-PROFILE-009
        // Access Profile Without Authentication
        // ============================================================

        [Fact]
        public void Profile_WithoutLogin_RedirectsToLogin()
        {
            // Arrange
            driver.Navigate().GoToUrl(
                $"{FrontendUrl}/profile"
            );

            // Act
            wait.Until(d =>
                d.Url.Contains("/login")
            );

            // Assert
            Assert.Contains(
                "/login",
                driver.Url.ToLower()
            );
        }

        // ============================================================
        // SEL-PROFILE-010
        // Verify Authenticated User Profile
        // ============================================================

        [Fact]
        public void Profile_AfterLogin_DisplaysAuthenticatedUser()
        {
            // Arrange
            Login();

            // Act
            var email = WaitForElement(
                By.Id("email")
            );

            // Assert
            Assert.Equal(
                OriginalEmail,
                email.GetAttribute("value")
            );

            Assert.NotEqual(
                "lithu12@gmail.com",
                email.GetAttribute("value")
            );
        }

        // ============================================================
        // Dispose Selenium WebDriver
        // ============================================================

        public void Dispose()
        {
            try
            {
                driver.Quit();
            }
            catch
            {
                // Ignore browser cleanup errors.
            }

            driver.Dispose();
        }
    }
}