using OpenQA.Selenium;
using Xunit;

namespace LendingService.SeleniumTests;

/// <summary>
/// MyBorrowingsController.Get is a distinct, user-scoped code path from the admin-side
/// ReservationsController.GetBorrowed/GetHistory that AdminAcceptRejectSeleniumTests and
/// AdminReturnSeleniumTests already exercise — those only ever check the admin's own "Borrowed
/// books" and "Reservation history" pages. Nothing so far has looked at what a regular user sees
/// on their own "My borrowings" page once a reservation is accepted or returned; these two tests
/// close that gap.
/// </summary>
public class MyBorrowingsActiveAndHistorySeleniumTests : LendingSeleniumTestBase
{
    private IWebElement WaitForRow(string title) =>
        Wait.Until(d => d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault())
        ?? throw new NoSuchElementException($"No row found for '{title}'.");

    // TC-MYBORROWINGS-001: Happy path — once admin accepts a user's reservation, that user's own
    // "My borrowings" page shows it under "Current loans" (not just the admin's "Borrowed books").
    [Fact]
    public void TC_MYBORROWINGS_001_AcceptedReservation_AppearsInUsersOwnCurrentLoans()
    {
        var title = $"QA MyLoans {Guid.NewGuid():N}";
        CreateReservableBookViaAdminUi(title);

        LoginAsUser();
        ReserveBookAsCurrentUser(title);

        LoginAsAdmin();
        NavigateTo("/admin/reservations/pending");
        WaitForRow(title).FindElement(By.CssSelector(".reservation-accept-button:not(.reservation-reject-button)")).Click();
        Wait.Until(d => d.PageSource.Contains("is now borrowed by"));

        LoginAsUser();
        NavigateTo("/my-borrowings");

        var currentLoansSection = Wait.Until(d => d.FindElement(By.CssSelector("[aria-label='Current loans']")));
        Assert.Contains(title, currentLoansSection.Text);

        var row = currentLoansSection.FindElement(By.XPath($".//tr[.//*[contains(normalize-space(.), '{title}')]]"));
        var dueDateCell = row.FindElements(By.TagName("td"))[2]; // Book | Borrowed | Due date | ...
        Assert.False(string.IsNullOrWhiteSpace(dueDateCell.Text));
        Assert.DoesNotContain("—", dueDateCell.Text);
    }

    // TC-MYBORROWINGS-002: Happy path — once admin returns that loan on time, the same user's own
    // "My borrowings" page shows it under "Borrowing history" with no fine.
    [Fact]
    public void TC_MYBORROWINGS_002_ReturnedLoan_AppearsInUsersOwnBorrowingHistory()
    {
        var title = $"QA MyHistory {Guid.NewGuid():N}";
        CreateReservableBookViaAdminUi(title);

        LoginAsUser();
        ReserveBookAsCurrentUser(title);

        LoginAsAdmin();
        NavigateTo("/admin/reservations/pending");
        WaitForRow(title).FindElement(By.CssSelector(".reservation-accept-button:not(.reservation-reject-button)")).Click();
        Wait.Until(d => d.PageSource.Contains("is now borrowed by"));

        NavigateTo("/admin/borrowed");
        WaitForRow(title).FindElement(By.CssSelector("button.btn-primary")).Click();
        AcceptConfirmDialogIfPresent();
        Wait.Until(d => d.PageSource.Contains("No fine is due"));

        LoginAsUser();
        NavigateTo("/my-borrowings");

        var historySection = Wait.Until(d => d.FindElement(By.CssSelector("[aria-label='Borrowing history']")));
        Assert.Contains(title, historySection.Text);

        var row = historySection.FindElement(By.XPath($".//tr[.//*[contains(normalize-space(.), '{title}')]]"));
        Assert.Contains("Returned", row.Text);
    }
}
