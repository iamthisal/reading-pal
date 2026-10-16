using Xunit;

namespace LendingService.SeleniumTests;

/// <summary>
/// The UI-level counterpart to the 401/403 role-boundary checks already covered by the
/// integration test suite (see tests/LendingService.IntegrationTests) — those prove the API
/// rejects the wrong role; these prove the browser never actually renders the other role's page
/// for someone who pastes/bookmarks its URL directly, regardless of what the API alone would do.
/// </summary>
public class RoleBoundarySeleniumTests : LendingSeleniumTestBase
{
    // TC-SECURITY-001: a "User"-role account can never reach admin-only content by navigating
    // straight to an admin URL; the route guard sends it to the member home page instead.
    [Fact]
    public void TC_SECURITY_001_UserRole_CannotReachAdminContentByDirectUrl()
    {
        LoginAsUser();

        Driver.Navigate().GoToUrl($"{FrontendUrl}/admin/reservations/pending");
        Wait.Until(d => !d.Url.TrimEnd('/').EndsWith("/admin/reservations/pending"));

        Assert.DoesNotContain("/admin", Driver.Url);
    }

    // TC-SECURITY-002: reverse direction — an Admin account can never reach the User-only
    // "My borrowings" page by navigating straight to its URL.
    [Fact]
    public void TC_SECURITY_002_AdminRole_CannotReachUserOnlyContentByDirectUrl()
    {
        LoginAsAdmin();

        Driver.Navigate().GoToUrl($"{FrontendUrl}/my-borrowings");
        Wait.Until(d => !d.Url.TrimEnd('/').EndsWith("/my-borrowings"));

        Assert.DoesNotContain("/my-borrowings", Driver.Url);
    }
}
