using OpenQA.Selenium;
using Xunit;

namespace LendingService.SeleniumTests;

public class ReserveBookEdgeCaseSeleniumTests : LendingSeleniumTestBase
{
    // TC-RESERVE-EDGE-001: a book with no available copies can't be borrowed. The member sees a
    // disabled Borrow button labelled as having no copies, instead of a working one that would only
    // fail at the API ("Book is currently unavailable for reservation.").
    [Fact]
    public async Task TC_RESERVE_EDGE_001_BookWithNoAvailableCopies_ShowsDisabledBorrowButton()
    {
        var title = $"QA NoCopies {Guid.NewGuid():N}";
        var isbn = CreateReservableBookViaAdminUi(title, copies: 1);

        var adminToken = await GetAdminApiTokenAsync();
        await MarkBookUnavailableAsync(await GetBookIdByIsbnAsync(isbn, adminToken), adminToken);

        LoginAsUser();
        SearchFor(title);

        var button = WaitForBorrowButton(title, $"{title} has no copies on the shelf");
        Assert.False(button.Enabled);
        Assert.Empty(Driver.FindElements(By.CssSelector($"button[aria-label=\"Borrow {title}\"]")));
    }

    // TC-RESERVE-EDGE-002: a member can't reserve the same book twice while the first request is
    // pending (fixed in #91). Once reserved, the button turns into a disabled "Pending", and My
    // borrowings holds exactly one pending reservation for the book.
    [Fact]
    public void TC_RESERVE_EDGE_002_UserCannotReserveSameBookTwice()
    {
        var title = $"QA Duplicate {Guid.NewGuid():N}";
        CreateReservableBookViaAdminUi(title, copies: 5);

        LoginAsUser();
        ReserveBookAsCurrentUser(title);

        SearchFor(title);
        var pending = WaitForBorrowButton(title, $"Your request for {title} is waiting for approval");
        Assert.False(pending.Enabled);
        Assert.Empty(Driver.FindElements(By.CssSelector($"button[aria-label=\"Borrow {title}\"]")));

        NavigateTo("/my-borrowings");
        var pendingSection = Wait.Until(d => d.FindElement(By.CssSelector("[aria-label='Pending reservations']")));
        Wait.Until(_ => pendingSection.Text.Contains(title));
        Assert.Single(pendingSection.FindElements(RowFor(title)));
    }
}
