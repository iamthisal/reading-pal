namespace LendingService.E2ETests;

/// <summary>TEST-PLAN.md §5.12: the admin's pending reservation queue.</summary>
public sealed class ReservationQueueTests : E2ETestBase
{
    private static readonly By ActionError = By.CssSelector("section[aria-label='Pending reservation queue'] p[role='alert']");

    private async Task OpenQueueAsync(Browser? browser = null)
    {
        browser ??= Browser;
        browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/reservations/pending");
        browser.Until(() => !browser.HasText("Loading pending reservations…"), TimeSpan.FromSeconds(30), "the queue to load");
    }

    private static string AcceptLabel(BookInfo book, Member member) => $"Accept {book.Title} for {member.FullName}";
    private static string RejectLabel(BookInfo book, Member member) => $"Reject {book.Title} for {member.FullName}";

    private async Task<int> AvailableCopiesAsync(BookInfo book) => (await Api.BookAsync(book.Id))!["availableCopies"]!.GetValue<int>();

    [Fact, Trait("Id", "ADMRES-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task ADMRES01_NewRequest_AppearsInTheQueueWithTheMembersName()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        await Api.ReserveAsync(member, book);

        await OpenQueueAsync();

        var row = Browser.Visible(Browser.RowContaining(book.Title));
        AssertContains(member.FullName, row.Text);
        Assert.True(row.FindElement(Aria(AcceptLabel(book, member))).Enabled);
        Assert.True(row.FindElement(Aria(RejectLabel(book, member))).Enabled);
    }

    [Fact, Trait("Id", "ADMRES-02"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task ADMRES02_Accepting_LendsTheBookAndTakesACopy()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync(2);
        await Api.ReserveAsync(member, book);
        await OpenQueueAsync();

        Browser.Click(Aria(AcceptLabel(book, member)));

        Browser.WaitForText($"{book.Title} is now borrowed by {member.FullName} for 14 days. Notification event queued.");
        Assert.False(Browser.Exists(Browser.RowContaining(book.Title)));
        Assert.Equal(1, await AvailableCopiesAsync(book));
        Browser.Go("/admin/borrowed");
        Assert.True(Browser.Visible(Browser.RowContaining(book.Title)).Displayed);
    }

    [Fact, Trait("Id", "ADMRES-03"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task ADMRES03_Rejecting_CancelsTheRequestWithoutTouchingCopies()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync(2);
        await Api.ReserveAsync(member, book);
        await OpenQueueAsync();

        Browser.Click(Aria(RejectLabel(book, member)));

        Browser.WaitForText($"Reservation for {book.Title} by {member.FullName} was cancelled. Notification event queued.");
        Assert.Equal(2, await AvailableCopiesAsync(book));
        Browser.Go("/admin/reservations/history");
        AssertContains("Cancelled", Browser.Visible(Browser.RowContaining(book.Title)).Text);
    }

    [Fact, Trait("Id", "ADMRES-04"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ADMRES04_AcceptingWithNoCopyLeft_KeepsTheRowForRetry()
    {
        var first = await NewMemberAsync();
        var second = await NewMemberAsync();
        var book = await NewBookAsync(1);
        await Api.ReserveAsync(first, book);
        await Api.ReserveAsync(second, book);
        await OpenQueueAsync();

        Browser.Click(Aria(AcceptLabel(book, first)));
        Browser.WaitForText($"is now borrowed by {first.FullName}");
        Browser.Click(Aria(AcceptLabel(book, second)));

        Assert.Equal("Inventory could not allocate a copy. Acceptance is pending; retry when a copy is available.", Browser.Visible(ActionError).Text);
        Browser.Until(() => Browser.Find(Aria(AcceptLabel(book, second))).Text == "Retry Accept", what: "the Retry Accept button");
        Assert.False(Browser.Find(Aria(RejectLabel(book, second))).Enabled);
    }

    [Fact, Trait("Id", "ADMRES-05"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMRES05_RetryAccept_SucceedsOnceACopyComesBack()
    {
        var first = await NewMemberAsync();
        var second = await NewMemberAsync();
        var book = await NewBookAsync(1);
        // Both ask while the copy is on the shelf; once it is lent, a new request would be refused.
        var firstReservation = await Api.ReserveAsync(first, book);
        var secondReservation = await Api.ReserveAsync(second, book);
        Assert.True((await Api.AcceptReservationAsync(firstReservation)).Ok);
        Assert.False((await Api.AcceptReservationAsync(secondReservation)).Ok);
        await Api.ReturnAsync(await Api.BorrowRecordIdAsync(firstReservation));
        await OpenQueueAsync();

        Browser.Click(Aria(AcceptLabel(book, second)));

        Browser.WaitForText($"{book.Title} is now borrowed by {second.FullName}");
        Assert.Contains((await Api.MyBorrowingsAsync(second))["active"]!.AsArray(), loan => (string?)loan!["bookTitle"] == book.Title);
        Assert.Equal(0, await AvailableCopiesAsync(book));
    }

    [Fact, Trait("Id", "ADMRES-06"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ADMRES06_SecondAdminActingOnAHandledRequest_IsToldItChanged()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        await Api.ReserveAsync(member, book);
        var other = OpenBrowser();
        await OpenQueueAsync();
        await OpenQueueAsync(other);
        other.Visible(Aria(RejectLabel(book, member)));

        Browser.Click(Aria(AcceptLabel(book, member)));
        Browser.WaitForText("is now borrowed by");
        other.Click(Aria(RejectLabel(book, member)));

        Assert.Equal("Only pending reservations can be rejected. Refresh the queue.", other.Visible(ActionError).Text);
        other.Until(() => !other.Exists(Browser.RowContaining(book.Title)), what: "the queue to refresh");
    }

    [Fact, Trait("Id", "ADMRES-07"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ADMRES07_AcceptingARequestTheMemberCancelled_IsRefused()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var reservation = await Api.ReserveAsync(member, book);
        await OpenQueueAsync();
        var accept = Browser.Visible(Aria(AcceptLabel(book, member)));

        Assert.True((await Api.CancelReservationAsync(member, reservation)).Ok);
        Browser.Click(accept);

        Assert.Equal("Only pending reservations can be accepted. Refresh the queue.", Browser.Visible(ActionError).Text);
        Assert.Empty((await Api.MyBorrowingsAsync(member))["active"]!.AsArray());
    }

    [Fact, Trait("Id", "ADMRES-08"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ADMRES08_InventoryDownDuringAccept_CanBeRetriedSafely()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync(2);
        await Api.ReserveAsync(member, book);
        await OpenQueueAsync();
        var accept = Browser.Visible(Aria(AcceptLabel(book, member)));

        StopContainer(Containers.Inventory);
        Browser.Click(accept);

        Assert.Equal("Inventory checkout could not be confirmed. Retry Accept to safely complete this reservation.",
            Browser.Visible(ActionError, TimeSpan.FromSeconds(30)).Text);
        await StartContainerAsync(Containers.Inventory);
        await OpenQueueAsync();
        Browser.Until(() => Browser.Find(Aria(AcceptLabel(book, member))).Text == "Retry Accept", what: "the Retry Accept button");
        Browser.Click(Aria(AcceptLabel(book, member)));

        Browser.WaitForText($"{book.Title} is now borrowed by {member.FullName}");
        Assert.Equal(1, await AvailableCopiesAsync(book));
    }

    [Fact, Trait("Id", "ADMRES-09"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ADMRES09_LendingDown_ShowsTheQueueError()
    {
        var token = await AdminTokenAsync();
        StopContainer(Containers.Lending);

        Browser.SignInAsAdmin(token, "/admin/reservations/pending");

        Browser.WaitForText("Unable to load pending reservations. Please try refreshing.", TimeSpan.FromSeconds(30));
        await StartContainerAsync(Containers.Lending);
    }

    [Fact, Trait("Id", "ADMRES-10"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task ADMRES10_RefreshButton_BringsInRequestsMadeAfterThePageLoaded()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        await OpenQueueAsync();
        Assert.False(Browser.Exists(Browser.RowContaining(book.Title)));

        await Api.ReserveAsync(member, book);
        Browser.Click(By.XPath("//div[contains(@class,'page-banner-actions')]//button[normalize-space(.)='Refresh']"));

        Assert.True(Browser.Visible(Browser.RowContaining(book.Title), TimeSpan.FromSeconds(30)).Displayed);
    }
}
