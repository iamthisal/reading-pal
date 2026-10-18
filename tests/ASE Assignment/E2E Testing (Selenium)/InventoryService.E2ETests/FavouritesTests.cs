namespace InventoryService.E2ETests;

/// <summary>TEST-PLAN.md §5.9: favourites.</summary>
public sealed class FavouritesTests : E2ETestBase
{
    private IReadOnlyList<string> FavouriteTitles() =>
        Browser.Driver.FindElements(By.CssSelector("ul[aria-label='Favourite books'] h2")).Select(heading => heading.Text).ToList();

    private void OpenFavourites()
    {
        Browser.Go("/favourites");
        Browser.Until(() => Browser.Exists(By.CssSelector("ul[aria-label='Favourite books']")) || Browser.HasText("No favourites yet")
            || Browser.Exists(By.CssSelector(".favourites-error")), what: "the favourites to load");
    }

    [Fact, Trait("Id", "FAV-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task FAV01_Heart_AddsTheBookToFavourites()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member, $"/search?q={Uri.EscapeDataString(book.Title)}");

        Browser.Click(Aria($"Add {book.Title} to favourites"));

        Browser.Until(() => Browser.Find(Aria($"Remove {book.Title} from favourites")).GetAttribute("aria-pressed") == "true", what: "the heart to fill");
        OpenFavourites();
        Assert.Contains(book.Title, FavouriteTitles());
    }

    [Fact, Trait("Id", "FAV-02"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task FAV02_Heart_RemovesTheBook_FromACardAndFromTheFavouritesPage()
    {
        var member = await NewMemberAsync();
        var fromCard = await NewBookAsync();
        var fromPage = await NewBookAsync();
        await Api.AddFavouriteAsync(member.Token, fromCard.Id);
        await Api.AddFavouriteAsync(member.Token, fromPage.Id);

        Browser.SignInAs(member, $"/search?q={Uri.EscapeDataString(fromCard.Title)}");
        Browser.Click(Aria($"Remove {fromCard.Title} from favourites"));
        Browser.Until(() => Browser.Exists(Aria($"Add {fromCard.Title} to favourites")), what: "the heart to clear");

        OpenFavourites();
        Assert.DoesNotContain(fromCard.Title, FavouriteTitles());
        Browser.Click(Aria($"Remove {fromPage.Title} from favourites"));
        Browser.Until(() => !FavouriteTitles().Contains(fromPage.Title), what: "the book to leave the list");
        Assert.Empty(await Api.FavouriteIdsAsync(member.Token));
    }

    [Fact, Trait("Id", "FAV-03"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task FAV03_Favourites_AreKeptAcrossSessions()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member, $"/books/{book.Id}");
        Browser.ClickButton("Add to favourites");
        Browser.Until(() => Browser.HasText("In your favourites"), what: "the favourite to save");

        Browser.SignOut();
        Browser.WaitForPath("/login");
        Browser.SignInThroughForm(member.Email, member.Password);
        Browser.WaitForPath("/home");

        OpenFavourites();
        Assert.Contains(book.Title, FavouriteTitles());
    }

    [Fact, Trait("Id", "FAV-04"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task FAV04_Favourites_BelongToOneMember()
    {
        var first = await NewMemberAsync();
        var second = await NewMemberAsync();
        var book = await NewBookAsync();
        await Api.AddFavouriteAsync(first.Token, book.Id);

        Browser.SignInAs(second, "/favourites");

        Browser.WaitForText("No favourites yet");
        Assert.DoesNotContain(book.Title, FavouriteTitles());
    }

    [Fact, Trait("Id", "FAV-05"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task FAV05_NoFavourites_ShowsTheEmptyStateWithALinkToBrowse()
    {
        var member = await NewMemberAsync();
        Browser.SignInAs(member, "/favourites");

        Browser.WaitForText("No favourites yet");
        AssertContains("Press the heart on any book to keep it here for later.", Browser.BodyText);
        Browser.ClickButton("Browse the catalogue");
        Browser.WaitForPath("/home");
    }

    [Fact, Trait("Id", "FAV-06"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task FAV06_Admin_CanKeepFavouritesToo()
    {
        var book = await NewBookAsync();
        Browser.SignInAsAdmin(await AdminTokenAsync(), $"/books/{book.Id}");

        Browser.ClickButton("Add to favourites");
        Browser.Until(() => Browser.HasText("In your favourites"), what: "the admin's favourite to save");

        OpenFavourites();
        Assert.Contains(book.Title, FavouriteTitles());
        Browser.Click(Aria($"Remove {book.Title} from favourites"));
        Browser.Until(() => !FavouriteTitles().Contains(book.Title), what: "the admin's favourite to clear");
    }

    [Fact, Trait("Id", "FAV-07"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task FAV07_DeletedFavouriteBook_DisappearsWithoutABrokenCard()
    {
        var member = await NewMemberAsync();
        var kept = await NewBookAsync();
        var removed = await NewBookAsync();
        await Api.AddFavouriteAsync(member.Token, kept.Id);
        await Api.AddFavouriteAsync(member.Token, removed.Id);
        Assert.True((await Api.DeleteBookAsync(removed.Id)).Ok);

        Browser.SignInAs(member, "/favourites");
        OpenFavourites();

        Assert.Equal([kept.Title], FavouriteTitles());
        Assert.False(Browser.Exists(By.CssSelector(".favourites-error")));
    }

    [Fact, Trait("Id", "FAV-08"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task FAV08_InventoryDown_ShowsTheFavouritesError()
    {
        var member = await NewMemberAsync();
        StopContainer(Containers.Inventory);

        Browser.SignInAs(member, "/favourites");

        Browser.WaitForText("Unable to load your favourites. Please try again.", TimeSpan.FromSeconds(30));
        await StartContainerAsync(Containers.Inventory);
    }
}
