using OpenQA.Selenium;
using Xunit;

namespace LendingService.SeleniumTests;

/// <summary>
/// Only ReturnFineCalculator itself is unit-tested (tests/LendingService.Tests) and the on-time
/// path is Selenium-tested (AdminReturnSeleniumTests, "No fine is due") — nothing ever actually
/// looks at a fine rendered on screen. The due date is hardcoded to checkout + 14 days
/// (ReservationsController.cs:232) with no admin override, so the only way to reach an overdue
/// loan without waiting 14 real days is to backdate it directly in the database after checkout —
/// see LendingSeleniumTestBase.BackdateActiveLoanDueDateAsync. This only runs against the local
/// docker-compose stack (needs a DB connection the deployed site doesn't expose).
/// </summary>
public class ReturnFineSeleniumTests : LendingSeleniumTestBase
{
    private IWebElement WaitForRow(string title) =>
        Wait.Until(d => d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault())
        ?? throw new NoSuchElementException($"No row found for '{title}'.");

    // TC-FINE-001: a book returned 5 days overdue shows the correct fine (5 x Rs. 10/day = Rs.
    // 50.00) both live on the Borrowed books table before return, and in the success message and
    // Reservation history after it.
    [Fact]
    public async Task TC_FINE_001_OverdueReturn_ShowsCorrectFineAmount()
    {
        var title = $"QA Fine {Guid.NewGuid():N}";
        var isbn = CreateReservableBookViaAdminUi(title);

        LoginAsUser();
        ReserveBookAsCurrentUser(title);

        LoginAsAdmin();
        NavigateTo("/admin/reservations/pending");
        WaitForRow(title).FindElement(By.CssSelector(".reservation-accept-button:not(.reservation-reject-button)")).Click();
        Wait.Until(d => d.PageSource.Contains("is now borrowed by"));

        var adminToken = await GetAdminApiTokenAsync();
        var bookId = await GetBookIdByIsbnAsync(isbn, adminToken);
        await BackdateActiveLoanDueDateAsync(bookId, daysAgo: 5);

        // Loaded after the backdate, so the overdue days and fine come from the new due date.
        NavigateTo("/admin/borrowed");
        var row = Wait.Until(d =>
        {
            var found = d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault();
            return found != null && found.Text.Contains("50.00") ? found : null;
        }) ?? throw new NoSuchElementException($"Borrowed-books row for '{title}' never showed the expected fine.");

        Assert.Contains("5", row.Text);
        Assert.Contains("50.00", row.Text);

        row.FindElement(By.CssSelector("button.btn-primary")).Click();
        AcceptConfirmDialogIfPresent();

        Wait.Until(d => d.PageSource.Contains("Fine: Rs. 50.00"));
        Assert.Contains("5 day(s) overdue", Driver.PageSource);

        NavigateTo("/admin/reservations/history");
        var historyRow = Wait.Until(d =>
            d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault());
        Assert.NotNull(historyRow);
        Assert.Contains("Returned", historyRow!.Text);
        Assert.Contains("50.00", historyRow.Text);
    }
}
