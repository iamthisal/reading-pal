using OpenQA.Selenium;
using Xunit;

namespace LendingService.SeleniumTests;

public class AdminAcceptRejectSeleniumTests : LendingSeleniumTestBase
{
    private void ReserveFreshBookAndOpenAdminPendingQueue(string title)
    {
        CreateReservableBookViaAdminUi(title);
        LoginAsUser();
        ReserveBookAsCurrentUser(title);

        LoginAsAdmin();
        NavigateTo("/admin/reservations/pending");
    }

    private IWebElement WaitForRow(string title) =>
        Wait.Until(d => d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault())
        ?? throw new NoSuchElementException($"No pending-queue row found for '{title}'.");

    // TC-ACCEPT-001: Happy path — admin accepts a pending reservation.
    // Expected: success message shown, the row leaves the pending queue, and the loan shows up
    // in Borrowed books.
    [Fact]
    public void TC_ACCEPT_001_AdminAcceptsPendingReservation_MovesItToBorrowedBooks()
    {
        var title = $"QA Accept {Guid.NewGuid():N}";
        ReserveFreshBookAndOpenAdminPendingQueue(title);

        var row = WaitForRow(title);
        row.FindElement(By.CssSelector(".reservation-accept-button:not(.reservation-reject-button)")).Click();

        Wait.Until(d => d.PageSource.Contains("is now borrowed by"));
        Wait.Until(d => !d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).Any());

        NavigateTo("/admin/borrowed");
        var borrowedRow = Wait.Until(d =>
            d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault());
        Assert.NotNull(borrowedRow);
        Assert.Contains("Borrowed", borrowedRow!.Text);
    }

    // TC-REJECT-001: Happy path — admin rejects a pending reservation.
    // Expected: success message shown, the row leaves the pending queue, and it appears in
    // Reservation history as Cancelled.
    [Fact]
    public void TC_REJECT_001_AdminRejectsPendingReservation_MovesItToHistoryAsCancelled()
    {
        var title = $"QA Reject {Guid.NewGuid():N}";
        ReserveFreshBookAndOpenAdminPendingQueue(title);

        var row = WaitForRow(title);
        row.FindElement(By.CssSelector(".reservation-reject-button")).Click();

        Wait.Until(d => d.PageSource.Contains("was cancelled"));
        Wait.Until(d => !d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).Any());

        NavigateTo("/admin/reservations/history");
        var historyRow = Wait.Until(d =>
            d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault());
        Assert.NotNull(historyRow);
        Assert.Contains("Cancelled", historyRow!.Text);
    }
}
