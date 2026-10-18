namespace InventoryService.E2ETests;

/// <summary>TEST-PLAN.md §5.8: a book's page and an author's page.</summary>
public sealed class BookPageTests : E2ETestBase
{
    [Fact, Trait("Id", "BOOK-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task BOOK01_BookPage_ShowsItsDetailsAndActions()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync(2, description: "A test book used by the end-to-end suite.");
        Browser.SignInAs(member, $"/search?q={Uri.EscapeDataString(book.Title)}");

        Browser.Click(By.LinkText(book.Title));

        Browser.WaitForPath($"/books/{book.Id}");
        Assert.Equal(book.Title, Browser.Visible(By.CssSelector(".detail-body h1")).Text);
        Assert.Equal($"by {book.Author}", Browser.Find(By.CssSelector(".detail-byline")).Text);
        Assert.True(Browser.Exists(By.CssSelector($".detail-eyebrow a[href*='genre=']")));
        AssertContains("A test book used by the end-to-end suite.", Browser.Find(By.CssSelector("[aria-label='About this book']")).Text);
        AssertContains("2 of 2 copies on the shelf", Browser.Find(By.CssSelector(".detail-stock")).Text);
        AssertContains(book.Isbn, Browser.Find(By.CssSelector(".detail-facts")).Text);
        Assert.True(Browser.Find(Aria($"Borrow {book.Title}")).Enabled);
        Assert.True(Browser.Exists(By.CssSelector(".detail-favourite")));
    }

    [Fact, Trait("Id", "BOOK-02"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task BOOK02_BackButton_ReturnsToTheSameSearchResults()
    {
        var member = await NewMemberAsync();
        var book = await NewBookAsync();
        var query = Uri.EscapeDataString(book.Title);
        Browser.SignInAs(member, $"/search?q={query}");
        Browser.Click(By.LinkText(book.Title));
        Browser.WaitForPath($"/books/{book.Id}");

        Browser.ClickButton("Back");

        Browser.WaitForPath("/search");
        Assert.True(Browser.Visible(By.LinkText(book.Title)).Displayed);
        Assert.Equal(book.Title, Browser.Find(Aria("Search the catalogue")).GetAttribute("value"));
    }

    [Theory, Trait("Id", "BOOK-03"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    [InlineData("999999999")]
    [InlineData("abc")]
    public async Task BOOK03_UnknownBook_ShowsBookNotFound(string id)
    {
        var member = await NewMemberAsync();

        Browser.SignInAs(member, $"/books/{id}");

        Browser.WaitForText("Book not found");
        AssertContains("It may have been removed from the catalogue.", Browser.BodyText);
        Assert.True(Browser.Visible(Browser.ButtonByText("Back to catalogue")).Displayed);
    }

    [Fact, Trait("Id", "BOOK-04"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task BOOK04_AuthorLink_OpensTheAuthorsPageWithTheirBooks()
    {
        var member = await NewMemberAsync();
        var name = $"E2E Writer {TestIds.Short()}";
        await NewAuthorAsync(name, "Writes test books.");
        var book = await NewBookAsync(author: name);
        Browser.SignInAs(member, $"/books/{book.Id}");

        Browser.Click(By.CssSelector(".detail-byline a"));

        Browser.WaitForPath($"/authors/{Uri.EscapeDataString(name)}");
        Assert.Equal(name, Browser.Visible(By.CssSelector(".detail-author h1")).Text);
        AssertContains("Writes test books.", Browser.Find(By.CssSelector("[aria-label='About the author']")).Text);
        AssertContains("1 book in the catalogue, 1 available now.", Browser.BodyText);
        Assert.True(Browser.Visible(By.CssSelector($"section.shelf[aria-label='Books by {name}']")).Text.Contains(book.Title));
    }

    [Fact, Trait("Id", "BOOK-05"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task BOOK05_UnknownAuthor_ShowsAuthorNotFound()
    {
        var member = await NewMemberAsync();
        var name = $"Nobody E2E {TestIds.Short()}";

        Browser.SignInAs(member, $"/authors/{Uri.EscapeDataString(name)}");

        Browser.WaitForText("Author not found");
        AssertContains($"There are no books by “{name}” in the catalogue.", Browser.BodyText);
    }

    [Fact, Trait("Id", "BOOK-06"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task BOOK06_AuthorWithNoBooks_SaysSo()
    {
        var member = await NewMemberAsync();
        var name = $"E2E Unpublished {TestIds.Short()}";
        await NewAuthorAsync(name);

        Browser.SignInAs(member, $"/authors/{Uri.EscapeDataString(name)}");

        Browser.WaitForText("No books in the catalogue yet.");
        Assert.False(Browser.Exists(By.CssSelector("section.shelf")));
    }

    [Fact, Trait("Id", "BOOK-07"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task BOOK07_PendingMember_IsToldTheyCanBorrowAfterApproval()
    {
        var member = await NewPendingMemberAsync();
        var book = await NewBookAsync();

        Browser.SignInAs(member, $"/books/{book.Id}");

        Browser.WaitForText("You can borrow once an admin has approved your account.");
        Assert.False(Browser.Exists(Aria($"Borrow {book.Title}")));
    }

    [Fact, Trait("Id", "BOOK-08"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task BOOK08_MissingOrBrokenCover_ShowsThePlaceholderCover()
    {
        var member = await NewMemberAsync();
        var noCover = await NewBookAsync();
        var brokenCover = await NewBookAsync(cover: "https://cover.invalid/missing.jpg");

        Browser.SignInAs(member, $"/books/{noCover.Id}");
        Assert.True(Browser.Visible(By.CssSelector(".detail-cover-fallback")).Text.Contains(noCover.Title));

        Browser.Go($"/books/{brokenCover.Id}");
        Browser.Until(() => Browser.Exists(By.CssSelector(".detail-cover-fallback")), TimeSpan.FromSeconds(20), "the broken cover to fall back");
        Assert.False(Browser.Exists(By.CssSelector("img.detail-cover")));
    }
}
