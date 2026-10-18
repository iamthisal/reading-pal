namespace NotificationService.E2ETests;

/// <summary>
/// TEST-PLAN.md §5.17, the event cases: an action in one place reaches the right people through Kafka.
/// Each test triggers the action, waits for the Notification Service to store the result, then checks
/// what the recipient sees on their Notifications page.
/// </summary>
public sealed class NotificationEventTests : E2ETestBase
{
    private void ExpectOnMemberPage(Member member, string text)
    {
        Browser.SignInAs(member, "/notifications");
        Browser.UntilAfterReloads(() => Browser.HasText(text), what: $"\"{text}\" on the member's Notifications page");
    }

    private void ExpectOnAdminPage(string token, string text)
    {
        Browser.SignInAsAdmin(token, "/admin/notifications");
        Browser.UntilAfterReloads(() => Browser.HasText(text), what: $"\"{text}\" on the admin Notifications page");
    }

    private async Task<bool> HasNotificationAsync(string token, bool admin, string text) =>
        (await Api.NotificationsAsync(token, admin)).Any(item => ((string?)item!["message"] ?? "").Contains(text, StringComparison.Ordinal));

    [Fact, Trait("Id", "NOTIF-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task NOTIF01_NewReservation_IsAnnouncedToAdminsWithTheMembersName()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();

        await Api.ReserveAsync(member, book);

        ExpectOnAdminPage(await AdminTokenAsync(), $"{member.FullName} reserved '{book.Title}' on");
        Assert.NotNull(await Api.WaitForNotificationAsync(await SecondAdminTokenAsync(), true, $"reserved '{book.Title}'"));
    }

    [Fact, Trait("Id", "NOTIF-02"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task NOTIF02_Acceptance_TellsTheMemberTheDueDate()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();

        await Api.BorrowAsync(member, book);

        ExpectOnMemberPage(member, $"Your reservation for '{book.Title}' was accepted. Please return it by ");
    }

    [Fact, Trait("Id", "NOTIF-03"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task NOTIF03_Rejection_TellsOnlyTheMember()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();

        await Api.RejectReservationAsync(await Api.ReserveAsync(member, book));

        ExpectOnMemberPage(member, $"Your reservation for '{book.Title}' was rejected by the library.");
        Assert.False(await HasNotificationAsync(await AdminTokenAsync(), true, $"cancelled their pending reservation for '{book.Title}'"));
    }

    [Fact, Trait("Id", "NOTIF-04"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task NOTIF04_MemberCancelling_TellsTheMemberAndTheAdmins()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();

        await Api.CancelReservationAsync(member, await Api.ReserveAsync(member, book));

        ExpectOnMemberPage(member, $"You cancelled your reservation for '{book.Title}'.");
        ExpectOnAdminPage(await AdminTokenAsync(), $"{member.FullName} cancelled their pending reservation for '{book.Title}'.");
    }

    [Fact, Trait("Id", "NOTIF-05"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task NOTIF05_Return_ThanksTheMember()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var reservation = await Api.BorrowAsync(member, book);

        await Api.ReturnAsync(await Api.BorrowRecordIdAsync(reservation));

        ExpectOnMemberPage(member, $"'{book.Title}' was returned on ");
        AssertContains("Thank you!", Browser.BodyText);
    }

    [Fact, Trait("Id", "NOTIF-06"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task NOTIF06_LateReturn_TellsTheMemberAboutTheFine()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var reservation = await Api.BorrowAsync(member, book);
        await Database.BackdateLoanAsync(reservation, 3);

        await Api.ReturnAsync(await Api.BorrowRecordIdAsync(reservation));

        ExpectOnMemberPage(member, $"A fine of Rs. 30.00 was recorded for '{book.Title}': returned 3 days late.");
    }

    [Fact, Trait("Id", "NOTIF-07"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task NOTIF07_LoanDueInTwoDays_GetsExactlyOneReminder()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var reservation = await Api.BorrowAsync(member, book);
        await Database.BackdateLoanAsync(reservation, -2);

        // The reminder check runs when Lending starts and then hourly.
        await Containers.RestartAsync(Containers.Lending);

        ExpectOnMemberPage(member, $"Reminder: '{book.Title}' is due on ");
        AssertContains("Please return it on time to avoid a fine.", Browser.BodyText);
        await Containers.RestartAsync(Containers.Lending);
        await Task.Delay(15_000);
        var reminders = (await Api.NotificationsAsync(member.Token, false)).Count(item => ((string?)item!["message"])!.StartsWith($"Reminder: '{book.Title}'"));
        Assert.Equal(1, reminders);
    }

    [Fact, Trait("Id", "NOTIF-08"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task NOTIF08_NewBook_IsAnnouncedToMembersAndOtherAdminsButNotItsCreator()
    {
        var member = await NewMemberAsync();
        var secondAdmin = await SecondAdminTokenAsync();

        var book = await NewBookAsync();

        ExpectOnMemberPage(member, $"New in the catalogue: '{book.Title}' by {book.Author}.");
        ExpectOnAdminPage(secondAdmin, $"{E2EConfig.AdminEmail} added '{book.Title}' to the catalogue.");
        Assert.False(await HasNotificationAsync(await AdminTokenAsync(), true, $"added '{book.Title}'"));
    }

    [Fact, Trait("Id", "NOTIF-09"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task NOTIF09_BookEditsAndAvailability_AreToldToOtherAdminsOnly()
    {
        var member = await NewMemberAsync();
        var secondAdmin = await SecondAdminTokenAsync();
        var book = await NewBookAsync();

        await Api.UpdateBookAsync(book, description: "Edited by NOTIF-09.");
        await Api.MarkUnavailableAsync(book.Id);
        await Api.MarkAvailableAsync(book.Id);

        ExpectOnAdminPage(secondAdmin, $"updated '{book.Title}'.");
        Browser.UntilAfterReloads(() => Browser.HasText($"marked '{book.Title}' as unavailable.") && Browser.HasText($"marked '{book.Title}' as available."),
            what: "both availability notices");
        Assert.False(await HasNotificationAsync(member.Token, false, $"updated '{book.Title}'"));
        Assert.False(await HasNotificationAsync(member.Token, false, $"marked '{book.Title}'"));
    }

    [Fact, Trait("Id", "NOTIF-10"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task NOTIF10_DeletedBook_TellsOnlyTheMemberWaitingForIt()
    {
        var waiting = await NewMemberAsync();
        var bystander = await NewMemberAsync();
        var book = await NewBookAsync();
        await Api.ReserveAsync(waiting, book);
        // Give the reservation event time to reach the Notification Service before the deletion.
        Assert.NotNull(await Api.WaitForNotificationAsync(await AdminTokenAsync(), true, $"reserved '{book.Title}'"));

        Assert.True((await Api.DeleteBookAsync(book.Id)).Ok);

        ExpectOnMemberPage(waiting, $"'{book.Title}', a book on your reservations or borrowings, has been removed from the library catalogue.");
        Assert.False(await HasNotificationAsync(bystander.Token, false, $"'{book.Title}', a book on your reservations"));
    }

    [Fact, Trait("Id", "NOTIF-11"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task NOTIF11_MemberApprovedAfterABookWasAdded_DoesNotGetItsAnnouncement()
    {
        var earlier = await NewMemberAsync();
        var book = await NewBookAsync();
        Assert.NotNull(await Api.WaitForNotificationAsync(earlier.Token, false, $"New in the catalogue: '{book.Title}'"));

        var later = await NewMemberAsync();

        Browser.SignInAs(later, "/notifications");
        Browser.Until(() => !Browser.HasText("Loading notifications…"), what: "the notifications to load");
        Assert.False(Browser.HasText(book.Title));
        Assert.False(await HasNotificationAsync(later.Token, false, book.Title));
    }

    [Fact, Trait("Id", "NOTIF-18"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task NOTIF18_KafkaOutage_DelaysButDoesNotLoseNotifications()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var admin = await AdminTokenAsync();
        StopContainer(Containers.Kafka);

        Browser.SignInAs(member, $"/books/{book.Id}");
        Browser.Click(Aria($"Borrow {book.Title}"));
        Browser.WaitForText("Book reserved successfully!");
        await Task.Delay(10_000);
        Assert.False(await HasNotificationAsync(admin, true, $"reserved '{book.Title}'"));

        await StartContainerAsync(Containers.Kafka);

        Assert.NotNull(await Api.WaitForNotificationAsync(admin, true, $"reserved '{book.Title}'", TimeSpan.FromMinutes(3)));
        ExpectOnAdminPage(admin, $"{member.FullName} reserved '{book.Title}'");
    }

    [Fact, Trait("Id", "NOTIF-19"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task NOTIF19_NotificationServiceRestart_DoesNotDuplicateNotifications()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();

        StopContainer(Containers.Notification);
        for (var round = 0; round < 3; round++) await Api.RejectReservationAsync(await Api.ReserveAsync(member, book));
        await StartContainerAsync(Containers.Notification);
        await Containers.RestartAsync(Containers.Notification);

        var deadline = DateTime.UtcNow + E2EConfig.EventTimeout;
        int count;
        do
        {
            count = (await Api.NotificationsAsync(member.Token, false)).Count(item => ((string?)item!["message"])!.Contains($"'{book.Title}' was rejected"));
            if (count >= 3) break;
            await Task.Delay(1000);
        } while (DateTime.UtcNow < deadline);
        await Task.Delay(5000);
        count = (await Api.NotificationsAsync(member.Token, false)).Count(item => ((string?)item!["message"])!.Contains($"'{book.Title}' was rejected"));
        Assert.Equal(3, count);
    }
}
