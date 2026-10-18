namespace LendingService.E2ETests;

/// <summary>TEST-PLAN.md §5.13: borrowed books, returns, fines and the reservation history.</summary>
public sealed class ReturnsTests : E2ETestBase
{
    private static readonly By Status = By.CssSelector("section[aria-label='Current borrowed books'] p[role='status']");
    private static readonly By Error = By.CssSelector("section[aria-label='Current borrowed books'] p[role='alert']");

    private async Task OpenBorrowedAsync(Browser? browser = null)
    {
        browser ??= Browser;
        browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/borrowed");
        browser.Until(() => !browser.HasText("Loading borrowed books…"), TimeSpan.FromSeconds(30), "the loans to load");
    }

    private static IWebElement ReturnButton(Browser browser, BookInfo book) =>
        browser.Visible(Browser.RowContaining(book.Title)).FindElement(By.XPath(".//button"));

    private async Task<(Member Member, BookInfo Book, int Reservation)> LoanAsync(int daysOverdue = 0)
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync(2);
        var reservation = await Api.BorrowAsync(member, book);
        if (daysOverdue != 0) await Database.BackdateLoanAsync(reservation, daysOverdue);
        return (member, book, reservation);
    }

    [Fact, Trait("Id", "LOAN-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task LOAN01_OnTimeReturn_HasNoFineAndPutsTheCopyBack()
    {
        var (_, book, _) = await LoanAsync();
        await OpenBorrowedAsync();

        Browser.Click(ReturnButton(Browser, book));
        AssertContains("Late returns cost Rs. 10 per day", Browser.AcceptAlert());

        Assert.Equal("Book returned. No fine is due.", Browser.Visible(Status).Text);
        Assert.False(Browser.Exists(Browser.RowContaining(book.Title)));
        Assert.Equal(2, (await Api.BookAsync(book.Id))!["availableCopies"]!.GetValue<int>());
        Browser.Go("/admin/reservations/history");
        AssertContains("Returned", Browser.Visible(Browser.RowContaining(book.Title)).Text);
    }

    [Fact, Trait("Id", "LOAN-02"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task LOAN02_LateReturn_RecordsTenRupeesPerDay()
    {
        var (_, book, _) = await LoanAsync(daysOverdue: 3);
        await OpenBorrowedAsync();
        AssertContains("Overdue", Browser.Visible(Browser.RowContaining(book.Title)).Text);

        Browser.Click(ReturnButton(Browser, book));
        Browser.AcceptAlert();

        Assert.Equal("Book returned. 3 day(s) overdue. Fine: Rs. 30.00 — Unpaid.", Browser.Visible(Status).Text);
    }

    [Fact, Trait("Id", "LOAN-03"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task LOAN03_DueJustBeforeSriLankanMidnight_CountsAsOneDayLate()
    {
        var (_, book, reservation) = await LoanAsync();
        var colombo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var todayInColombo = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, colombo).Date;
        // 23:59 yesterday in Colombo: only minutes ago if it is just past midnight, but still a whole day late.
        var dueUtc = TimeZoneInfo.ConvertTimeToUtc(todayInColombo.AddMinutes(-1), colombo);
        await Database.SetDueDateAsync(reservation, dueUtc);
        await OpenBorrowedAsync();

        Browser.Click(ReturnButton(Browser, book));
        Browser.AcceptAlert();

        Assert.Equal("Book returned. 1 day(s) overdue. Fine: Rs. 10.00 — Unpaid.", Browser.Visible(Status).Text);
    }

    [Fact, Trait("Id", "LOAN-04"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task LOAN04_CancellingTheReturnDialog_KeepsTheLoan()
    {
        var (_, book, _) = await LoanAsync();
        await OpenBorrowedAsync();

        Browser.Click(ReturnButton(Browser, book));
        Browser.DismissAlert();

        Assert.True(Browser.Exists(Browser.RowContaining(book.Title)));
        Assert.Contains(await Api.BorrowedAsync(), loan => loan!["bookId"]!.GetValue<int>() == book.Id);
    }

    [Fact, Trait("Id", "LOAN-05"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task LOAN05_TwoAdminsReturningTheSameLoan_RecordItOnce()
    {
        var (member, book, _) = await LoanAsync(daysOverdue: 1);
        var other = OpenBrowser();
        await OpenBorrowedAsync();
        await OpenBorrowedAsync(other);
        var staleButton = ReturnButton(other, book);

        Browser.Click(ReturnButton(Browser, book));
        Browser.AcceptAlert();
        Browser.Visible(Status);
        other.Click(staleButton);
        other.AcceptAlert();

        // The second request repeats the finished return instead of recording another.
        Assert.Equal("Book returned. 1 day(s) overdue. Fine: Rs. 10.00 — Unpaid.", other.Visible(Status).Text);
        var history = (await Api.MyBorrowingsAsync(member))["history"]!.AsArray();
        Assert.Single(history, loan => (string?)loan!["bookTitle"] == book.Title);
        Assert.Equal(10m, (await Api.MyBorrowingsAsync(member))["totalUnpaid"]!.GetValue<decimal>());
    }

    [Fact, Trait("Id", "LOAN-06"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task LOAN06_InventoryDownDuringReturn_KeepsTheOriginalReturnTime()
    {
        var (member, book, _) = await LoanAsync();
        await OpenBorrowedAsync();
        var returnButton = ReturnButton(Browser, book);
        StopContainer(Containers.Inventory);
        var firstAttempt = DateTime.UtcNow;

        Browser.Click(returnButton);
        Browser.AcceptAlert();

        Assert.Equal("Inventory return could not be confirmed. Retry Return; the original return date is preserved.",
            Browser.Visible(Error, TimeSpan.FromSeconds(30)).Text);
        await StartContainerAsync(Containers.Inventory);
        await Task.Delay(5000);
        await OpenBorrowedAsync();
        Browser.Until(() => ReturnButton(Browser, book).Text == "Retry Return", what: "the Retry Return button");
        Browser.Click(ReturnButton(Browser, book));
        Browser.AcceptAlert();

        Assert.Equal("Book returned. No fine is due.", Browser.Visible(Status).Text);
        var returned = (await Api.MyBorrowingsAsync(member))["history"]!.AsArray().Single(loan => (string?)loan!["bookTitle"] == book.Title)!;
        var returnDate = DateTime.Parse((string)returned["returnDate"]!).ToUniversalTime();
        Assert.InRange(returnDate, firstAttempt.AddSeconds(-5), firstAttempt.AddSeconds(30));
    }

    [Fact, Trait("Id", "LOAN-07"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task LOAN07_HistoryFilter_ShowsOnlyTheChosenStatus()
    {
        var (_, returnedBook, returnedReservation) = await LoanAsync();
        await Api.ReturnAsync(await Api.BorrowRecordIdAsync(returnedReservation));
        var member = await NewMemberAsync();
        var cancelledBook = await NewBookAsync();
        await Api.RejectReservationAsync(await Api.ReserveAsync(member, cancelledBook));
        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/reservations/history");
        Browser.Visible(Browser.RowContaining(returnedBook.Title));

        IReadOnlyList<string> Statuses() => Browser.Driver.FindElements(By.CssSelector("tbody tr td:nth-child(4)")).Select(cell => cell.Text).Distinct().ToList();

        Browser.Select(By.CssSelector(".admin-users-panel-heading select"), "Returned");
        Browser.Until(() => Statuses().SequenceEqual(["Returned"]), what: "only returned records");
        Assert.True(Browser.Exists(Browser.RowContaining(returnedBook.Title)));
        Assert.False(Browser.Exists(Browser.RowContaining(cancelledBook.Title)));

        Browser.Select(By.CssSelector(".admin-users-panel-heading select"), "Cancelled");
        Browser.Until(() => Statuses().SequenceEqual(["Cancelled"]), what: "only cancelled records");
        Assert.True(Browser.Exists(Browser.RowContaining(cancelledBook.Title)));

        Browser.Select(By.CssSelector(".admin-users-panel-heading select"), "All");
        Browser.Until(() => Browser.Exists(Browser.RowContaining(returnedBook.Title)) && Browser.Exists(Browser.RowContaining(cancelledBook.Title)),
            what: "both records");
    }

    [Fact, Trait("Id", "LOAN-08"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task LOAN08_HistoryColumns_ShowTheFineOrADash()
    {
        var (_, lateBook, lateReservation) = await LoanAsync(daysOverdue: 2);
        await Api.ReturnAsync(await Api.BorrowRecordIdAsync(lateReservation));
        var member = await NewMemberAsync();
        var cancelledBook = await NewBookAsync();
        await Api.RejectReservationAsync(await Api.ReserveAsync(member, cancelledBook));

        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/reservations/history");

        var late = Browser.Visible(Browser.RowContaining(lateBook.Title)).FindElements(By.TagName("td")).Select(cell => cell.Text).ToList();
        Assert.Equal(["2", "20.00", "Unpaid"], late.Skip(8).Take(3));
        var cancelled = Browser.Visible(Browser.RowContaining(cancelledBook.Title)).FindElements(By.TagName("td")).Select(cell => cell.Text).ToList();
        Assert.Equal(["—", "—", "—"], cancelled.Skip(8).Take(3));
    }
}
