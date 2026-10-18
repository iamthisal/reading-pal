using System.Globalization;

namespace InventoryService.E2ETests;

/// <summary>TEST-PLAN.md §5.18: checks that apply across the whole interface.</summary>
public sealed class CrossCuttingTests : E2ETestBase
{
    [Fact, Trait("Id", "X-01"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task X01_PhoneWidth_HasNoSidewaysScrollingOnTheMainPages()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        await Api.BorrowAsync(member, book);
        Browser.Go("/login");
        Browser.Resize(390, 844);

        foreach (var path in new[] { "/login", "/register" })
        {
            Browser.Go(path);
            Browser.Visible(By.Id("email"));
            Assert.False(Browser.HasHorizontalScroll(), $"{path} scrolls sideways at 390 px.");
        }

        Browser.SignInAs(member);
        foreach (var path in new[] { "/home", $"/search?q={Uri.EscapeDataString(book.Title)}", $"/books/{book.Id}", "/my-borrowings" })
        {
            Browser.Go(path);
            Browser.Visible(By.CssSelector(".app-main"));
            Thread.Sleep(1500);
            Assert.False(Browser.HasHorizontalScroll(), $"{path} scrolls sideways at 390 px.");
        }

        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/borrowed");
        Browser.Visible(Browser.RowContaining(book.Title));
        Assert.False(Browser.HasHorizontalScroll(), "/admin/borrowed scrolls sideways at 390 px.");
        // The wide table scrolls inside its own container instead.
        var wrap = Browser.Find(By.CssSelector(".admin-users-table-wrap"));
        Assert.Equal("auto", (string?)Browser.Js("return getComputedStyle(arguments[0]).overflowX;", wrap));
    }

    [Fact, Trait("Id", "X-02"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task X02_KeyboardOnly_SignInReserveAndCloseADialog()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.Go("/login");

        var email = Browser.Visible(By.Id("email"));
        email.SendKeys(member.Email);
        email.SendKeys(Keys.Tab);
        new OpenQA.Selenium.Interactions.Actions(Browser.Driver).SendKeys(member.Password).SendKeys(Keys.Enter).Perform();
        Browser.WaitForPath("/home");

        Browser.Go($"/books/{book.Id}");
        Browser.Visible(Aria($"Borrow {book.Title}"));
        for (var presses = 0; presses < 60 && (string?)Browser.Js("return document.activeElement.getAttribute('aria-label');") != $"Borrow {book.Title}"; presses++)
            new OpenQA.Selenium.Interactions.Actions(Browser.Driver).SendKeys(Keys.Tab).Perform();
        Assert.Equal($"Borrow {book.Title}", (string?)Browser.Js("return document.activeElement.getAttribute('aria-label');"));
        Assert.NotEqual("none", (string?)Browser.Js("return getComputedStyle(document.activeElement).outlineStyle;"));
        new OpenQA.Selenium.Interactions.Actions(Browser.Driver).SendKeys(Keys.Enter).Perform();
        Browser.WaitForText("Book reserved successfully!");

        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/books");
        Browser.ClickButton("Add new book");
        Browser.Until(() => Browser.OpenDialog != null, what: "the dialog");
        new OpenQA.Selenium.Interactions.Actions(Browser.Driver).SendKeys(Keys.Escape).Perform();
        Browser.Until(() => Browser.OpenDialog == null, what: "Escape to close the dialog");
    }

    [Fact, Trait("Id", "X-03"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task X03_ReducedMotion_StopsTheSliderAndTheShimmer()
    {
        var member = await NewMemberAsync();
        Browser.Go("/login");
        Browser.EmulateReducedMotion();
        Browser.SignInAs(member);
        Browser.Visible(Aria("Library highlights"));

        Thread.Sleep(8000);

        Assert.StartsWith("Show poster 1", Browser.Find(By.CssSelector(".poster-dot[aria-current='true']")).GetAttribute("aria-label"));
        var animation = (string?)Browser.Js(
            "const probe = document.createElement('img'); probe.className = 'shelf-cover skeleton'; document.body.appendChild(probe);" +
            "const name = getComputedStyle(probe).animationName; probe.remove(); return name;");
        Assert.Equal("none", animation);
    }

    [Fact, Trait("Id", "X-04"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task X04_CoreJourney_LeavesNoUncaughtErrorsInTheConsole()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member);

        foreach (var path in new[] { "/home", $"/search?q={Uri.EscapeDataString(book.Title)}", $"/books/{book.Id}", "/favourites", "/my-borrowings", "/notifications", "/profile" })
        {
            Browser.Go(path);
            Browser.Visible(By.CssSelector(".app-main"));
            Thread.Sleep(1500);
        }

        var uncaught = Browser.SevereConsoleErrors().Where(message => message.Contains("Uncaught", StringComparison.Ordinal)
            || message.Contains("TypeError", StringComparison.Ordinal) || message.Contains("ReferenceError", StringComparison.Ordinal)).ToList();
        Assert.True(uncaught.Count == 0, string.Join("\n", uncaught));
    }

    [Fact, Trait("Id", "X-05"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task X05_ScriptInABookTitle_IsShownAsTextEverywhere()
    {
        var member = await NewMemberAsync();
        var title = $"<img src=x onerror=window.__e2e=1> {TestIds.Short()}";
        var book = await NewBookAsync(title: title);
        var reservation = await Api.ReserveAsync(member, book);

        Browser.SignInAs(member, $"/search?q={Uri.EscapeDataString(title)}");
        Browser.WaitForText(title);
        Browser.Go($"/books/{book.Id}");
        Browser.WaitForText(title);
        Assert.Null(Browser.Js("return window.__e2e;"));

        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/reservations/pending");
        Browser.Visible(Browser.RowContaining(title));
        Assert.Null(Browser.Js("return window.__e2e;"));

        Assert.True((await Api.AcceptReservationAsync(reservation)).Ok);
        Assert.NotNull(await Api.WaitForNotificationAsync(member.Token, false, title));
        Browser.SignInAs(member, "/notifications");
        Browser.WaitForText(title);
        Assert.Null(Browser.Js("return window.__e2e;"));
    }

    [Fact, Trait("Id", "X-06"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task X06_NotificationTimes_AreShownInSriLankaTime()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        await Api.ReserveAsync(member, book);
        var admin = await AdminTokenAsync();
        var notification = await Api.WaitForNotificationAsync(admin, true, book.Title)
            ?? throw new InvalidOperationException("No admin notification arrived.");

        var created = DateTime.Parse((string)notification["createdAtUtc"]!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        var colombo = TimeZoneInfo.ConvertTimeFromUtc(created, TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo"));
        var expected = colombo.ToString("d MMM yyyy, HH:mm", CultureInfo.GetCultureInfo("en-GB"));

        Browser.SignInAsAdmin(admin, "/admin/notifications");
        var row = Browser.Visible(By.XPath($"//li[contains(@class,'notification-list-item')][.//p[contains(., {Browser.XPathLiteral(book.Title)})]]"));
        Assert.Equal(expected, row.FindElement(By.TagName("time")).Text);
    }
}
