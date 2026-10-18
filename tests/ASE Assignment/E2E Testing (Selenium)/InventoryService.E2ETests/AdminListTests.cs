namespace InventoryService.E2ETests;

/// <summary>TEST-PLAN.md §5.16: curated lists.</summary>
public sealed class AdminListTests : E2ETestBase
{
    private static readonly By DialogError = By.CssSelector("dialog[open] .modal-error");
    private static readonly By PageMessage = By.CssSelector("p.curation-message[role='status']");

    private async Task OpenListsAsync()
    {
        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/lists");
        Browser.WaitForText("All lists");
        Browser.Until(() => !Browser.HasText("Loading lists…"), what: "the lists to load");
    }

    private IWebElement Row(string key) => Browser.Visible(By.XPath($"//tr[.//span[contains(@class,'curation-key')][normalize-space(.)='{key}']]"));

    private void RowButton(string key, string label)
    {
        Browser.Click(Row(key).FindElement(By.XPath($".//button[normalize-space(.)={Browser.XPathLiteral(label)}]")));
    }

    private void OpenEditor(string key)
    {
        RowButton(key, "Edit contents");
        Browser.Until(() => Browser.OpenDialog?.Text.Contains("On this list, in order") == true, what: "the list editor");
    }

    private IReadOnlyList<string> ItemsOnList() =>
        Browser.Driver.FindElements(By.CssSelector("dialog[open] ol.curation-items .curation-item-text strong")).Select(item => item.Text).ToList();

    private void CreateThroughForm(string name, string? key = null)
    {
        Browser.ClickButton("Create list");
        Browser.Type(By.Id("list-name"), name);
        if (key != null) Browser.Type(By.Id("list-key"), key);
        Browser.Click(By.XPath("//dialog[@open]//button[@type='submit'][normalize-space(.)='Create list']"));
    }

    [Fact, Trait("Id", "ADMLIST-01"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMLIST01_CreatingAList_FillsItsKeyFromTheName()
    {
        var suffix = TestIds.Short();
        var name = $"E2E Picks {suffix}";
        await OpenListsAsync();

        Browser.ClickButton("Create list");
        Browser.Type(By.Id("list-name"), name);
        Assert.Equal($"e2e-picks-{suffix}", Browser.Find(By.Id("list-key")).GetAttribute("value"));
        Browser.Click(By.XPath("//dialog[@open]//button[@type='submit'][normalize-space(.)='Create list']"));
        TrackList($"e2e-picks-{suffix}");

        // The new list opens straight in its contents editor; the confirmation shows once it is closed.
        Browser.Until(() => Browser.OpenDialog?.Text.Contains($"Contents of {name}") == true, what: "the new list's editor");
        Browser.Click(By.CssSelector("dialog[open] .modal-close"));
        Assert.Equal($"Created \"{name}\". Add its books below.", Browser.Visible(PageMessage).Text);
        Assert.NotNull(await Api.ListAsync($"e2e-picks-{suffix}"));
    }

    [Fact, Trait("Id", "ADMLIST-02"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMLIST02_AddingAndOrderingBooks_IsSavedInThatOrder()
    {
        var suffix = TestIds.Short();
        var first = await NewBookAsync(title: $"E2E {suffix} One");
        var second = await NewBookAsync(title: $"E2E {suffix} Two");
        var third = await NewBookAsync(title: $"E2E {suffix} Three");
        var key = await NewListAsync();
        await OpenListsAsync();
        OpenEditor(key);

        Browser.Type(By.CssSelector("dialog[open] input.curation-search"), suffix);
        foreach (var book in new[] { first, second, third }) Browser.Click(Aria($"Add {book.Title} to the list"));
        Browser.Click(Aria($"Move {third.Title} up"));
        Assert.Equal([first.Title, third.Title, second.Title], ItemsOnList());
        Browser.Click(By.XPath("//dialog[@open]//button[normalize-space(.)='Save list']"));

        Browser.Until(() => Browser.OpenDialog == null, what: "the editor to close");
        AssertContains("with 3 items.", Browser.Visible(PageMessage).Text);
        var saved = (await Api.ListAsync(key))!["books"]!.AsArray().Select(book => (string)book!["title"]!).ToList();
        Assert.Equal([first.Title, third.Title, second.Title], saved);
    }

    [Fact, Trait("Id", "ADMLIST-03"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMLIST03_AuthorList_TakesAuthors()
    {
        var name = $"E2E Listed {TestIds.Short()}";
        var authorId = await NewAuthorAsync(name);
        var key = await NewListAsync("Authors");
        await OpenListsAsync();
        OpenEditor(key);

        Browser.Type(By.CssSelector("dialog[open] input.curation-search"), name);
        Browser.Click(Aria($"Add {name} to the list"));
        Browser.Click(By.XPath("//dialog[@open]//button[normalize-space(.)='Save list']"));

        Browser.Until(() => Browser.OpenDialog == null, what: "the editor to close");
        var authors = (await Api.ListAsync(key))!["authors"]!.AsArray().Select(author => author!["id"]!.GetValue<int>());
        Assert.Contains(authorId, authors);
    }

    [Fact, Trait("Id", "ADMLIST-04"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ADMLIST04_DuplicateKey_IsRefused()
    {
        var key = await NewListAsync();
        await OpenListsAsync();

        CreateThroughForm("Another name", key);

        Assert.Equal($"A list with the key '{key}' already exists.", Browser.Visible(DialogError).Text);
    }

    [Theory, Trait("Id", "ADMLIST-05"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    [InlineData("a--b")]
    [InlineData("-a-")]
    public async Task ADMLIST05_KeyWithDoubleOrOuterHyphens_IsRefused(string key)
    {
        await OpenListsAsync();

        CreateThroughForm($"E2E Bad Key {TestIds.Short()}", key);

        Assert.Equal("Unable to create the list.", Browser.Visible(DialogError).Text);
        Assert.Null(await Api.ListAsync(key));
    }

    [Fact, Trait("Id", "ADMLIST-06"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ADMLIST06_ClosingWithUnsavedChanges_AsksFirst()
    {
        var book = await NewBookAsync();
        var key = await NewListAsync();
        await OpenListsAsync();
        OpenEditor(key);
        Browser.Type(By.CssSelector("dialog[open] input.curation-search"), book.Title);
        Browser.Click(Aria($"Add {book.Title} to the list"));
        Browser.WaitForText("You have unsaved changes.");

        Browser.Click(By.CssSelector("dialog[open] .modal-close"));
        Assert.Equal("Discard the unsaved changes?", Browser.DismissAlert());
        Assert.Equal([book.Title], ItemsOnList());

        Browser.Click(By.XPath("//dialog[@open]//div[@class='modal-actions']//button[normalize-space(.)='Cancel']"));
        Browser.AcceptAlert();
        Browser.Until(() => Browser.OpenDialog == null, what: "the editor to close");
        Assert.Empty((await Api.ListAsync(key))!["books"]!.AsArray());
    }

    [Fact, Trait("Id", "ADMLIST-07"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ADMLIST07_EveryAuthorAlreadyListed_LeavesNothingToAdd()
    {
        var key = await NewListAsync("Authors", await Api.AllAuthorIdsAsync());
        await OpenListsAsync();

        OpenEditor(key);

        Browser.WaitForText("Every one of the authors is already on this list.");
    }

    [Fact, Trait("Id", "ADMLIST-08"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMLIST08_RenamingAList_KeepsItsKey()
    {
        var key = await NewListAsync();
        var newName = $"E2E Renamed {TestIds.Short()}";
        await OpenListsAsync();

        RowButton(key, "Rename");
        Browser.Type(By.CssSelector("dialog[open] .modal-fields input"), newName);
        Browser.Click(By.XPath("//dialog[@open]//button[@type='submit'][normalize-space(.)='Save changes']"));

        Assert.Equal($"Renamed to \"{newName}\".", Browser.Visible(PageMessage).Text);
        Assert.Equal(newName, (string?)(await Api.ListAsync(key))!["name"]);
    }

    [Fact, Trait("Id", "ADMLIST-09"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMLIST09_DeletingAList_KeepsItsBooks()
    {
        var book = await NewBookAsync();
        var key = await NewListAsync("Books", book.Id);
        var name = (string)(await Api.ListAsync(key))!["name"]!;
        await OpenListsAsync();

        RowButton(key, "Delete");
        AssertContains("The books themselves are kept.", Browser.AcceptAlert());

        Assert.Equal($"Deleted \"{name}\".", Browser.Visible(PageMessage).Text);
        Assert.Null(await Api.ListAsync(key));
        Assert.NotNull(await Api.BookAsync(book.Id));
    }

    [Fact, Trait("Id", "ADMLIST-10"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task ADMLIST10_ItemSearchWithNoMatch_SaysSo()
    {
        var key = await NewListAsync();
        await OpenListsAsync();
        OpenEditor(key);

        Browser.Type(By.CssSelector("dialog[open] input.curation-search"), $"zzqq-{TestIds.Short()}");

        Browser.WaitForText("No books match that search.");
    }
}
