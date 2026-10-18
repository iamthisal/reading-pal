namespace NotificationService.E2ETests;

/// <summary>TEST-PLAN.md §5.17, the inbox cases: reading notifications in the bell and on the Notifications page.</summary>
public sealed class NotificationInboxTests : E2ETestBase
{
    private static readonly By Bell = By.CssSelector("button.notification-bell-button");

    private static By UnreadItem(string text) =>
        By.XPath($"//section[contains(@class,'notification-list-card')]/ul[contains(@class,'notification-list-items') and not(contains(@class,'notification-list-history'))]" +
                 $"/li[.//p[contains(., {Browser.XPathLiteral(text)})]]");

    private static By HistoryItems => By.CssSelector("ul.notification-list-history li");

    /// <summary>A member who has <paramref name="count"/> rejection notifications waiting for one book.</summary>
    private async Task<(Member Member, BookInfo Book)> MemberWithNotificationsAsync(int count = 1)
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        for (var index = 0; index < count; index++) await Api.RejectReservationAsync(await Api.ReserveAsync(member, book));
        var deadline = DateTime.UtcNow + E2EConfig.EventTimeout + TimeSpan.FromSeconds(count);
        while ((await Api.NotificationsAsync(member.Token, false)).Count(item => ((string?)item!["message"])!.Contains(Rejected(book))) < count)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The rejection notifications did not arrive.");
            await Task.Delay(1000);
        }
        return (member, book);
    }

    // The member may also get a "New in the catalogue" notice for the same book, so match the rejection itself.
    private static string Rejected(BookInfo book) => $"'{book.Title}' was rejected";

    private string BellLabel => Browser.Find(Bell).GetAttribute("aria-label")!;

    [Fact, Trait("Id", "NOTIF-12"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task NOTIF12_MarkingOneAsRead_RemovesItFromUnreadAndLowersTheBellCount()
    {
        var (member, book) = await MemberWithNotificationsAsync();
        Browser.SignInAs(member, "/notifications");
        var item = Browser.Visible(UnreadItem(Rejected(book)));
        Browser.Until(() => BellLabel.StartsWith("Notifications, "), what: "an unread count on the bell");
        var before = int.Parse(BellLabel.Split(", ")[1].Split(' ')[0]);

        Browser.Click(item.FindElement(By.XPath(".//button[normalize-space(.)='Mark as read']")));

        Browser.Until(() => !Browser.Exists(UnreadItem(Rejected(book))), what: "the item to leave the unread list");
        Browser.Until(() => before == 1 ? BellLabel == "Notifications" : BellLabel == $"Notifications, {before - 1} unread", what: "the bell count to drop");
    }

    [Fact, Trait("Id", "NOTIF-13"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task NOTIF13_MarkAllAsRead_EmptiesTheUnreadList()
    {
        var (member, _) = await MemberWithNotificationsAsync(2);
        Browser.SignInAs(member, "/notifications");
        var unread = (await Api.NotificationsAsync(member.Token, false)).Count(item => !item!["isRead"]!.GetValue<bool>());
        Browser.WaitForText($"{unread} unread");

        Browser.Click(By.XPath("//div[@class='notification-list-actions']//button[normalize-space(.)='Mark all as read']"));

        Browser.WaitForText("No unread notifications.");
        Browser.WaitForText("All caught up");
        Browser.Until(() => BellLabel == "Notifications", what: "the bell to clear");
    }

    [Fact, Trait("Id", "NOTIF-14"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task NOTIF14_ShowAndHideViewed_PagesTheHistoryInTwenties()
    {
        var (member, book) = await MemberWithNotificationsAsync(21);
        var total = (await Api.NotificationsAsync(member.Token, false)).Count;
        Assert.InRange(total, 21, 40);
        Browser.SignInAs(member, "/notifications");
        Browser.WaitForText($"{total} unread");
        Browser.Click(By.XPath("//div[@class='notification-list-actions']//button[normalize-space(.)='Mark all as read']"));
        Browser.WaitForText("No unread notifications.");

        Browser.ClickButton("Show Viewed");
        Browser.Until(() => Browser.Driver.FindElements(HistoryItems).Count == 20, what: "the first 20 viewed notifications");
        Assert.True(Browser.Visible(By.CssSelector(".notification-list-history-heading")).Displayed);
        Assert.True(Browser.Exists(Browser.ButtonByText("Hide Viewed")));

        Browser.ClickButton("Load more history");
        Browser.Until(() => Browser.Driver.FindElements(HistoryItems).Count == total, what: "the rest of the viewed notifications");
        Assert.False(Browser.Exists(Browser.ButtonByText("Load more history")));

        Browser.ClickButton("Hide Viewed");
        Browser.Until(() => !Browser.Exists(HistoryItems), what: "the history to hide");
        Assert.True(Browser.Exists(Browser.ButtonByText("Show Viewed")));

        Browser.ClickButton("Show Viewed");
        Browser.Until(() => Browser.Driver.FindElements(HistoryItems).Count == total, what: "the loaded history to come back");
        AssertContains(book.Title, Browser.Find(By.CssSelector("ul.notification-list-history")).Text);
    }

    [Fact, Trait("Id", "NOTIF-15"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task NOTIF15_ReadStatus_IsKeptPerAdmin()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var adminA = await AdminTokenAsync();
        var adminB = await SecondAdminTokenAsync();
        await Api.ReserveAsync(member, book);
        Assert.NotNull(await Api.WaitForNotificationAsync(adminB, true, $"reserved '{book.Title}'"));
        Browser.SignInAsAdmin(adminA, "/admin/notifications");

        Browser.Click(Browser.Visible(UnreadItem(book.Title)).FindElement(By.XPath(".//button[normalize-space(.)='Mark as read']")));
        Browser.Until(() => !Browser.Exists(UnreadItem(book.Title)), what: "admin A's item to be read");

        var forB = (await Api.NotificationsAsync(adminB, true)).First(item => ((string?)item!["message"])!.Contains($"reserved '{book.Title}'"))!;
        Assert.False(forB["isRead"]!.GetValue<bool>());
    }

    [Fact, Trait("Id", "NOTIF-16"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task NOTIF16_ReadingFromTheBell_UpdatesTheOpenPage()
    {
        var (member, book) = await MemberWithNotificationsAsync();
        Browser.SignInAs(member, "/notifications");
        Browser.Visible(UnreadItem(Rejected(book)));

        Browser.Click(Bell);
        Browser.Click(By.XPath("//div[@role='dialog'][@aria-label='Notifications']//button[normalize-space(.)='Mark all as read']"));

        Browser.WaitForText("No unread notifications.");
        Assert.False(Browser.Exists(UnreadItem(Rejected(book))));
    }

    [Fact, Trait("Id", "NOTIF-17"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task NOTIF17_NotificationServiceDown_ShowsErrorsInTheBellAndThePage()
    {
        var member = await NewMemberAsync();
        StopContainer(Containers.Notification);

        Browser.SignInAs(member, "/notifications");

        Browser.WaitForText("Your notifications could not be loaded.", TimeSpan.FromSeconds(30));
        Assert.True(Browser.Exists(Browser.ButtonByText("Retry")));
        Browser.Click(Bell);
        Browser.WaitForText("Notifications are unavailable right now.", TimeSpan.FromSeconds(30));
        await StartContainerAsync(Containers.Notification);
        Browser.ClickButton("Retry");
        Browser.Until(() => !Browser.HasText("Your notifications could not be loaded."), TimeSpan.FromSeconds(30), "the page to recover");
    }
}
