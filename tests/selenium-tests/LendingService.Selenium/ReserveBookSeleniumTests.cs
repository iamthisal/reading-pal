using OpenQA.Selenium;
using Xunit;

namespace LendingService.SeleniumTests;

public class ReserveBookSeleniumTests : LendingSeleniumTestBase
{
    // TC-RESERVE-001: Happy path — a validated user reserves an available book from Discover.
    // Expected: success banner shown, and the reservation appears pending in "My borrowings".
    [Fact]
    public void TC_RESERVE_001_UserReservesAvailableBook_ShowsSuccessAndAppearsInMyBorrowings()
    {
        var title = $"QA Reserve {Guid.NewGuid():N}";
        CreateReservableBookViaAdminUi(title);

        LoginAsUser();
        ReserveBookAsCurrentUser(title);

        Assert.Contains("Book reserved successfully", Driver.PageSource);

        NavigateTo("/my-borrowings");
        Wait.Until(d => d.PageSource.Contains("Pending reservations"));

        var pendingRow = Wait.Until(d =>
            d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault());

        Assert.NotNull(pendingRow);
        Assert.Contains("Pending", pendingRow!.Text);
    }

    // TC-RESERVE-002: Happy path — the same reservation shows up in the admin's pending queue.
    // Expected: the reservation is visible to admins immediately, awaiting accept/reject.
    [Fact]
    public void TC_RESERVE_002_ReservedBook_AppearsInAdminPendingQueue()
    {
        var title = $"QA Reserve {Guid.NewGuid():N}";
        CreateReservableBookViaAdminUi(title);

        LoginAsUser();
        ReserveBookAsCurrentUser(title);

        LoginAsAdmin();
        NavigateTo("/admin/reservations/pending");

        var row = Wait.Until(d =>
            d.FindElements(By.XPath($"//tr[.//*[contains(normalize-space(.), '{title}')]]")).FirstOrDefault());

        Assert.NotNull(row);
    }
}
