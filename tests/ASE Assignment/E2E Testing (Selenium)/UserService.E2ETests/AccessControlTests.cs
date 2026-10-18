namespace UserService.E2ETests;

/// <summary>TEST-PLAN.md §5.3: role-based routes and navigation.</summary>
public sealed class AccessControlTests : E2ETestBase
{
    private static readonly string[] AdminRoutes =
    [
        "/admin/dashboard", "/admin/users/pending", "/admin/users/active", "/admin/books", "/admin/reservations/pending",
        "/admin/borrowed", "/admin/reservations/history", "/admin/notifications", "/admin/authors", "/admin/lists"
    ];

    [Theory, Trait("Id", "RBAC-01"), Trait("Priority", "P1"), Trait("Type", "Unhappy")]
    [InlineData("/home")]
    [InlineData("/search")]
    [InlineData("/books/1")]
    [InlineData("/my-borrowings")]
    [InlineData("/admin/books")]
    public void RBAC01_SignedOutVisitor_IsSentToLogin(string path)
    {
        Browser.Go(path);

        Browser.WaitForPath("/login");
    }

    [Fact, Trait("Id", "RBAC-02"), Trait("Priority", "P1"), Trait("Type", "Unhappy")]
    public async Task RBAC02_Member_CannotOpenAnyAdminPage()
    {
        var member = await NewMemberAsync();
        Browser.SignInAs(member);

        foreach (var route in AdminRoutes)
        {
            Browser.Go(route);
            Browser.WaitForPath("/home");
            Assert.False(Browser.Exists(Aria("Administration navigation")), $"{route} showed admin navigation to a member.");
        }
    }

    [Fact, Trait("Id", "RBAC-03"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task RBAC03_Admin_IsSentAwayFromMemberOnlyPages()
    {
        Browser.SignInAsAdmin(await AdminTokenAsync());

        foreach (var route in new[] { "/profile", "/my-borrowings", "/notifications" })
        {
            Browser.Go(route);
            Browser.WaitForPath("/admin/dashboard");
        }
    }

    [Fact, Trait("Id", "RBAC-04"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task RBAC04_Admin_CanOpenThePagesSharedWithMembers()
    {
        var book = await NewBookAsync();
        Browser.SignInAsAdmin(await AdminTokenAsync());

        foreach (var route in new[] { "/home", "/search", $"/books/{book.Id}", $"/authors/{Uri.EscapeDataString(book.Author)}", "/favourites" })
        {
            Browser.Go(route);
            Browser.Until(() => Browser.Path == new Uri(E2EConfig.FrontendUrl + route).AbsolutePath, what: $"{route} to stay open");
            Assert.True(Browser.Visible(Aria("Library navigation")).Displayed);
        }
        Browser.Go($"/books/{book.Id}");
        Browser.WaitForText(book.Title);
    }

    [Fact, Trait("Id", "RBAC-05"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task RBAC05_EachRole_SeesItsOwnNavigation()
    {
        var member = await NewMemberAsync();
        Browser.SignInAs(member);
        var memberNav = Browser.Visible(Aria("Library navigation")).Text;
        foreach (var label in new[] { "Discover", "Search", "Favourites", "My borrowings", "Notifications", "My profile" })
            AssertContains(label, memberNav);
        Assert.DoesNotContain("Admin dashboard", memberNav);

        Browser.SignInAsAdmin(await AdminTokenAsync());
        var adminNav = Browser.Visible(Aria("Administration navigation")).Text;
        foreach (var label in new[] { "Overview", "User approvals", "Active users", "Book inventory", "Authors", "Curated lists",
                     "Pending reservations", "Borrowed books", "Reservation history", "Notifications" })
            AssertContains(label, adminNav);
    }

    [Fact, Trait("Id", "RBAC-06"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task RBAC06_MemberToken_IsRefusedByAnAdminApi()
    {
        var member = await NewMemberAsync();
        Browser.SignInAs(member);

        // Calls the admin endpoint from the page with the member's own stored token.
        var status = Convert.ToInt32(((IJavaScriptExecutor)Browser.Driver).ExecuteAsyncScript(
            "const done = arguments[arguments.length - 1];" +
            "fetch(arguments[0] + '/api/books', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token') }," +
            " body: JSON.stringify({ title: 'Should not exist', author: 'x', isbn: 'E2E-RBAC-06', genre: 'x', totalCopies: 1 }) })" +
            ".then(r => done(r.status)).catch(() => done(0));", E2EConfig.InventoryServiceUrl));

        Assert.Equal(403, status);
        var search = await Api.SendAsync(HttpMethod.Get, $"{E2EConfig.InventoryServiceUrl}/api/books/search?q=E2E-RBAC-06");
        Assert.Equal(0, search.Body!["total"]!.GetValue<int>());
    }
}
