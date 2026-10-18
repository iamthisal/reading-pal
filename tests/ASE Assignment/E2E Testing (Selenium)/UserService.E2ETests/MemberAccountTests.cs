namespace UserService.E2ETests;

/// <summary>TEST-PLAN.md §5.4: admins approving, rejecting and revoking members.</summary>
public sealed class MemberAccountTests : E2ETestBase
{
    /// <summary>Opens the member's details dialog from a user list by clicking their name.</summary>
    private static void OpenMember(Browser browser, Member member)
    {
        var row = browser.Visible(Browser.RowContaining(member.Email));
        browser.Click(row.FindElement(By.XPath("./td[2]")));
        browser.Until(() => browser.OpenDialog != null, what: "the member dialog");
    }

    [Fact, Trait("Id", "ACC-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task ACC01_Admin_ApprovesAPendingMember_WhoMovesToActiveUsers()
    {
        var member = await NewPendingMemberAsync();
        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/users/pending");

        OpenMember(Browser, member);
        Browser.ClickButton("Accept User");

        Browser.Until(() => !Browser.Exists(Browser.RowContaining(member.Email)), what: "the member to leave the pending list");
        Browser.Go("/admin/users/active");
        AssertContains("Validated", Browser.Visible(Browser.RowContaining(member.Email)).Text);
    }

    [Fact, Trait("Id", "ACC-02"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task ACC02_ApprovedMember_CanBorrowAfterSigningInAgain()
    {
        var member = await NewPendingMemberAsync();
        var book = await NewBookAsync();
        await Api.ApproveMemberAsync(member.Id);

        Browser.SignInThroughForm(member.Email, member.Password);
        Browser.WaitForPath("/home");
        Assert.False(Browser.HasText("Account Pending Approval"));
        Browser.Go($"/books/{book.Id}");

        Assert.True(Browser.Visible(Aria($"Borrow {book.Title}")).Enabled);
    }

    [Fact, Trait("Id", "ACC-03"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ACC03_Admin_RejectsAPendingMember_WhoCanNoLongerSignIn()
    {
        var member = await NewPendingMemberAsync();
        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/users/pending");

        OpenMember(Browser, member);
        Browser.ClickButton("Reject");

        Browser.Until(() => !Browser.Exists(Browser.RowContaining(member.Email)), what: "the member to leave the pending list");
        Browser.Go("/admin/users/active");
        Browser.WaitForText("Validated members");
        Assert.False(Browser.Exists(Browser.RowContaining(member.Email)));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await Api.LoginAsync(member.Email, member.Password)).Status);
    }

    [Fact, Trait("Id", "ACC-04"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ACC04_Admin_RevokesAnActiveMember_WhoMovesBackToPending()
    {
        var member = await NewMemberAsync();
        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/users/active");

        OpenMember(Browser, member);
        Browser.ClickButton("Revoke Access");
        AssertContains("revoke access", Browser.AcceptAlert());

        // A failure is reported with a second browser dialog; the requirement is that none appears.
        string? failure = null;
        Browser.Until(() =>
        {
            try { return (failure = AlertText()) != null || !Browser.Exists(Browser.RowContaining(member.Email)); }
            catch (UnhandledAlertException) { return (failure = AlertText()) != null; }
        }, what: "the member to leave the active list");
        if (failure != null) Browser.AcceptAlert();
        Assert.True(failure == null, $"Revoking showed \"{failure}\" instead of moving the member.");
        Assert.NotNull(await Api.PendingUserIdAsync(member.Email));
    }

    private string? AlertText()
    {
        try { return Browser.Driver.SwitchTo().Alert().Text; }
        catch (NoAlertPresentException) { return null; }
    }

    [Fact, Trait("Id", "ACC-05"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ACC05_CancellingTheRevokeDialog_ChangesNothing()
    {
        var member = await NewMemberAsync();
        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/users/active");

        OpenMember(Browser, member);
        Browser.ClickButton("Revoke Access");
        Browser.DismissAlert();

        Assert.NotNull(Browser.OpenDialog);
        Assert.NotNull(await Api.ActiveUserIdAsync(member.Email));
    }

    [Fact, Trait("Id", "ACC-06"), Trait("Priority", "P1"), Trait("Type", "Unhappy")]
    public async Task ACC06_RevokedMemberWithAnOldSession_IsRefusedWhenBorrowing()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member, $"/books/{book.Id}");
        var borrow = Browser.Visible(Aria($"Borrow {book.Title}"));
        Assert.True((await Api.RevokeMemberAsync(member.Id)).Ok);

        Browser.Click(borrow);

        Browser.WaitForText("Your account is not approved to reserve books. Please contact the library.");
        Assert.DoesNotContain(await Api.PendingReservationsAsync(), item => item!["bookId"]!.GetValue<int>() == book.Id);
    }

    [Fact, Trait("Id", "ACC-07"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ACC07_RevokedMember_SigningInAgain_SeesTheWarningAndNoBorrowButton()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Assert.True((await Api.RevokeMemberAsync(member.Id)).Ok);

        Browser.SignInThroughForm(member.Email, member.Password);
        Browser.WaitForText("Account Pending Approval:");
        Browser.Go($"/books/{book.Id}");

        Browser.WaitForText("You can borrow once an admin has approved your account.");
        Assert.False(Browser.Exists(Aria($"Borrow {book.Title}")));
    }

    [Fact, Trait("Id", "ACC-08"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ACC08_SecondAdminRejectingAnAlreadyApprovedMember_IsRefused()
    {
        var member = await NewPendingMemberAsync();
        var token = await AdminTokenAsync();
        var other = OpenBrowser();
        Browser.SignInAsAdmin(token, "/admin/users/pending");
        other.SignInAsAdmin(token, "/admin/users/pending");
        OpenMember(Browser, member);
        OpenMember(other, member);

        Browser.ClickButton("Accept User");
        Browser.Until(() => !Browser.Exists(Browser.RowContaining(member.Email)), what: "the first admin's accept");
        other.ClickButton("Reject");

        Assert.Equal("Failed to reject user. Please try again.", other.AcceptAlert());
        Assert.NotNull(await Api.ActiveUserIdAsync(member.Email));
    }

    [Fact, Trait("Id", "ACC-09"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task ACC09_AdminAccounts_AreNeverListedAsMembers()
    {
        await SecondAdminTokenAsync();
        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/users/active");
        Browser.WaitForText("Validated members");

        foreach (var page in new[] { "/admin/users/active", "/admin/users/pending" })
        {
            Browser.Go(page);
            Browser.Until(() => !Browser.HasText("Loading users..."), what: "the user list");
            Assert.False(Browser.HasText(E2EConfig.AdminEmail), $"{page} lists the built-in admin.");
            Assert.False(Browser.HasText(SecondAdminAddress), $"{page} lists a database admin.");
        }
    }
}
