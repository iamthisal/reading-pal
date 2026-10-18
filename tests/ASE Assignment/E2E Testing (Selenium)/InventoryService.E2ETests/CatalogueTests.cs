namespace InventoryService.E2ETests;

/// <summary>TEST-PLAN.md §5.6: the Discover page and its shelves.</summary>
public sealed class CatalogueTests : E2ETestBase
{
    [Fact, Trait("Id", "CAT-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task CAT01_DiscoverPage_ShowsBannerStatsSliderAndShelves()
    {
        var member = await NewMemberAsync();

        Browser.SignInAs(member);

        Browser.WaitForText("Discover");
        Assert.True(Browser.Visible(Aria("Search the catalogue")).Displayed);
        Browser.Until(() => Browser.Exists(By.CssSelector(".discover-stats span:not(.skeleton)")), what: "the catalogue counts");
        AssertContains("available now", Browser.Find(By.CssSelector(".discover-stats")).Text);
        Assert.True(Browser.Visible(Aria("Library highlights")).Displayed);
        Browser.Until(() => Browser.Driver.FindElements(By.CssSelector("section.shelf[aria-label]")).Count > 0, what: "a shelf");
    }

    [Fact, Trait("Id", "CAT-02"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task CAT02_BannerSearch_OpensSearchWithTheQuery()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        Browser.SignInAs(member);

        Browser.Type(Aria("Search the catalogue"), book.Title);
        Browser.Find(Aria("Search the catalogue")).SendKeys(Keys.Enter);

        Browser.WaitForPath("/search");
        AssertContains(Uri.EscapeDataString(book.Title).Replace("%20", "+"), Browser.Query.Replace("%20", "+"));
        Assert.True(Browser.Visible(By.LinkText(book.Title)).Displayed);
    }

    [Fact, Trait("Id", "CAT-03"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task CAT03_RecommendedList_DrivesTheFirstShelfInOrder()
    {
        var member = await NewMemberAsync();
        var first = await NewBookAsync(title: UniqueTitle("Second"));
        var second = await NewBookAsync(title: UniqueTitle("First"));
        var list = await Api.ListAsync("recommended") ?? throw new InvalidOperationException("The recommended list is missing.");
        var original = list["books"]!.AsArray().Select(book => book!["id"]!.GetValue<int>()).ToArray();
        try
        {
            Assert.True((await Api.SetListItemsAsync("recommended", [second.Id, first.Id])).Ok);

            Browser.SignInAs(member);

            var shelf = Browser.Visible(By.CssSelector($"section.shelf[aria-label={Quote((string)list["name"]!)}]"));
            var titles = shelf.FindElements(By.CssSelector("h3")).Select(heading => heading.Text).ToList();
            Assert.Equal([second.Title, first.Title], titles);
        }
        finally
        {
            await Api.SetListItemsAsync("recommended", original);
        }
    }

    [Fact, Trait("Id", "CAT-04"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task CAT04_PosterSlider_MovesBetweenPostersAndItsLinksWork()
    {
        var member = await NewMemberAsync();
        Browser.SignInAs(member);

        Browser.Click(Aria("Next poster"));
        Browser.Until(() => Browser.Find(By.CssSelector(".poster-dot[aria-current='true']")).GetAttribute("aria-label")!.StartsWith("Show poster 2"),
            what: "the second poster");
        Browser.Click(By.CssSelector(".poster-dot[aria-label^='Show poster 1']"));
        Browser.Until(() => Browser.Find(By.CssSelector(".poster-dot[aria-current='true']")).GetAttribute("aria-label")!.StartsWith("Show poster 1"),
            what: "the first poster");
        Browser.Click(By.XPath("//a[contains(@class,'poster-action')][contains(normalize-space(.),'Browse books')]"));

        Browser.WaitForPath("/search");
        AssertContains("sort=newest", Browser.Query);
    }

    [Fact, Trait("Id", "CAT-05"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task CAT05_SlowCatalogue_ShowsAnimatedPlaceholders_ThenTheShelves()
    {
        var member = await NewMemberAsync();
        Browser.SignInAs(member, "/favourites");
        PauseContainer(Containers.Inventory);

        Browser.Go("/home");

        Browser.Until(() => Browser.Exists(By.CssSelector(".skeleton")), what: "loading placeholders");
        var animation = (string?)Browser.Js("return getComputedStyle(document.querySelector('.skeleton')).animationName;");
        Assert.NotEqual("none", animation);
        ResumeContainer(Containers.Inventory);
        Browser.Until(() => Browser.Driver.FindElements(By.CssSelector("section.shelf[aria-label]")).Count > 0,
            TimeSpan.FromSeconds(30), "the shelves after the catalogue answers");
    }

    [Fact, Trait("Id", "CAT-06"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task CAT06_InventoryDown_ShowsTheCatalogueError_AndTheShellStillWorks()
    {
        var member = await NewMemberAsync();
        StopContainer(Containers.Inventory);

        Browser.SignInAs(member);

        Browser.WaitForText("Unable to load the book catalogue. Please ensure the Inventory Service is running.", TimeSpan.FromSeconds(30));
        Assert.True(Browser.Visible(Aria("Library navigation")).Displayed);
        Browser.Click(By.LinkText("My profile"));
        Browser.WaitForPath("/profile");
        await StartContainerAsync(Containers.Inventory);
    }

    [Fact, Trait("Id", "CAT-07"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task CAT07_RecommendedShelf_FollowsTheScreenWidth()
    {
        var member = await NewMemberAsync();
        var list = await Api.ListAsync("recommended");
        var shelfSelector = By.CssSelector($"section.shelf[aria-label={Quote((string)list!["name"]!)}] .shelf-track");
        Browser.SignInAs(member);
        Browser.Resize(2560, 1440);

        // At 2560 px all six recommended books fit, so the shelf has nothing to scroll.
        Browser.Until(() => Convert.ToBoolean(Browser.Js("const t = arguments[0]; return t.children.length >= 6 && t.scrollWidth <= t.clientWidth + 1;",
            Browser.Find(shelfSelector))), what: "all six books to fit at 2560 px");

        Browser.Resize(1440, 900);
        Browser.Until(() => Convert.ToBoolean(Browser.Js("const t = arguments[0]; return t.scrollWidth > t.clientWidth + 1;", Browser.Find(shelfSelector))),
            what: "the shelf to page at 1440 px");

        Browser.Resize(390, 844);
        Assert.False(Browser.HasHorizontalScroll());
    }

    [Fact, Trait("Id", "CAT-08"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task CAT08_Cards_ShowAvailabilityAndTheMatchingBorrowState()
    {
        var member = await NewMemberAsync();
        var suffix = TestIds.Short();
        var available = await NewBookAsync(2, title: $"E2E Stock {suffix} In");
        var gone = await NewBookAsync(1, title: $"E2E Stock {suffix} Out");
        await Api.MarkUnavailableAsync(gone.Id);

        Browser.SignInAs(member, $"/search?q={suffix}");

        var inRow = Browser.Visible(By.XPath($"//li[contains(@class,'book-list-row')][.//a[normalize-space(.)='{available.Title}']]"));
        var outRow = Browser.Visible(By.XPath($"//li[contains(@class,'book-list-row')][.//a[normalize-space(.)='{gone.Title}']]"));
        AssertContains("Available", inRow.Text);
        AssertContains("Not available now", outRow.Text);
        Assert.True(inRow.FindElement(Aria($"Borrow {available.Title}")).Enabled);
        Assert.False(outRow.FindElement(Aria($"{gone.Title} has no copies on the shelf")).Enabled);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
