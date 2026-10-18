namespace InventoryService.E2ETests;

/// <summary>TEST-PLAN.md §5.7: the Search page.</summary>
public sealed class SearchTests : E2ETestBase
{
    // Read in one script call: the list re-renders while results arrive, which would leave Selenium holding stale rows.
    private IReadOnlyList<string> ResultTitles() =>
        ((IReadOnlyCollection<object>)Browser.Js("return [...document.querySelectorAll('ol.book-list li.book-list-row h2')].map(h => h.textContent.trim());")!)
            .Select(title => (string)title).ToList();

    private string CountText => Browser.Find(By.CssSelector(".search-count")).Text;

    private void WaitForResults(int count) =>
        Browser.Until(() => !Browser.Exists(By.CssSelector(".book-list-stale")) && ResultTitles().Count == count,
            what: $"{count} search results (showing {ResultTitles().Count})");

    private void ToggleFilter(string label) =>
        Browser.Click(By.XPath($"//aside[@aria-label='Filter results']//label[.//span[normalize-space(.)={Browser.XPathLiteral(label)}]]//input"));

    [Fact, Trait("Id", "SRCH-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task SRCH01_SearchByTitle_FindsOnlyThatBook()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();

        Browser.SignInAs(member, $"/search?q={Uri.EscapeDataString(book.Title)}");

        WaitForResults(1);
        Assert.Equal(book.Title, ResultTitles().Single());
        Assert.Equal("1 book", CountText);
    }

    [Fact, Trait("Id", "SRCH-02"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task SRCH02_SearchByAuthorIsbnAndGenre_EachFindTheBook()
    {
        var member = await NewMemberAsync();
        var suffix = TestIds.Short();
        var book = await NewBookAsync(author: $"Author{suffix}", genre: $"Genre{suffix}");
        Browser.SignInAs(member);

        foreach (var query in new[] { $"Author{suffix}", book.Isbn, $"Genre{suffix}" })
        {
            Browser.Go($"/search?q={Uri.EscapeDataString(query)}");
            WaitForResults(1);
            Assert.Equal(book.Title, ResultTitles().Single());
        }
    }

    [Fact, Trait("Id", "SRCH-03"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task SRCH03_SeveralWords_MustAllMatch()
    {
        var member = await NewMemberAsync();
        var suffix = TestIds.Short();
        var orchid = await NewBookAsync(title: $"E2E {suffix} Orchid");
        await NewBookAsync(title: $"E2E {suffix} Tulip");

        Browser.SignInAs(member, $"/search?q={suffix}%20orchid");

        WaitForResults(1);
        Assert.Equal(orchid.Title, ResultTitles().Single());
    }

    [Fact, Trait("Id", "SRCH-04"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task SRCH04_GenreFilters_NarrowAndRestoreTheResults()
    {
        var member = await NewMemberAsync();
        var suffix = TestIds.Short();
        string mystery = $"Mystery{suffix}", poetry = $"Poetry{suffix}";
        var a = await NewBookAsync(title: $"E2E {suffix} A", genre: mystery);
        var b = await NewBookAsync(title: $"E2E {suffix} B", genre: poetry);
        await NewBookAsync(title: $"E2E {suffix} C", genre: TestGenre);
        Browser.SignInAs(member, $"/search?q={suffix}");
        WaitForResults(3);

        ToggleFilter(mystery);
        WaitForResults(1);
        Assert.Equal(a.Title, ResultTitles().Single());
        AssertContains($"genre={mystery}", Browser.Query);

        ToggleFilter(poetry);
        WaitForResults(2);
        Assert.Equal(new[] { a.Title, b.Title }.Order(), ResultTitles().Order());

        ToggleFilter(mystery);
        ToggleFilter(poetry);
        WaitForResults(3);
    }

    [Fact, Trait("Id", "SRCH-05"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task SRCH05_AvailableOnly_HidesBooksWithNoCopies()
    {
        var member = await NewMemberAsync();
        var suffix = TestIds.Short();
        var onShelf = await NewBookAsync(title: $"E2E {suffix} Shelf");
        var gone = await NewBookAsync(1, title: $"E2E {suffix} Gone");
        await Api.MarkUnavailableAsync(gone.Id);
        Browser.SignInAs(member, $"/search?q={suffix}");
        WaitForResults(2);

        ToggleFilter("Available now");

        WaitForResults(1);
        Assert.Equal(onShelf.Title, ResultTitles().Single());
        AssertContains("available=1", Browser.Query);
    }

    [Fact, Trait("Id", "SRCH-06"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task SRCH06_SortOrder_ChangesTheListAndIsKeptInTheAddress()
    {
        var member = await NewMemberAsync();
        var suffix = TestIds.Short();
        var banana = await NewBookAsync(title: $"E2E {suffix} Banana");
        var apple = await NewBookAsync(title: $"E2E {suffix} Apple");
        var cherry = await NewBookAsync(title: $"E2E {suffix} Cherry");
        Browser.SignInAs(member, $"/search?q={suffix}");
        WaitForResults(3);

        Browser.Select(By.CssSelector(".search-sort select"), "Title A–Z");
        Browser.Until(() => ResultTitles().SequenceEqual([apple.Title, banana.Title, cherry.Title]), what: "title order");
        AssertContains("sort=title", Browser.Query);

        Browser.Select(By.CssSelector(".search-sort select"), "Newest first");
        Browser.Until(() => ResultTitles().SequenceEqual([cherry.Title, apple.Title, banana.Title]), what: "newest first");
        AssertContains("sort=newest", Browser.Query);
    }

    [Fact, Trait("Id", "SRCH-07"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task SRCH07_ManyResults_ArePagedTwelveAtATime()
    {
        var member = await NewMemberAsync();
        var suffix = TestIds.Short();
        for (var index = 1; index <= 13; index++) await NewBookAsync(1, title: $"E2E {suffix} Page {index:00}");
        Browser.SignInAs(member, $"/search?q={suffix}&sort=title");
        WaitForResults(12);
        Assert.Equal("Showing 1–12 of 13 books", CountText);

        Browser.Click(By.XPath("//nav[@aria-label='Result pages']//button[contains(normalize-space(.),'Next')]"));

        WaitForResults(1);
        Assert.Equal($"E2E {suffix} Page 13", ResultTitles().Single());
        AssertContains("page=2", Browser.Query);
        Assert.Equal("Showing 13–13 of 13 books", CountText);
    }

    [Fact, Trait("Id", "SRCH-08"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task SRCH08_DeepLink_RestoresTheQueryAndFilters()
    {
        var member = await NewMemberAsync();
        var suffix = TestIds.Short();
        var onShelf = await NewBookAsync(title: $"E2E {suffix} Linked");
        var gone = await NewBookAsync(1, title: $"E2E {suffix} Hidden");
        await Api.MarkUnavailableAsync(gone.Id);

        Browser.SignInAs(member, $"/search?q={suffix}&available=1&page=1");

        WaitForResults(1);
        Assert.Equal(onShelf.Title, ResultTitles().Single());
        Assert.Equal(suffix, Browser.Find(Aria("Search the catalogue")).GetAttribute("value"));
        Assert.True(Browser.Find(By.XPath("//label[.//span[normalize-space(.)='Available now']]//input")).Selected);
        Assert.True(Browser.Visible(Aria("Remove filter: available now")).Displayed);
    }

    [Fact, Trait("Id", "SRCH-09"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task SRCH09_NoMatches_ShowsAFriendlyEmptyState()
    {
        var member = await NewMemberAsync();
        var query = $"zzqq-E2E-{TestIds.Short()}";

        Browser.SignInAs(member, $"/search?q={query}");

        Browser.WaitForText($"Nothing matches “{query}”");
        Assert.Equal("No books found", CountText);
        Assert.False(Browser.Exists(By.CssSelector(".search-error")));
    }

    [Fact, Trait("Id", "SRCH-10"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task SRCH10_OddInput_IsTreatedAsPlainText()
    {
        var member = await NewMemberAsync();
        var odd = "<script>window.__e2e=1</script>%\"' " + new string('x', 300);
        Browser.SignInAs(member, "/search");

        Browser.Type(Aria("Search the catalogue"), odd);
        Browser.Find(Aria("Search the catalogue")).SendKeys(Keys.Enter);

        Browser.WaitForText("Nothing matches");
        Assert.Null(Browser.Js("return window.__e2e;"));
        Assert.False(Browser.Exists(By.CssSelector(".search-error")));
    }

    [Fact, Trait("Id", "SRCH-11"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task SRCH11_PageBeyondTheEnd_DoesNotError()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();

        Browser.SignInAs(member, $"/search?q={Uri.EscapeDataString(book.Title)}&page=999");

        Browser.Until(() => CountText.Length > 0 && CountText != "Searching…", what: "the search to finish");
        Assert.False(Browser.Exists(By.CssSelector(".search-error")));
        Assert.True(Browser.Visible(Aria("Library navigation")).Displayed);
    }

    [Fact, Trait("Id", "SRCH-12"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task SRCH12_InventoryDown_ShowsTheSearchError()
    {
        var member = await NewMemberAsync();
        StopContainer(Containers.Inventory);

        Browser.SignInAs(member, "/search?q=anything");

        Browser.WaitForText("Unable to search the catalogue. Please try again.", TimeSpan.FromSeconds(30));
        await StartContainerAsync(Containers.Inventory);
    }

    [Fact, Trait("Id", "SRCH-13"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task SRCH13_BannerSearchBox_HighlightsTheWholeBoxWithoutAnInnerRing()
    {
        var member = await NewMemberAsync();
        Browser.SignInAs(member, "/search");

        Browser.Click(Aria("Search the catalogue"));

        var input = Browser.Find(Aria("Search the catalogue"));
        Assert.Equal("none", (string?)Browser.Js("return getComputedStyle(arguments[0]).outlineStyle;", input));
        var shadow = (string?)Browser.Js("return getComputedStyle(arguments[0].closest('.discover-search-panel')).boxShadow;", input);
        AssertContains("rgb", shadow ?? "");
    }
}
