namespace UserService.E2ETests;

/// <summary>TEST-PLAN.md §5.1: signing in, signing out and sessions.</summary>
public sealed class AuthenticationTests : E2ETestBase
{
    private static readonly By ErrorMessage = By.CssSelector(".auth-form-card .error-message");

    [Fact, Trait("Id", "AUTH-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task AUTH01_ApprovedMember_SignsInThroughForm_LandsOnDiscover()
    {
        var member = await NewMemberAsync();

        Browser.SignInThroughForm(member.Email, member.Password);

        Browser.WaitForPath("/home");
        Browser.WaitForText("Discover");
        Assert.True(Browser.Exists(Aria("Account menu")));
        Assert.False(Browser.HasText("Account Pending Approval"));
    }

    [Fact, Trait("Id", "AUTH-02"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public void AUTH02_Admin_SignsInThroughForm_LandsOnAdminDashboard()
    {
        Browser.SignInThroughForm(E2EConfig.AdminEmail, E2EConfig.AdminPassword);

        Browser.WaitForPath("/admin/dashboard");
        Assert.True(Browser.Visible(Aria("Administration navigation")).Displayed);
    }

    [Fact, Trait("Id", "AUTH-03"), Trait("Priority", "P1"), Trait("Type", "Unhappy")]
    public async Task AUTH03_WrongPassword_ShowsInvalidCredentials_AndStaysOnLogin()
    {
        var member = await NewMemberAsync();
        const string wrongPassword = "Wrong-pass-999";

        Browser.SignInThroughForm(member.Email, wrongPassword);

        Assert.Equal("Invalid email or password", Browser.Visible(ErrorMessage).Text);
        Assert.Equal("/login", Browser.Path);
        Assert.Null(Browser.StoredToken);
        Assert.False(Browser.HasText(wrongPassword));
    }

    [Fact, Trait("Id", "AUTH-04"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public void AUTH04_UnknownEmail_ShowsTheSameMessageAsAWrongPassword()
    {
        Browser.SignInThroughForm($"nobody.{TestIds.Short()}@reader.test", TestIds.MemberPassword);

        Assert.Equal("Invalid email or password", Browser.Visible(ErrorMessage).Text);
        Assert.Equal("/login", Browser.Path);
    }

    [Fact, Trait("Id", "AUTH-05"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public void AUTH05_EmptyFields_AreBlockedByTheBrowser()
    {
        Browser.Go("/login");
        Browser.ClickButton("Sign in");

        Assert.NotEmpty(Browser.ValidationMessage(By.Id("email")));
        Assert.NotEmpty(Browser.ValidationMessage(By.Id("password")));
        Assert.Equal("/login", Browser.Path);
        Assert.False(Browser.Exists(ErrorMessage));
    }

    [Fact, Trait("Id", "AUTH-06"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public void AUTH06_MalformedEmail_IsBlockedByTheBrowser()
    {
        Browser.Go("/login");
        Browser.Type(By.Id("email"), "not-an-email");
        Browser.Type(By.Id("password"), TestIds.MemberPassword);
        Browser.ClickButton("Sign in");

        Assert.False(Browser.IsValid(By.Id("email")));
        Assert.Equal("/login", Browser.Path);
        Assert.Null(Browser.StoredToken);
    }

    [Fact, Trait("Id", "AUTH-07"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task AUTH07_PendingMember_SignsIn_AndSeesTheApprovalWarning()
    {
        var member = await NewPendingMemberAsync();

        Browser.SignInThroughForm(member.Email, member.Password);

        Browser.WaitForPath("/home");
        Browser.WaitForText("Account Pending Approval:");
        AssertContains("Your registration must be validated before reserving physical books.", Browser.BodyText);
    }

    [Fact, Trait("Id", "AUTH-08"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task AUTH08_RejectedMember_CannotSignIn()
    {
        var member = await NewPendingMemberAsync();
        Assert.True((await Api.RejectMemberAsync(member.Id)).Ok);

        Browser.SignInThroughForm(member.Email, member.Password);

        Assert.Equal("Invalid email or password", Browser.Visible(ErrorMessage).Text);
    }

    [Fact, Trait("Id", "AUTH-09"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task AUTH09_LogOut_ClearsTheSession_AndProtectedPagesRedirect()
    {
        var member = await NewMemberAsync();
        Browser.SignInAs(member);
        Browser.WaitForText("Discover");

        Browser.SignOut();

        Browser.WaitForPath("/login");
        Assert.Null(Browser.StoredToken);
        Browser.Driver.Navigate().Back();
        Browser.WaitForPath("/login");
        Browser.Go("/my-borrowings");
        Browser.WaitForPath("/login");
    }

    [Fact, Trait("Id", "AUTH-10"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public void AUTH10_ExpiredToken_IsTreatedAsSignedOut()
    {
        Browser.SignInWithToken(FakeJwt.Create(DateTimeOffset.UtcNow.AddMinutes(-5)), "/home");

        Browser.WaitForPath("/login");
        Assert.Null(Browser.StoredToken);
    }

    [Fact, Trait("Id", "AUTH-11"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public void AUTH11_MalformedToken_RedirectsToLoginWithoutCrashing()
    {
        Browser.SignInWithToken("garbage", "/my-borrowings");

        Browser.WaitForPath("/login");
        Assert.True(Browser.Visible(By.Id("email")).Displayed);
    }

    [Fact, Trait("Id", "AUTH-12"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public void AUTH12_TokenExpiringDuringASession_SignsTheReaderOut()
    {
        Browser.SignInWithToken(FakeJwt.Create(DateTimeOffset.UtcNow.AddSeconds(10)), "/favourites");
        Browser.WaitForPath("/favourites");

        Browser.WaitForPath("/login", TimeSpan.FromSeconds(30));
        Assert.Null(Browser.StoredToken);
    }

    [Fact, Trait("Id", "AUTH-13"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task AUTH13_RootAddress_SendsEachRoleToItsHome()
    {
        var member = await NewMemberAsync();
        Browser.SignInAs(member, "/");
        Browser.WaitForPath("/home");

        Browser.SignInAsAdmin(await AdminTokenAsync(), "/");
        Browser.WaitForPath("/admin/dashboard");
    }

    [Fact, Trait("Id", "AUTH-14"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task AUTH14_UnknownRoute_RedirectsByRole()
    {
        Browser.Go("/no-such-page");
        Browser.WaitForPath("/login");

        var member = await NewMemberAsync();
        Browser.SignInAs(member, "/no-such-page");
        Browser.WaitForPath("/home");
    }

    [Fact, Trait("Id", "AUTH-15"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task AUTH15_SessionSurvivesAReload()
    {
        var member = await NewMemberAsync();
        Browser.SignInAs(member, "/favourites");
        Browser.WaitForText("Favourites");

        Browser.Reload();

        Browser.WaitForPath("/favourites");
        Assert.True(Browser.Visible(Aria("Account menu")).Displayed);
    }
}
