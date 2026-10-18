namespace InventoryService.E2ETests;

/// <summary>TEST-PLAN.md §5.14: books and genres in the admin inventory.</summary>
public sealed class AdminBookTests : E2ETestBase
{
    private static readonly By DialogError = By.CssSelector("dialog[open] .modal-error");
    private static readonly By PageSuccess = By.CssSelector(".admin-inventory-alert-success");
    private static readonly By PageError = By.CssSelector(".admin-inventory-alert-error");

    private async Task OpenInventoryAsync(string? filter = null)
    {
        Browser.SignInAsAdmin(await AdminTokenAsync(), "/admin/books");
        Browser.WaitForText("Current Inventory");
        if (filter != null) Browser.Type(By.CssSelector("input[placeholder='Search by title, author, ISBN...']"), filter);
    }

    private void OpenAddForm()
    {
        Browser.ClickButton("Add new book");
        Browser.Until(() => Browser.OpenDialog != null, what: "the Add a book dialog");
    }

    private void FillAddForm(string title, string author, string isbn, string copies, string? description = null, string? cover = null)
    {
        Browser.Type(By.Id("book-title"), title);
        Browser.Type(By.Id("book-author"), author);
        Browser.Type(By.Id("book-isbn"), isbn);
        Browser.Select(By.Id("book-genre"), TestGenre);
        if (cover != null) Browser.Type(By.Id("book-cover"), cover);
        if (description != null) Browser.Type(By.Id("book-description"), description);
        Browser.Type(By.Id("book-copies"), copies);
    }

    private void SubmitDialog(string label) =>
        Browser.Click(By.XPath($"//dialog[@open]//button[@type='submit'][normalize-space(.)={Browser.XPathLiteral(label)}]"));

    private async Task<int?> BookIdByTitleAsync(string title)
    {
        var result = await Api.SendAsync(HttpMethod.Get, $"{E2EConfig.InventoryServiceUrl}/api/books/search?q={Uri.EscapeDataString(title)}");
        return result.Body?["items"]?.AsArray().FirstOrDefault(item => (string?)item?["title"] == title)?["id"]?.GetValue<int>();
    }

    [Fact, Trait("Id", "ADMBOOK-01"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task ADMBOOK01_Admin_AddsABook_ThatIsSearchableAtOnce()
    {
        var title = UniqueTitle("Added");
        await OpenInventoryAsync();
        OpenAddForm();

        FillAddForm(title, "E2E Admin Author", TestIds.Isbn(), "3", "Added through the admin form.", "https://example.com/cover.jpg");
        SubmitDialog("Add book");

        AssertContains($"Successfully added \"{title}\"", Browser.Visible(PageSuccess).Text);
        AssertContains("Available Copies: 3", Browser.Find(PageSuccess).Text);
        var id = await BookIdByTitleAsync(title);
        Assert.NotNull(id);
        TrackBook(id!.Value);
    }

    [Fact, Trait("Id", "ADMBOOK-02"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK02_BlankRequiredField_IsRefused()
    {
        await OpenInventoryAsync();
        OpenAddForm();

        // Spaces get past the browser's "required" check, so the page's own check has to catch them.
        FillAddForm("   ", "E2E Author", TestIds.Isbn(), "1");
        SubmitDialog("Add book");

        Assert.Equal("Please fill in all required fields.", Browser.Visible(DialogError).Text);
        Assert.NotNull(Browser.OpenDialog);
    }

    [Fact, Trait("Id", "ADMBOOK-03"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK03_NegativeCopies_AreBlocked()
    {
        var title = UniqueTitle("Negative");
        await OpenInventoryAsync();
        OpenAddForm();

        FillAddForm(title, "E2E Author", TestIds.Isbn(), "-1");
        SubmitDialog("Add book");

        Assert.False(Browser.IsValid(By.Id("book-copies")));
        Assert.NotNull(Browser.OpenDialog);
        Assert.Null(await BookIdByTitleAsync(title));
    }

    [Fact, Trait("Id", "ADMBOOK-04"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK04_DuplicateIsbn_IsRefusedEvenInADifferentCase()
    {
        var existing = await NewBookAsync();
        var title = UniqueTitle("Duplicate");
        await OpenInventoryAsync();
        OpenAddForm();

        FillAddForm(title, "E2E Author", existing.Isbn.ToLowerInvariant(), "1");
        SubmitDialog("Add book");

        Assert.Equal($"A book with ISBN '{existing.Isbn.ToLowerInvariant()}' already exists.", Browser.Visible(DialogError).Text);
        Assert.Null(await BookIdByTitleAsync(title));
    }

    [Fact, Trait("Id", "ADMBOOK-05"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK05_InvalidCoverUrl_IsBlocked()
    {
        await OpenInventoryAsync();
        OpenAddForm();

        FillAddForm(UniqueTitle("Cover"), "E2E Author", TestIds.Isbn(), "1", cover: "not a url");
        SubmitDialog("Add book");

        Assert.False(Browser.IsValid(By.Id("book-cover")));
        Assert.NotNull(Browser.OpenDialog);
    }

    [Fact, Trait("Id", "ADMBOOK-06"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK06_OverlongTitle_IsRefusedByTheServer_AndCappedInTheEditForm()
    {
        var existing = await NewBookAsync();
        var longTitle = "E2E " + new string('T', 246);
        await OpenInventoryAsync(existing.Title);
        OpenAddForm();

        // The Add form has no length limit on the title, so the service's 200-character rule decides.
        FillAddForm(longTitle, "E2E Author", TestIds.Isbn(), "1");
        SubmitDialog("Add book");

        Assert.Equal("Failed to add book. Please verify your inputs and try again.", Browser.Visible(DialogError).Text);
        Assert.Null(await BookIdByTitleAsync(longTitle));

        // The Edit form stops typing at 200 characters instead.
        Browser.Click(By.CssSelector("dialog[open] .modal-close"));
        Browser.Click(By.CssSelector($"button[title={Quote($"Edit {existing.Title}")}]"));
        Browser.Type(By.Id("edit-book-title-input"), longTitle);
        Assert.Equal(200, Browser.Find(By.Id("edit-book-title-input")).GetAttribute("value")!.Length);
    }

    [Fact, Trait("Id", "ADMBOOK-07"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMBOOK07_Admin_EditsABook_AndTheChangeShowsToReaders()
    {
        var book = await NewBookAsync();
        var renamed = UniqueTitle("Edited");
        await OpenInventoryAsync(book.Title);

        Browser.Click(By.CssSelector($"button[title={Quote($"Edit {book.Title}")}]"));
        Browser.Type(By.Id("edit-book-title-input"), renamed);
        Browser.Type(By.Id("edit-book-description"), "Edited description.");
        SubmitDialog("Save changes");

        Assert.Equal($"Successfully updated \"{renamed}\".", Browser.Visible(PageSuccess).Text);
        Browser.Go($"/books/{book.Id}");
        Assert.Equal(renamed, Browser.Visible(By.CssSelector(".detail-body h1")).Text);
        Browser.WaitForText("Edited description.");
    }

    [Fact, Trait("Id", "ADMBOOK-08"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK08_TotalCopiesBelowCurrentLoans_IsRefused()
    {
        var book = await NewBookAsync(3);
        await Api.BorrowAsync(await NewMemberAsync(), book);
        await Api.BorrowAsync(await NewMemberAsync(), book);
        await OpenInventoryAsync(book.Title);

        Browser.Click(By.CssSelector($"button[title={Quote($"Edit {book.Title}")}]"));
        Browser.Type(By.Id("edit-book-copies"), "1");
        SubmitDialog("Save changes");

        Assert.Equal("Total copies cannot be less than currently borrowed copies (2).", Browser.Visible(DialogError).Text);
        Assert.Equal(3, (await Api.BookAsync(book.Id))!["totalCopies"]!.GetValue<int>());
    }

    [Fact, Trait("Id", "ADMBOOK-09"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMBOOK09_MarkUnavailable_GreysOutBorrowForReaders()
    {
        var book = await NewBookAsync(2);
        var member = await NewMemberAsync();
        await OpenInventoryAsync(book.Title);

        Browser.Click(By.CssSelector($"button[title={Quote($"Mark {book.Title} unavailable")}]"));
        AssertContains("Available copies will be set to 0.", Browser.AcceptAlert());

        Assert.Equal($"\"{book.Title}\" is now marked as unavailable.", Browser.Visible(PageSuccess).Text);
        Browser.SignInAs(member, $"/books/{book.Id}");
        Assert.False(Browser.Visible(Aria($"{book.Title} has no copies on the shelf")).Enabled);
    }

    [Fact, Trait("Id", "ADMBOOK-10"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMBOOK10_MakeAvailable_PutsOneCopyBack()
    {
        var book = await NewUnavailableBookAsync();
        await OpenInventoryAsync(book.Title);

        Browser.Click(By.CssSelector($"button[title={Quote($"Make {book.Title} available")}]"));
        AssertContains("This will add 1 available copy.", Browser.AcceptAlert());

        Assert.Equal($"\"{book.Title}\" is available again with 1 copy.", Browser.Visible(PageSuccess).Text);
        Assert.Equal(1, (await Api.BookAsync(book.Id))!["availableCopies"]!.GetValue<int>());
    }

    [Fact, Trait("Id", "ADMBOOK-11"), Trait("Priority", "P1"), Trait("Type", "Happy")]
    public async Task ADMBOOK11_DeletingABookWithNoLoans_RemovesIt()
    {
        var book = await NewBookAsync();
        await OpenInventoryAsync(book.Title);

        Browser.Click(By.CssSelector($"button[title={Quote($"Remove {book.Title}")}]"));
        AssertContains("This cannot be undone.", Browser.AcceptAlert());

        Assert.Equal($"Successfully removed \"{book.Title}\".", Browser.Visible(PageSuccess).Text);
        Assert.Null(await Api.BookAsync(book.Id));
    }

    [Fact, Trait("Id", "ADMBOOK-12"), Trait("Priority", "P1"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK12_DeletingABookOnLoan_IsRefused()
    {
        var book = await NewBookAsync(2);
        await Api.BorrowAsync(await NewMemberAsync(), book);
        await OpenInventoryAsync(book.Title);

        Browser.Click(By.CssSelector($"button[title={Quote($"Remove {book.Title}")}]"));
        Browser.AcceptAlert();

        AssertContains("A book cannot be removed while copies are currently borrowed.", Browser.Visible(PageError).Text);
        Assert.NotNull(await Api.BookAsync(book.Id));
    }

    [Fact, Trait("Id", "ADMBOOK-13"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK13_CancellingTheDeleteDialog_KeepsTheBook()
    {
        var book = await NewBookAsync();
        await OpenInventoryAsync(book.Title);

        Browser.Click(By.CssSelector($"button[title={Quote($"Remove {book.Title}")}]"));
        Browser.DismissAlert();

        Assert.True(Browser.Exists(Browser.RowContaining(book.Title)));
        Assert.NotNull(await Api.BookAsync(book.Id));
    }

    [Fact, Trait("Id", "ADMBOOK-14"), Trait("Priority", "P2"), Trait("Type", "Happy")]
    public async Task ADMBOOK14_Admin_AddsRenamesAndDeletesAGenre()
    {
        var name = $"E2E Genre {TestIds.Short()}";
        var renamed = name + " Renamed";
        await OpenInventoryAsync();
        Browser.ClickButton("Manage genres");

        Browser.Type(By.Id("new-genre-name"), name);
        Browser.Click(By.XPath("//dialog[@open]//form[contains(@class,'modal-inline-form')]//button[@type='submit']"));
        Browser.Until(() => Browser.Exists(By.CssSelector($"button[title={Quote($"Edit {name}")}]")), what: "the new genre");
        TrackGenre((await Api.GenreIdAsync(name))!.Value);

        Browser.Click(By.CssSelector($"button[title={Quote($"Edit {name}")}]"));
        Browser.Type(Aria($"New name for {name}"), renamed);
        Browser.Click(By.XPath("//dialog[@open]//button[normalize-space(.)='Save']"));
        Browser.Until(() => Browser.Exists(By.CssSelector($"button[title={Quote($"Edit {renamed}")}]")), what: "the renamed genre");

        Browser.Click(By.CssSelector("dialog[open] .modal-close"));
        OpenAddForm();
        Assert.Contains(renamed, new OpenQA.Selenium.Support.UI.SelectElement(Browser.Find(By.Id("book-genre"))).Options.Select(option => option.Text));
        Browser.Click(By.CssSelector("dialog[open] .modal-close"));

        Browser.ClickButton("Manage genres");
        Browser.Click(By.CssSelector($"button[title={Quote($"Delete {renamed}")}]"));
        Browser.AcceptAlert();
        Browser.Until(() => !Browser.Exists(By.CssSelector($"button[title={Quote($"Delete {renamed}")}]")), what: "the genre to be deleted");
        Assert.Null(await Api.GenreIdAsync(renamed));
    }

    [Fact, Trait("Id", "ADMBOOK-15"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK15_DuplicateGenre_IsRefused()
    {
        await OpenInventoryAsync();
        Browser.ClickButton("Manage genres");

        Browser.Type(By.Id("new-genre-name"), TestGenre);
        Browser.Click(By.XPath("//dialog[@open]//form[contains(@class,'modal-inline-form')]//button[@type='submit']"));

        Assert.Equal($"A genre named '{TestGenre}' already exists.", Browser.Visible(By.CssSelector("dialog[open] .modal-error")).Text);
    }

    [Fact, Trait("Id", "ADMBOOK-16"), Trait("Priority", "P2"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK16_GenreInUse_CannotBeDeleted()
    {
        var name = $"E2E Genre {TestIds.Short()}";
        await Api.CreateGenreAsync(name);
        TrackGenre((await Api.GenreIdAsync(name))!.Value);
        await NewBookAsync(genre: name);
        await OpenInventoryAsync();
        Browser.ClickButton("Manage genres");

        Browser.Click(By.CssSelector($"button[title={Quote($"Delete {name}")}]"));
        Browser.AcceptAlert();

        Assert.Equal("This genre cannot be deleted while it is assigned to a book.", Browser.Visible(By.CssSelector("dialog[open] .modal-error")).Text);
        Assert.NotNull(await Api.GenreIdAsync(name));
    }

    [Fact, Trait("Id", "ADMBOOK-17"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK17_InventoryDown_ShowsTheConnectionError()
    {
        var token = await AdminTokenAsync();
        StopContainer(Containers.Inventory);

        Browser.SignInAsAdmin(token, "/admin/books");

        Browser.WaitForText("Unable to connect to the Inventory Service. Please ensure it is running on port 5001.", TimeSpan.FromSeconds(30));
        await StartContainerAsync(Containers.Inventory);
    }

    [Fact, Trait("Id", "ADMBOOK-18"), Trait("Priority", "P3"), Trait("Type", "Unhappy")]
    public async Task ADMBOOK18_KafkaDown_SavesTheBookButReportsTheMissingEvent()
    {
        var title = UniqueTitle("NoKafka");
        await OpenInventoryAsync();
        StopContainer(Containers.Kafka);
        OpenAddForm();

        FillAddForm(title, "E2E Author", TestIds.Isbn(), "1");
        SubmitDialog("Add book");

        Assert.Equal("The database change was saved, but the Kafka event could not be published.",
            Browser.Visible(DialogError, TimeSpan.FromSeconds(40)).Text);
        var id = await BookIdByTitleAsync(title);
        Assert.NotNull(id);
        TrackBook(id!.Value);
        await StartContainerAsync(Containers.Kafka);
    }

    [Fact, Trait("Id", "ADMBOOK-19"), Trait("Priority", "P3"), Trait("Type", "Happy")]
    public async Task ADMBOOK19_Dialog_FocusesTheFirstField_AndEscapeClosesIt()
    {
        await OpenInventoryAsync();

        OpenAddForm();

        Browser.Until(() => (string?)Browser.Js("return document.activeElement.id;") == "book-title", what: "focus on the title field");
        Browser.Find(By.Id("book-title")).SendKeys(Keys.Escape);
        Browser.Until(() => Browser.OpenDialog == null, what: "the dialog to close");
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
