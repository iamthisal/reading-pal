using System.Net;

namespace UserService.E2ETests;

/// <summary>TEST-PLAN.md §5.5: a member's own profile.</summary>
public sealed class ProfileTests : E2ETestBase
{
    private const string Success = "Profile updated successfully!";

    private void OpenProfile(Member member)
    {
        Browser.SignInAs(member, "/profile");
        Browser.Until(() => Browser.Find(By.Id("email")).GetAttribute("value") == member.Email, what: "the profile to load");
    }

    private void Save() => Browser.ClickButton("Save Changes");

    [Fact, Trait("Id", "PROF-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task PROF01_Profile_ShowsTheMembersCurrentDetails()
    {
        var member = await NewMemberAsync("Profile");

        OpenProfile(member);

        Assert.Equal(member.FirstName, Browser.Find(By.Id("firstName")).GetAttribute("value"));
        Assert.Equal(member.LastName, Browser.Find(By.Id("lastName")).GetAttribute("value"));
        Assert.Equal("", Browser.Find(By.Id("password")).GetAttribute("value"));
    }

    [Fact, Trait("Id", "PROF-02"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task PROF02_UpdatingNames_IsSavedAndKeptAfterAReload()
    {
        var member = await NewMemberAsync();
        OpenProfile(member);

        Browser.Type(By.Id("firstName"), "Renamed");
        Browser.Type(By.Id("lastName"), "Member");
        Save();

        Browser.WaitForText(Success);
        Browser.Reload();
        Browser.Until(() => Browser.Find(By.Id("firstName")).GetAttribute("value") == "Renamed", what: "the saved first name");
        Assert.Equal("Member", Browser.Find(By.Id("lastName")).GetAttribute("value"));
    }

    [Fact, Trait("Id", "PROF-03"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task PROF03_ChangingThePassword_ReplacesTheOldOne()
    {
        var member = await NewMemberAsync();
        OpenProfile(member);
        const string newPassword = "New-pass-456";

        Browser.Type(By.Id("password"), newPassword);
        Save();

        Browser.WaitForText(Success);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.LoginAsync(member.Email, member.Password)).Status);
        Assert.True((await Api.LoginAsync(member.Email, newPassword)).Ok);
    }

    [Fact, Trait("Id", "PROF-04"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task PROF04_SavingWithoutAPassword_KeepsTheOldPassword()
    {
        var member = await NewMemberAsync();
        OpenProfile(member);

        Browser.Type(By.Id("lastName"), "KeptPassword");
        Save();

        Browser.WaitForText(Success);
        Assert.True((await Api.LoginAsync(member.Email, member.Password)).Ok);
        Assert.Equal("KeptPassword", (string?)(await Api.ProfileAsync(member.Token))["lastName"]);
    }

    [Fact, Trait("Id", "PROF-05"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task PROF05_EmailOfAnotherMember_IsRefused()
    {
        var member = await NewMemberAsync();
        var other = await NewMemberAsync();
        OpenProfile(member);

        Browser.Type(By.Id("email"), other.Email);
        Save();

        Browser.WaitForText("Email already in use");
        Assert.Equal(member.Email, (string?)(await Api.ProfileAsync(member.Token))["email"]);
    }

    [Theory, Trait("Id", "PROF-06"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    [InlineData("email", "x@")]
    [InlineData("firstName", "")]
    [InlineData("password", "abc")]
    public async Task PROF06_InvalidField_IsBlockedByTheBrowser(string field, string value)
    {
        var member = await NewMemberAsync();
        OpenProfile(member);

        Browser.Type(By.Id(field), value);
        Save();

        Assert.False(Browser.IsValid(By.Id(field)));
        Assert.False(Browser.HasText(Success));
    }

    [Fact, Trait("Id", "PROF-07"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task PROF07_ChangingTheEmail_MovesTheSignInToTheNewAddress()
    {
        var member = await NewMemberAsync();
        var newEmail = $"e2e.{TestIds.Short()}@renamed.test";
        TrackMember(newEmail);
        OpenProfile(member);

        Browser.Type(By.Id("email"), newEmail);
        Save();

        Browser.WaitForText(Success);
        Assert.True((await Api.LoginAsync(newEmail, member.Password)).Ok);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.LoginAsync(member.Email, member.Password)).Status);
    }
}
