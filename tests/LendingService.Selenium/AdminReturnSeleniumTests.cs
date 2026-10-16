using OpenQA.Selenium;
using Xunit;

namespace LendingService.SeleniumTests;

public class AdminReturnSeleniumTests : LendingSeleniumTestBase
{
    private IWebElement WaitForRow(string title) =>
        Wait.Until(d => d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault())
        ?? throw new NoSuchElementException($"No row found for '{title}'.");

    /// <summary>Reserves a fresh book as the regular user, then accepts it as admin so it becomes an active loan on Borrowed books.</summary>
    private void CreateActiveLoan(string title)
    {
        CreateReservableBookViaAdminUi(title);
        LoginAsUser();
        ReserveBookAsCurrentUser(title);

        LoginAsAdmin();
        NavigateTo("/admin/reservations/pending");
        WaitForRow(title).FindElement(By.CssSelector(".reservation-accept-button:not(.reservation-reject-button)")).Click();
        Wait.Until(d => d.PageSource.Contains("is now borrowed by"));

        NavigateTo("/admin/borrowed");
    }

    // TC-RETURN-001: Happy path — admin returns a book on time (checked out moments ago, due in
    // 14 days). The UI shows a native confirm() dialog before submitting the return.
    // Expected: dialog accepted, "No fine is due" success message, row leaves Borrowed books, and
    // it appears in Reservation history as Returned with no fine.
    [Fact]
    public void TC_RETURN_001_AdminReturnsOnTimeLoan_ShowsNoFineAndMovesItToHistory()
    {
        var title = $"QA Return {Guid.NewGuid():N}";
        CreateActiveLoan(title);

        var row = WaitForRow(title);
        row.FindElement(By.CssSelector("button.btn-primary")).Click();
        AcceptConfirmDialogIfPresent();

        Wait.Until(d => d.PageSource.Contains("No fine is due"));
        Wait.Until(d => !d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).Any());

        NavigateTo("/admin/reservations/history");
        var historyRow = Wait.Until(d =>
            d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault());
        Assert.NotNull(historyRow);
        Assert.Contains("Returned", historyRow!.Text);
    }
}
