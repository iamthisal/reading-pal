namespace LendingService.E2ETests;

/// <summary>TEST-PLAN.md §5.10: the Borrow button and reserving a book.</summary>
public sealed class ReserveTests : E2ETestBase
{
    private static string PendingLabel(BookInfo book) => $"Your request for {book.Title} is waiting for approval";
    private static string BorrowedLabel(BookInfo book) => $"You have borrowed {book.Title}";
    private static string BorrowLabel(BookInfo book) => $"Borrow {book.Title}";
    private static readonly By Toast = By.CssSelector(".discover-toast");

    private async Task<bool> HasPendingRequestAsync(BookInfo book) =>
        (await Api.PendingReservationsAsync()).Any(item => item!["bookId"]!.GetValue<int>() == book.Id);

    /// <summary>The page re-reads the member's reservations when the window regains focus.</summary>
    private void RefocusWindow() => Browser.Js("window.dispatchEvent(new Event('focus'));");

    [Fact, Trait("Id", "RES-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task RES01_ApprovedMember_ReservesABook_AndTheButtonShowsPending()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member, $"/books/{book.Id}");

        Browser.Click(Aria(BorrowLabel(book)));

        Assert.Equal("Book reserved successfully! It is now pending admin approval.", Browser.Visible(Toast).Text.TrimEnd('×').Trim());
        var pending = Browser.Visible(Aria(PendingLabel(book)));
        Assert.Equal("Pending", pending.Text);
        Assert.False(pending.Enabled);
        Assert.True(await HasPendingRequestAsync(book));
    }

    [Fact, Trait("Id", "RES-02"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task RES02_PendingState_ShowsOnTheSearchListBookPageAndShelves()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member, $"/books/{book.Id}");
        Browser.Click(Aria(BorrowLabel(book)));
        Browser.Visible(Aria(PendingLabel(book)));

        Browser.Go($"/search?q={Uri.EscapeDataString(book.Title)}");
        Assert.Equal("Pending", Browser.Visible(Aria(PendingLabel(book))).Text);
        Browser.Go($"/authors/{Uri.EscapeDataString(book.Author)}");
        Assert.Equal("Pending", Browser.Visible(By.CssSelector($"section.shelf [aria-label={Quote(PendingLabel(book))}]")).Text);
    }

    [Fact, Trait("Id", "RES-03"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task RES03_AcceptedRequest_TurnsTheButtonToBorrowed()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var reservation = await Api.ReserveAsync(member, book);
        Browser.SignInAs(member, $"/books/{book.Id}");
        Browser.Visible(Aria(PendingLabel(book)));

        Assert.True((await Api.AcceptReservationAsync(reservation)).Ok);
        RefocusWindow();

        var borrowed = Browser.Visible(Aria(BorrowedLabel(book)), TimeSpan.FromSeconds(40));
        Assert.Equal("Borrowed", borrowed.Text);
        Assert.False(borrowed.Enabled);
    }

    [Fact, Trait("Id", "RES-04"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task RES04_ReturnedBook_CanBeBorrowedAgain()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var reservation = await Api.BorrowAsync(member, book);
        Browser.SignInAs(member, $"/books/{book.Id}");
        Browser.Visible(Aria(BorrowedLabel(book)));

        Assert.True((await Api.ReturnAsync(await Api.BorrowRecordIdAsync(reservation))).Ok);
        Browser.Reload();

        Assert.True(Browser.Visible(Aria(BorrowLabel(book))).Enabled);
    }

    [Fact, Trait("Id", "RES-05"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task RES05_RejectedOrCancelledRequest_ShowsBorrowAgain()
    {
        var member = await NewMemberAsync();
        var rejected = await NewBookAsync();
        var cancelled = await NewBookAsync();
        var rejectedId = await Api.ReserveAsync(member, rejected);
        var cancelledId = await Api.ReserveAsync(member, cancelled);

        Assert.True((await Api.RejectReservationAsync(rejectedId)).Ok);
        Assert.True((await Api.CancelReservationAsync(member, cancelledId)).Ok);

        Browser.SignInAs(member, $"/books/{rejected.Id}");
        Assert.True(Browser.Visible(Aria(BorrowLabel(rejected))).Enabled);
        Browser.Go($"/books/{cancelled.Id}");
        Assert.True(Browser.Visible(Aria(BorrowLabel(cancelled))).Enabled);
    }

    [Fact, Trait("Id", "RES-06"), Trait("Priority", "P1"), Trait("Type", "Unhappy")]
    public async Task RES06_NoCopiesOnTheShelf_ShowsAGreyedDisabledBorrow()
    {
        var member = await NewMemberAsync();
        var book = await NewUnavailableBookAsync();

        Browser.SignInAs(member, $"/books/{book.Id}");

        var button = Browser.Visible(Aria($"{book.Title} has no copies on the shelf"));
        Assert.Equal("Borrow", button.Text);
        Assert.False(button.Enabled);
        Assert.Equal("No copies on the shelf right now", button.GetAttribute("title"));
        Assert.Equal("not-allowed", button.GetCssValue("cursor"));
        AssertContains("Every copy is out at the moment.", Browser.BodyText);
    }

    [Fact, Trait("Id", "RES-07"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task RES07_CopiesRunningOutAfterThePageLoaded_IsRefused()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync(1);
        Browser.SignInAs(member, $"/books/{book.Id}");
        var borrow = Browser.Visible(Aria(BorrowLabel(book)));

        Assert.True((await Api.MarkUnavailableAsync(book.Id)).Ok);
        Browser.Click(borrow);

        AssertContains("Book is currently unavailable for reservation.", Browser.Visible(Toast).Text);
        Assert.False(await HasPendingRequestAsync(book));
    }

    [Fact, Trait("Id", "RES-08"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task RES08_SecondRequestForTheSameBook_IsRefused()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member, $"/books/{book.Id}");
        var borrow = Browser.Visible(Aria(BorrowLabel(book)));

        // The first request is made elsewhere (another tab), so this page still offers Borrow.
        await Api.ReserveAsync(member, book);
        Browser.Click(borrow);

        AssertContains("You already have a pending reservation for this book.", Browser.Visible(Toast).Text);
        Assert.Single(await Api.PendingReservationsAsync(), item => item!["bookId"]!.GetValue<int>() == book.Id);
    }

    [Fact, Trait("Id", "RES-09"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task RES09_Admin_SeesBorrow_ButIsRefused()
    {
        var book = await NewBookAsync();
        Browser.SignInAsAdmin(await AdminTokenAsync(), $"/books/{book.Id}");

        // Finding 1 in TEST-PLAN.md §10: the button should be hidden for admins.
        Browser.Click(Aria(BorrowLabel(book)));

        AssertContains("Admins cannot reserve books. Please log in with a regular user account.", Browser.Visible(Toast).Text);
        Assert.False(await HasPendingRequestAsync(book));
    }

    [Fact, Trait("Id", "RES-10"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task RES10_BookDeletedAfterThePageLoaded_IsReportedAsNotFound()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member, $"/books/{book.Id}");
        var borrow = Browser.Visible(Aria(BorrowLabel(book)));

        Assert.True((await Api.DeleteBookAsync(book.Id)).Ok);
        Browser.Click(borrow);

        AssertContains($"Book with ID {book.Id} not found in inventory.", Browser.Visible(Toast).Text);
    }

    [Fact, Trait("Id", "RES-11"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task RES11_LendingDown_ShowsAnErrorAndTheButtonRecovers()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member, $"/books/{book.Id}");
        Browser.Visible(Aria(BorrowLabel(book)));
        StopContainer(Containers.Lending);

        Browser.Click(Aria(BorrowLabel(book)));

        AssertContains("Failed to reserve book. Please try again.", Browser.Visible(Toast, TimeSpan.FromSeconds(30)).Text);
        var borrow = Browser.Visible(Aria(BorrowLabel(book)));
        Assert.Equal("Borrow", borrow.Text);
        Assert.True(borrow.Enabled);
        await StartContainerAsync(Containers.Lending);
    }

    [Fact, Trait("Id", "RES-12"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task RES12_UserServiceDown_RefusesBecauseApprovalCannotBeChecked()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member, $"/books/{book.Id}");
        Browser.Visible(Aria(BorrowLabel(book)));
        StopContainer(Containers.User);

        Browser.Click(Aria(BorrowLabel(book)));

        AssertContains("Your account could not be verified right now. Please try again.", Browser.Visible(Toast, TimeSpan.FromSeconds(40)).Text);
        await StartContainerAsync(Containers.User);
        Assert.False(await HasPendingRequestAsync(book));
    }

    [Fact, Trait("Id", "RES-13"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task RES13_DoubleClick_CreatesOnlyOneRequest()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member, $"/books/{book.Id}");

        new OpenQA.Selenium.Interactions.Actions(Browser.Driver).DoubleClick(Browser.Visible(Aria(BorrowLabel(book)))).Perform();

        Browser.Visible(Aria(PendingLabel(book)));
        Thread.Sleep(2000);
        Assert.Single(await Api.PendingReservationsAsync(), item => item!["bookId"]!.GetValue<int>() == book.Id);
    }

    [Fact, Trait("Id", "RES-14"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task RES14_MemberWithOtherRequestsAndLoans_CanBorrowAnotherBook()
    {
        var member = await NewMemberAsync();
        await Api.ReserveAsync(member, await NewBookAsync());
        await Api.BorrowAsync(member, await NewBookAsync());
        var third = await NewBookAsync();
        Browser.SignInAs(member, $"/books/{third.Id}");

        Browser.Click(Aria(BorrowLabel(third)));

        AssertContains("Book reserved successfully!", Browser.Visible(Toast).Text);
        Assert.True(await HasPendingRequestAsync(third));
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
