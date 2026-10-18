namespace UserService.E2ETests;

/// <summary>TEST-PLAN.md §5.2: registering a member.</summary>
public sealed class RegistrationTests : E2ETestBase
{
    private static readonly By ErrorMessage = By.CssSelector(".auth-form-card .error-message");

    private string FillRegistration(string firstName = "Reader", string lastName = "Tester", string? email = null, string password = TestIds.MemberPassword)
    {
        email ??= $"e2e.{TestIds.Short()}@reader.test";
        Browser.Go("/register");
        Browser.Type(By.Id("firstName"), firstName);
        Browser.Type(By.Id("lastName"), lastName);
        Browser.Type(By.Id("email"), email);
        Browser.Type(By.Id("password"), password);
        Browser.ClickButton("Register Account");
        return email;
    }

    [Fact, Trait("Id", "REG-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public void REG01_NewMember_Registers_SeesSuccess_AndIsSentToSignIn()
    {
        var email = FillRegistration();
        TrackMember(email);

        Browser.WaitForText("Registration Successful!");
        Assert.True(Browser.Visible(Browser.ButtonByText("Sign in now")).Displayed);
        Browser.WaitForPath("/login", TimeSpan.FromSeconds(8));
    }

    [Fact, Trait("Id", "REG-02"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task REG02_NewRegistration_AppearsInTheAdminApprovalQueue()
    {
        var email = FillRegistration(lastName: "Queue");
        TrackMember(email);
        Browser.WaitForText("Registration Successful!");

        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/users/pending");

        var row = Browser.Visible(Browser.RowContaining(email));
        AssertContains("Pending", row.Text);
    }

    [Fact, Trait("Id", "REG-03"), Trait("Priority", "P1"), Trait("Type", "Unhappy")]
    public async Task REG03_DuplicateEmail_IsRefused()
    {
        var existing = await NewMemberAsync();

        FillRegistration(email: existing.Email);

        Assert.Equal("Email already in use", Browser.Visible(ErrorMessage).Text);
        Assert.Equal("/register", Browser.Path);
    }

    [Fact, Trait("Id", "REG-04"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task REG04_PasswordShorterThanSix_IsRefusedByTheBrowserAndTheServer()
    {
        var email = FillRegistration(password: "abc12");
        TrackMember(email);

        Assert.False(Browser.IsValid(By.Id("password")));
        Assert.False(Browser.HasText("Registration Successful!"));

        // The server enforces the same rule when the browser check is bypassed.
        var direct = await Api.RegisterAsync("Reader", "Tester", email, "abc12");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, direct.Status);
    }

    [Theory, Trait("Id", "REG-05"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    [InlineData("firstName")]
    [InlineData("lastName")]
    [InlineData("email")]
    [InlineData("password")]
    public void REG05_MissingRequiredField_BlocksTheSubmit(string emptyField)
    {
        Browser.Go("/register");
        foreach (var (id, value) in new[] { ("firstName", "Reader"), ("lastName", "Tester"), ("email", $"e2e.{TestIds.Short()}@reader.test"), ("password", TestIds.MemberPassword) })
            Browser.Type(By.Id(id), id == emptyField ? "" : value);
        Browser.ClickButton("Register Account");

        Assert.NotEmpty(Browser.ValidationMessage(By.Id(emptyField)));
        Assert.False(Browser.HasText("Registration Successful!"));
    }

    [Fact, Trait("Id", "REG-06"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public void REG06_InvalidEmailFormat_IsBlocked()
    {
        FillRegistration(email: "a@");

        Assert.False(Browser.IsValid(By.Id("email")));
        Assert.False(Browser.HasText("Registration Successful!"));
    }

    [Fact, Trait("Id", "REG-07"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task REG07_BuiltInAdminAddress_CanBeRegistered_ButStillSignsInAsAdmin()
    {
        // Finding 6 in TEST-PLAN.md §10: the built-in admin is not a database user, so its address is free.
        await Api.DeleteMemberAsync(E2EConfig.AdminEmail);
        TrackMember(E2EConfig.AdminEmail);

        FillRegistration(email: E2EConfig.AdminEmail, password: "Member-pass-123");

        Browser.WaitForText("Registration Successful!");
        Browser.SignInThroughForm(E2EConfig.AdminEmail, E2EConfig.AdminPassword);
        Browser.WaitForPath("/admin/dashboard");
    }

    [Fact, Trait("Id", "REG-08"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public void REG08_LinksBetweenSignInAndRegister_Work()
    {
        Browser.Go("/register");
        Browser.ClickButton("Sign in here");
        Browser.WaitForPath("/login");

        Browser.ClickButton("Register here");
        Browser.WaitForPath("/register");
    }

    [Fact, Trait("Id", "REG-09"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public void REG09_RegisterPage_MatchesTheLoginLayout_AndStacksNamesOnAPhone()
    {
        Browser.Go("/login");
        Assert.True(Browser.Visible(By.CssSelector(".auth-page .auth-showcase")).Displayed);
        Assert.True(Browser.Exists(By.CssSelector(".auth-page .auth-form-card")));

        Browser.Go("/register");
        Assert.True(Browser.Visible(By.CssSelector(".auth-page .auth-showcase")).Displayed);
        var first = Browser.Visible(By.Id("firstName"));
        var last = Browser.Visible(By.Id("lastName"));
        // Side by side: the same row, give or take sub-pixel rounding.
        Assert.InRange(Math.Abs(first.Location.Y - last.Location.Y), 0, 2);
        Assert.True(last.Location.X > first.Location.X);

        Browser.Resize(390, 844);
        Browser.Until(() => Browser.Find(By.Id("lastName")).Location.Y > Browser.Find(By.Id("firstName")).Location.Y,
            what: "the last name to move under the first name");
        Assert.False(Browser.HasHorizontalScroll());
    }
}
