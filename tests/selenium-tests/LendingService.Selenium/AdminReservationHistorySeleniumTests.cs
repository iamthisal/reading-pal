using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace LendingService.SeleniumTests;

/// <summary>
/// Covers the Reservation history status filter in isolation. Unlike the other suites here, this
/// one is read-only against whatever history already exists in the environment — it doesn't need
/// to create its own reservation first, since it only asserts that the filter narrows whatever is
/// shown rather than asserting on a specific row.
/// </summary>
public class AdminReservationHistorySeleniumTests : LendingSeleniumTestBase
{
    private void SelectStatusFilter(string status)
    {
        var dropdown = Wait.Until(d => d.FindElement(By.XPath("//label[contains(., 'Status')]/select")));
        new SelectElement(dropdown).SelectByText(status);
    }

    private void AssertEveryVisibleRowHasStatus(string status)
    {
        // The filter is client-side React state (SelectElement.SelectByText only dispatches the
        // native 'change' event); the table doesn't re-render until that state update commits,
        // which isn't guaranteed to have happened by the time the very next Selenium command
        // runs. Wait for the DOM to actually reach one of the two valid end states before
        // asserting, instead of checking immediately.
        Wait.Until(d =>
        {
            if (d.FindElements(By.XPath("//*[contains(text(),'No records match this status')]")).Any())
                return true;
            var rows = d.FindElements(By.CssSelector("table.admin-reservations-table tbody .borrow-status"));
            return rows.Any() && rows.All(badge => badge.Text == status);
        });

        // Either every visible status badge matches the filter, or the empty-state message is shown.
        var noMatches = Driver.FindElements(By.XPath("//*[contains(text(),'No records match this status')]")).Any();
        if (noMatches) return;

        var badges = Driver.FindElements(By.CssSelector("table.admin-reservations-table tbody .borrow-status"));
        Assert.NotEmpty(badges);
        Assert.All(badges, badge => Assert.Equal(status, badge.Text));
    }

    // TC-HISTORY-001: Happy path — filtering Reservation history to "Cancelled" only shows
    // cancelled records (or the empty-state message when there are none).
    [Fact]
    public void TC_HISTORY_001_FilteringByCancelled_OnlyShowsCancelledRecords()
    {
        LoginAsAdmin();
        NavigateTo("/admin/reservations/history");
        Wait.Until(d => d.PageSource.Contains("Past records"));

        SelectStatusFilter("Cancelled");

        AssertEveryVisibleRowHasStatus("Cancelled");
    }

    // TC-HISTORY-002: Happy path — filtering Reservation history to "Returned" only shows
    // returned records (or the empty-state message when there are none).
    [Fact]
    public void TC_HISTORY_002_FilteringByReturned_OnlyShowsReturnedRecords()
    {
        LoginAsAdmin();
        NavigateTo("/admin/reservations/history");
        Wait.Until(d => d.PageSource.Contains("Past records"));

        SelectStatusFilter("Returned");

        AssertEveryVisibleRowHasStatus("Returned");
    }
}
