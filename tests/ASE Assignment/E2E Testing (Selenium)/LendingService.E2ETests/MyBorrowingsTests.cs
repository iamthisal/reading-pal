namespace LendingService.E2ETests;

/// <summary>TEST-PLAN.md §5.11: the member's My Borrowings page.</summary>
public sealed class MyBorrowingsTests : E2ETestBase
{
    private IWebElement Region(string label) => Browser.Visible(By.CssSelector($"[role='region'][aria-label='{label}']"));

    private IWebElement RowIn(string region, string title) =>
        Region(region).FindElement(By.XPath($".//tr[td[normalize-space(.)={Browser.XPathLiteral(title)}]]"));

    private void OpenMyBorrowings(Member member)
    {
        Browser.SignInAs(member, "/my-borrowings");
        Browser.Until(() => !Browser.HasText("Loading your borrowing records…"), what: "the records to load");
    }

    private string SriLankaDate(string isoUtc) => (string)Browser.Js(
        "return new Intl.DateTimeFormat('en-LK', { year: 'numeric', month: 'short', day: 'numeric', timeZone: 'Asia/Colombo' }).format(new Date(arguments[0]));",
        isoUtc)!;

    [Fact, Trait("Id", "MYB-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task MYB01_PendingReservation_IsListedWithACancelButton()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        await Api.ReserveAsync(member, book);

        OpenMyBorrowings(member);

        var row = RowIn("Pending reservations", book.Title);
        AssertContains("Pending", row.Text);
        Assert.True(row.FindElement(Aria($"Cancel reservation for {book.Title}")).Enabled);
    }

    [Fact, Trait("Id", "MYB-02"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task MYB02_AcceptedReservation_IsACurrentLoanDueInFourteenDays()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        await Api.BorrowAsync(member, book);
        var loan = (await Api.MyBorrowingsAsync(member))["active"]!.AsArray().Single(item => (string?)item!["bookTitle"] == book.Title)!;
        var checkout = DateTime.Parse((string)loan["checkoutDate"]!).ToUniversalTime();
        var due = DateTime.Parse((string)loan["dueDate"]!).ToUniversalTime();
        Assert.Equal(14, (due - checkout).TotalDays, 3);

        OpenMyBorrowings(member);

        var cells = RowIn("Current loans", book.Title).FindElements(By.TagName("td")).Select(cell => cell.Text).ToList();
        Assert.Equal(SriLankaDate((string)loan["dueDate"]!), cells[2]);
        Assert.Equal("0", cells[3]);
        Assert.Equal("Borrowed", cells[5]);
    }

    [Fact, Trait("Id", "MYB-03"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task MYB03_OverdueLoan_ShowsDaysOverdueAndAnEstimatedFine()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var reservation = await Api.BorrowAsync(member, book);
        await Database.BackdateLoanAsync(reservation, 3);

        OpenMyBorrowings(member);

        var cells = RowIn("Current loans", book.Title).FindElements(By.TagName("td")).Select(cell => cell.Text).ToList();
        Assert.Equal("3", cells[3]);
        Assert.Equal("Rs. 30.00", cells[4]);
        Assert.Equal("Overdue", cells[5]);
        AssertContains("Rs. 30.00", Browser.Find(By.CssSelector(".my-loans-summary")).Text);
    }

    [Fact, Trait("Id", "MYB-04"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task MYB04_ReturnedBook_MovesToBorrowingHistory()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var reservation = await Api.BorrowAsync(member, book);
        await Api.ReturnAsync(await Api.BorrowRecordIdAsync(reservation));

        OpenMyBorrowings(member);

        var cells = RowIn("Borrowing history", book.Title).FindElements(By.TagName("td")).Select(cell => cell.Text).ToList();
        Assert.NotEqual("—", cells[3]);
        Assert.Equal("Rs. 0.00", cells[5]);
        Assert.Equal("Returned · No fine", cells[6]);
        Assert.True(Browser.HasText("No current loans"));
    }

    [Fact, Trait("Id", "MYB-05"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task MYB05_MemberCancelsAPendingReservation()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        await Api.ReserveAsync(member, book);
        OpenMyBorrowings(member);

        Browser.Click(Aria($"Cancel reservation for {book.Title}"));
        Assert.Equal($"Cancel your reservation for \"{book.Title}\"?", Browser.AcceptAlert());

        Browser.WaitForText($"Your reservation for \"{book.Title}\" has been cancelled.");
        AssertContains("Cancelled", RowIn("Cancelled reservation history", book.Title).Text);
        Assert.False(await StillPendingAsync(book));
    }

    private async Task<bool> StillPendingAsync(BookInfo book) =>
        (await Api.PendingReservationsAsync()).Any(item => item!["bookId"]!.GetValue<int>() == book.Id);

    [Fact, Trait("Id", "MYB-06"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task MYB06_DismissingTheCancelDialog_KeepsTheReservation()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        await Api.ReserveAsync(member, book);
        OpenMyBorrowings(member);

        Browser.Click(Aria($"Cancel reservation for {book.Title}"));
        Browser.DismissAlert();

        Assert.True(RowIn("Pending reservations", book.Title).Displayed);
        Assert.True(await StillPendingAsync(book));
    }

    [Fact, Trait("Id", "MYB-07"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task MYB07_CancellingAfterTheAdminAccepted_IsRefused()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var reservation = await Api.ReserveAsync(member, book);
        OpenMyBorrowings(member);
        var cancel = Browser.Visible(Aria($"Cancel reservation for {book.Title}"));

        Assert.True((await Api.AcceptReservationAsync(reservation)).Ok);
        Browser.Click(cancel);
        Browser.AcceptAlert();

        Browser.WaitForText("This reservation is already being processed or has been accepted and cannot be cancelled. Refresh your list.");
        Assert.True(RowIn("Current loans", book.Title).Displayed);
    }

    [Fact, Trait("Id", "MYB-08"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task MYB08_LendingDown_ShowsTheLoadError()
    {
        var member = await NewMemberAsync();
        StopContainer(Containers.Lending);

        Browser.SignInAs(member, "/my-borrowings");

        Browser.WaitForText("Unable to load your borrowing records. Please try again.", TimeSpan.FromSeconds(30));
        await StartContainerAsync(Containers.Lending);
    }

    [Fact, Trait("Id", "MYB-09"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public void MYB09_TokenTheServiceRejects_AsksTheMemberToSignInAgain()
    {
        // Looks valid to the page but its signature is wrong, so Lending answers 401.
        Browser.SignInWithToken(FakeJwt.Create(DateTimeOffset.UtcNow.AddHours(1)), "/my-borrowings");

        Browser.WaitForText("Your session has expired. Please log in again.");
    }

    [Fact, Trait("Id", "MYB-10"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task MYB10_RecordedFine_IsAddedToTheUnpaidTotal()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var reservation = await Api.BorrowAsync(member, book);
        await Database.BackdateLoanAsync(reservation, 2);
        await Api.ReturnAsync(await Api.BorrowRecordIdAsync(reservation));

        OpenMyBorrowings(member);

        var unpaid = Browser.Find(By.XPath("//div[@class='my-loans-summary']/section[h2='Unpaid fines']/strong")).Text;
        Assert.Equal("Rs. 20.00", unpaid);
        Assert.Equal("Returned · Unpaid", RowIn("Borrowing history", book.Title).FindElements(By.TagName("td"))[6].Text);
    }
}
