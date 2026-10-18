using System.Drawing;
using System.Runtime.InteropServices;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace ReadingPal.E2E.Support;

/// <summary>
/// One Chrome window plus the helpers every test uses. A test can open several (two admins, or a
/// member and an admin side by side).
/// </summary>
public sealed class Browser : IDisposable
{
    public IWebDriver Driver { get; }
    public WebDriverWait Wait { get; }

    public Browser(int width = 1440, int height = 900)
    {
        var options = new ChromeOptions();
        if (E2EConfig.Headless) options.AddArgument("--headless=new");
        options.AddArguments($"--window-size={width},{height}", "--disable-search-engine-choice-screen", "--no-first-run",
            "--disable-features=PasswordLeakDetection,AutofillServerCommunication", "--password-store=basic");
        options.AddUserProfilePreference("credentials_enable_service", false);
        options.AddUserProfilePreference("profile.password_manager_enabled", false);
        options.SetLoggingPreference(LogType.Browser, LogLevel.All);
        Driver = new ChromeDriver(options);
        Driver.Manage().Window.Size = new Size(width, height);
        Wait = new WebDriverWait(Driver, E2EConfig.UiTimeout) { PollingInterval = TimeSpan.FromMilliseconds(200) };
        Wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
    }

    // ---------- Navigation ----------

    public void Go(string path) => Driver.Navigate().GoToUrl(E2EConfig.FrontendUrl + path);

    public string Path => new Uri(Driver.Url).AbsolutePath;
    public string Query => new Uri(Driver.Url).Query;

    public void WaitForPath(string path, TimeSpan? timeout = null) =>
        Until(() => Path == path, timeout, $"the page to reach {path} (it is at {Path})");

    public void Reload() => Driver.Navigate().Refresh();

    // ---------- Signing in ----------

    /// <summary>Signs in by storing a token, as the login page does; much faster than typing.</summary>
    public void SignInWithToken(string token, string path = "/home")
    {
        if (!Driver.Url.StartsWith(E2EConfig.FrontendUrl, StringComparison.Ordinal)) Go("/login");
        Js("window.localStorage.setItem('token', arguments[0]);", token);
        Go(path);
    }

    public void SignInAs(Member member, string path = "/home") => SignInWithToken(member.Token, path);

    public void SignInAsAdmin(string token, string path = "/admin/dashboard") => SignInWithToken(token, path);

    /// <summary>Signs in through the form, the way a person does.</summary>
    public void SignInThroughForm(string email, string password)
    {
        Go("/login");
        Type(By.Id("email"), email);
        Type(By.Id("password"), password);
        ClickButton("Sign in");
    }

    public void SignOut()
    {
        Click(By.CssSelector("button[aria-label='Account menu']"));
        Click(By.XPath("//*[@role='menuitem'][normalize-space(.)='Log out']"));
    }

    public string? StoredToken => (string?)Js("return window.localStorage.getItem('token');");

    // ---------- Finding and waiting ----------

    public IWebElement Find(By by) => Wait.Until(driver => driver.FindElement(by));

    public IWebElement Visible(By by, TimeSpan? timeout = null)
    {
        IWebElement? found = null;
        Until(() =>
        {
            found = Driver.FindElements(by).FirstOrDefault(element => element.Displayed);
            return found != null;
        }, timeout, $"{by} to be visible");
        return found!;
    }

    public bool Exists(By by) => Driver.FindElements(by).Any(element => element.Displayed);

    public void Until(Func<bool> condition, TimeSpan? timeout = null, string? what = null)
    {
        var wait = new WebDriverWait(Driver, timeout ?? E2EConfig.UiTimeout) { PollingInterval = TimeSpan.FromMilliseconds(250) };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
        try { wait.Until(_ => condition()); }
        catch (WebDriverTimeoutException)
        {
            throw new WebDriverTimeoutException($"Timed out waiting for {what ?? "a condition"}. Page: {Driver.Url}\nVisible text: {PageSummary()}");
        }
    }

    /// <summary>Keeps reloading until the condition holds; for results that arrive through Kafka.</summary>
    public void UntilAfterReloads(Func<bool> condition, TimeSpan? timeout = null, string? what = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? E2EConfig.EventTimeout);
        while (DateTime.UtcNow < deadline)
        {
            try { if (condition()) return; } catch (WebDriverException) { }
            Thread.Sleep(2000);
            Reload();
            Thread.Sleep(1000);
        }
        throw new WebDriverTimeoutException($"Timed out waiting for {what ?? "a condition"} after reloading. Page: {Driver.Url}");
    }

    private string PageSummary()
    {
        try
        {
            var text = BodyText.Replace('\n', ' ');
            return text.Length > 600 ? text[..600] + "…" : text;
        }
        catch (WebDriverException) { return "(unavailable: a browser dialog is open)"; }
    }

    public string BodyText => Driver.FindElement(By.TagName("body")).Text;

    public void WaitForText(string text, TimeSpan? timeout = null) =>
        Until(() => BodyText.Contains(text, StringComparison.Ordinal), timeout, $"the text \"{text}\"");

    public bool HasText(string text) => BodyText.Contains(text, StringComparison.Ordinal);

    public void WaitForNoText(string text, TimeSpan? timeout = null) =>
        Until(() => !BodyText.Contains(text, StringComparison.Ordinal), timeout, $"\"{text}\" to disappear");

    public static By ButtonByText(string text) =>
        By.XPath($"//button[normalize-space(.)={XPathLiteral(text)}] | //a[normalize-space(.)={XPathLiteral(text)}]");

    public static By ByAria(string label) => By.CssSelector($"[aria-label={CssLiteral(label)}]");

    public static By RowContaining(string text) => By.XPath($"//tr[.//*[contains(normalize-space(.), {XPathLiteral(text)})]]");

    // ---------- Acting ----------

    public void Click(By by)
    {
        var element = Visible(by);
        ScrollIntoView(element);
        try { element.Click(); }
        catch (ElementClickInterceptedException) { Js("arguments[0].click();", element); }
    }

    public void Click(IWebElement element)
    {
        ScrollIntoView(element);
        try { element.Click(); }
        catch (ElementClickInterceptedException) { Js("arguments[0].click();", element); }
    }

    public void ClickButton(string text) => Click(ButtonByText(text));

    /// <summary>Clears an input the way a person would (select all, delete) so React sees the change, then types.</summary>
    public void Type(By by, string text)
    {
        var element = Visible(by);
        Type(element, text);
    }

    public void Type(IWebElement element, string text)
    {
        ScrollIntoView(element);
        element.Click();
        var modifier = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Keys.Command : Keys.Control;
        element.SendKeys(modifier + "a");
        element.SendKeys(Keys.Delete);
        if (text.Length > 0) element.SendKeys(text);
    }

    public void Select(By by, string visibleText) => new SelectElement(Visible(by)).SelectByText(visibleText);

    public void SelectValue(By by, string value) => new SelectElement(Visible(by)).SelectByValue(value);

    public void ScrollIntoView(IWebElement element) => Js("arguments[0].scrollIntoView({block: 'center'});", element);

    public object? Js(string script, params object[] args) => ((IJavaScriptExecutor)Driver).ExecuteScript(script, args);

    // ---------- Native dialogs (window.confirm / alert) ----------

    public IAlert WaitForAlert(TimeSpan? timeout = null)
    {
        IAlert? alert = null;
        Until(() =>
        {
            try { alert = Driver.SwitchTo().Alert(); return true; }
            catch (NoAlertPresentException) { return false; }
        }, timeout, "a browser dialog");
        return alert!;
    }

    public string AcceptAlert()
    {
        var alert = WaitForAlert();
        var text = alert.Text;
        alert.Accept();
        return text;
    }

    public string DismissAlert()
    {
        var alert = WaitForAlert();
        var text = alert.Text;
        alert.Dismiss();
        return text;
    }

    // ---------- Page facts ----------

    /// <summary>The message the browser would show for an invalid field; empty when the field is valid.</summary>
    public string ValidationMessage(By by) => (string?)Js("return arguments[0].validationMessage;", Find(by)) ?? string.Empty;

    public bool IsValid(By by) => (bool)(Js("return arguments[0].checkValidity();", Find(by)) ?? false);

    public IWebElement? OpenDialog => Driver.FindElements(By.CssSelector("dialog[open]")).FirstOrDefault();

    public IReadOnlyList<string> SevereConsoleErrors() =>
        Driver.Manage().Logs.GetLog(LogType.Browser)
            .Where(entry => entry.Level == LogLevel.Severe)
            .Select(entry => entry.Message)
            .ToList();

    /// <summary>
    /// Sets the page's viewport. Chrome will not shrink a real window below about 500 px, so phone widths
    /// are emulated through the DevTools protocol, as the browser's own device toolbar does.
    /// </summary>
    public void Resize(int width, int height)
    {
        ((ChromeDriver)Driver).ExecuteCdpCommand("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = width, ["height"] = height, ["deviceScaleFactor"] = 1, ["mobile"] = width < 768
        });
        Until(() => Convert.ToInt32(Js("return window.innerWidth;")) == width, what: $"the viewport to be {width}px wide");
    }

    public bool HasHorizontalScroll() =>
        (bool)(Js("return document.documentElement.scrollWidth > document.documentElement.clientWidth + 1;") ?? false);

    public void EmulateReducedMotion() =>
        ((ChromeDriver)Driver).ExecuteCdpCommand("Emulation.setEmulatedMedia", new Dictionary<string, object>
        {
            ["features"] = new[] { new Dictionary<string, object> { ["name"] = "prefers-reduced-motion", ["value"] = "reduce" } }
        });

    public static string XPathLiteral(string value) =>
        !value.Contains('\'') ? $"'{value}'"
        : !value.Contains('"') ? $"\"{value}\""
        : "concat('" + value.Replace("'", "', \"'\", '") + "')";

    private static string CssLiteral(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public void Dispose()
    {
        try { Driver.Quit(); } catch { /* already closed */ }
        Driver.Dispose();
    }
}
