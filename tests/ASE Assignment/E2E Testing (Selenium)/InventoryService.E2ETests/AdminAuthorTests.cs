namespace InventoryService.E2ETests;

/// <summary>TEST-PLAN.md §5.15: authors in the admin catalogue.</summary>
public sealed class AdminAuthorTests : E2ETestBase
{
    private static readonly By DialogError = By.CssSelector("dialog[open] .modal-error");
    private static readonly By PageMessage = By.CssSelector("p.curation-message[role='status']");

    private async Task OpenAuthorsAsync()
    {
        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/authors");
        Browser.WaitForText("All authors");
        Browser.Until(() => !Browser.HasText("Loading authors…"), what: "the authors to load");
    }

    // The name cell starts with the portrait or, without one, the author's initial; match the name inside it.
    private IWebElement Row(string name) => Browser.Visible(By.XPath($"//tbody/tr[td[1][contains(normalize-space(.), {Browser.XPathLiteral(name)})]]"));

    private void EditRow(string name)
    {
        Browser.Click(Row(name).FindElement(By.XPath(".//button[normalize-space(.)='Edit']")));
        Browser.Until(() => Browser.OpenDialog != null, what: "the author dialog");
    }

    private void Submit(string label) =>
        Browser.Click(By.XPath($"//dialog[@open]//button[@type='submit'][normalize-space(.)={Browser.XPathLiteral(label)}]"));

    [Fact, Trait("Id", "ADMAUTH-01"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMAUTH01_Admin_AddsAnAuthor_WhoGetsAPublicPage()
    {
        var name = $"E2E Author {TestIds.Short()}";
        await OpenAuthorsAsync();

        Browser.ClickButton("Add author");
        Browser.Type(By.Id("author-name"), name);
        Browser.Type(By.Id("author-image"), "https://example.com/portrait.jpg");
        Browser.Type(By.Id("author-description"), "A biography written by the test.");
        Submit("Add author");

        Assert.Equal($"Added {name}.", Browser.Visible(PageMessage).Text);
        TrackAuthor((await Api.AuthorIdAsync(name))!.Value);
        Browser.Go($"/authors/{Uri.EscapeDataString(name)}");
        Browser.WaitForText("A biography written by the test.");
    }

    [Fact, Trait("Id", "ADMAUTH-02"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMAUTH02_Admin_EditsAnAuthorsBiography()
    {
        var name = $"E2E Author {TestIds.Short()}";
        await NewAuthorAsync(name, "Old biography.");
        await OpenAuthorsAsync();

        EditRow(name);
        Browser.Type(By.Id("author-description"), "New biography.");
        Submit("Save changes");

        Assert.Equal($"Saved {name}.", Browser.Visible(PageMessage).Text);
        AssertContains("New biography.", Row(name).Text);
    }

    [Fact, Trait("Id", "ADMAUTH-03"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMAUTH03_RenamingAnAuthor_RenamesThemOnTheirBooks()
    {
        var name = $"E2E Author {TestIds.Short()}";
        var renamed = name + " Jr";
        await NewAuthorAsync(name);
        var book = await NewBookAsync(author: name);
        await OpenAuthorsAsync();

        EditRow(name);
        Browser.WaitForText("Renaming an author also renames it on their books.");
        Browser.Type(By.Id("author-name"), renamed);
        Submit("Save changes");

        Assert.Equal($"Saved {renamed}.", Browser.Visible(PageMessage).Text);
        Browser.Go($"/books/{book.Id}");
        Assert.Equal($"by {renamed}", Browser.Visible(By.CssSelector(".detail-byline")).Text);
    }

    [Fact, Trait("Id", "ADMAUTH-04"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ADMAUTH04_DuplicateAuthorName_IsRefused()
    {
        var name = $"E2E Author {TestIds.Short()}";
        await NewAuthorAsync(name);
        await OpenAuthorsAsync();

        Browser.ClickButton("Add author");
        Browser.Type(By.Id("author-name"), name.ToUpperInvariant());
        Submit("Add author");

        Assert.Equal($"An author named '{name.ToUpperInvariant()}' already exists.", Browser.Visible(DialogError).Text);
    }

    [Fact, Trait("Id", "ADMAUTH-05"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMAUTH05_ClearingThePortrait_IsAllowed()
    {
        var name = $"E2E Author {TestIds.Short()}";
        var id = await NewAuthorAsync(name, imageUrl: "https://example.com/old.jpg");
        await OpenAuthorsAsync();

        EditRow(name);
        Browser.Type(By.Id("author-image"), "");
        Submit("Save changes");

        Assert.Equal($"Saved {name}.", Browser.Visible(PageMessage).Text);
        var author = await Api.SendAsync(HttpMethod.Get, $"{E2EConfig.InventoryServiceUrl}/api/authors/{id}");
        Assert.True(string.IsNullOrEmpty((string?)author.Body!["imageUrl"]));
    }

    [Fact, Trait("Id", "ADMAUTH-06"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ADMAUTH06_NonWebPortraitLink_IsRefused()
    {
        var name = $"E2E Author {TestIds.Short()}";
        await OpenAuthorsAsync();

        Browser.ClickButton("Add author");
        Browser.Type(By.Id("author-name"), name);
        // The browser accepts ftp:// as a URL; the service only allows http(s).
        Browser.Type(By.Id("author-image"), "ftp://example.com/portrait.jpg");
        Submit("Add author");

        Assert.Equal("Unable to add the author.", Browser.Visible(DialogError).Text);
        Assert.Null(await Api.AuthorIdAsync(name));
    }

    [Fact, Trait("Id", "ADMAUTH-07"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMAUTH07_RemovingAnAuthor_KeepsTheirBooks()
    {
        var name = $"E2E Author {TestIds.Short()}";
        await NewAuthorAsync(name);
        var book = await NewBookAsync(author: name);
        await OpenAuthorsAsync();

        Browser.Click(Row(name).FindElement(By.XPath(".//button[normalize-space(.)='Remove']")));
        AssertContains("Their books stay in the catalogue", Browser.AcceptAlert());

        Assert.Equal($"Removed {name}.", Browser.Visible(PageMessage).Text);
        Assert.Null(await Api.AuthorIdAsync(name));
        Assert.NotNull(await Api.BookAsync(book.Id));
    }

    [Fact, Trait("Id", "ADMAUTH-08"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ADMAUTH08_CancellingTheRemoveDialog_KeepsTheAuthor()
    {
        var name = $"E2E Author {TestIds.Short()}";
        await NewAuthorAsync(name);
        await OpenAuthorsAsync();

        Browser.Click(Row(name).FindElement(By.XPath(".//button[normalize-space(.)='Remove']")));
        Browser.DismissAlert();

        Assert.True(Row(name).Displayed);
        Assert.NotNull(await Api.AuthorIdAsync(name));
    }
}
