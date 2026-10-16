using OpenQA.Selenium;
using Xunit;

namespace LendingService.SeleniumTests;

public class MyBorrowingsCancelSeleniumTests : LendingSeleniumTestBase
{
    // TC-CANCEL-001: Happy path — a user cancels their own pending reservation from
    // "My borrowings". The UI shows a native confirm() dialog before submitting the cancellation.
    // Expected: dialog accepted, success message shown, the reservation leaves the pending table
    // and appears under "Cancelled reservations" instead.
    [Fact]
    public void TC_CANCEL_001_UserCancelsOwnPendingReservation_MovesItToCancelledSection()
    {
        var title = $"QA Cancel {Guid.NewGuid():N}";
        CreateReservableBookViaAdminUi(title);

        LoginAsUser();
        ReserveBookAsCurrentUser(title);

        NavigateTo("/my-borrowings");
        var pendingRow = Wait.Until(d =>
            d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault())
            ?? throw new NoSuchElementException($"No pending row found for '{title}'.");

        pendingRow.FindElement(By.CssSelector(".my-loans-cancel")).Click();
        AcceptConfirmDialogIfPresent();

        Wait.Until(d => d.PageSource.Contains("has been cancelled"));

        // The "Pending reservations" region only renders while at least one pending row exists;
        // either way, this reservation must not be inside it any more.
        var pendingSections = Driver.FindElements(By.CssSelector("[aria-label='Pending reservations']"));
        Assert.All(pendingSections, section => Assert.DoesNotContain(title, section.Text));

        var cancelledSection = Wait.Until(d => d.FindElement(By.CssSelector("[aria-label='Cancelled reservation history']")));
        Assert.Contains(title, cancelledSection.Text);
    }
}
