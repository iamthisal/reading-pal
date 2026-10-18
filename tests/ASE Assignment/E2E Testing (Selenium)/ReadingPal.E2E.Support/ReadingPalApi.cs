using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ReadingPal.E2E.Support;

public sealed record Member(int Id, string Email, string Password, string FirstName, string LastName, string Token)
{
    public string FullName => $"{FirstName} {LastName}";
}

public sealed record BookInfo(int Id, string Title, string Author, string Isbn, string Genre, int TotalCopies);

/// <summary>An HTTP response kept whole, so a test can check the status and the message together.</summary>
public sealed record ApiResult(HttpStatusCode Status, JsonNode? Body)
{
    public bool Ok => (int)Status is >= 200 and < 300;
    public string Message => Body?["message"]?.GetValue<string>() ?? string.Empty;
}

/// <summary>
/// Calls the four services' REST APIs to arrange test data, so each test drives only the behaviour it
/// is about through the browser (TEST-PLAN.md §3).
/// </summary>
public sealed class ReadingPalApi
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static string? _adminToken;
    private static DateTime _adminTokenAt;

    public async Task<ApiResult> SendAsync(HttpMethod method, string url, string? token = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        JsonNode? json = null;
        if (text.Length > 0)
        {
            try { json = JsonNode.Parse(text); } catch { json = JsonValue.Create(text); }
        }
        return new ApiResult(response.StatusCode, json);
    }

    private async Task<JsonNode> RequireAsync(HttpMethod method, string url, string? token = null, object? body = null)
    {
        var result = await SendAsync(method, url, token, body);
        if (!result.Ok) throw new InvalidOperationException($"{method} {url} failed with {(int)result.Status}: {result.Body?.ToJsonString()}");
        return result.Body ?? new JsonObject();
    }

    // ---------- Accounts ----------

    public async Task<ApiResult> LoginAsync(string email, string password) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.UserServiceUrl}/api/auth/login", body: new { email, password });

    public async Task<string> TokenForAsync(string email, string password)
    {
        var result = await LoginAsync(email, password);
        if (!result.Ok) throw new InvalidOperationException($"Login failed for {email}: {result.Message}");
        return result.Body!["token"]!.GetValue<string>();
    }

    public async Task<string> AdminTokenAsync()
    {
        // Tokens last two hours; renew well before that.
        if (_adminToken == null || DateTime.UtcNow - _adminTokenAt > TimeSpan.FromMinutes(60))
        {
            _adminToken = await TokenForAsync(E2EConfig.AdminEmail, E2EConfig.AdminPassword);
            _adminTokenAt = DateTime.UtcNow;
        }
        return _adminToken;
    }

    public async Task<ApiResult> RegisterAsync(string firstName, string lastName, string email, string password) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.UserServiceUrl}/api/auth/register", body: new { firstName, lastName, email, password });

    /// <summary>Registers a member who has not been approved yet.</summary>
    public async Task<Member> CreatePendingMemberAsync(string? firstName = null)
    {
        var suffix = TestIds.Short();
        var first = firstName ?? "Reader";
        var last = $"E2E{suffix}";
        var email = $"e2e.{suffix}@reader.test";
        const string password = TestIds.MemberPassword;
        var registered = await RegisterAsync(first, last, email, password);
        if (!registered.Ok) throw new InvalidOperationException($"Register failed: {registered.Message}");
        var id = await PendingUserIdAsync(email) ?? throw new InvalidOperationException($"{email} is not in the pending list.");
        return new Member(id, email, password, first, last, await TokenForAsync(email, password));
    }

    /// <summary>Registers a member and approves them, then signs in again so the token says "approved".</summary>
    public async Task<Member> CreateApprovedMemberAsync(string? firstName = null)
    {
        var pending = await CreatePendingMemberAsync(firstName);
        await ApproveMemberAsync(pending.Id);
        return pending with { Token = await TokenForAsync(pending.Email, pending.Password) };
    }

    public async Task<int?> PendingUserIdAsync(string email)
    {
        var list = await RequireAsync(HttpMethod.Get, $"{E2EConfig.UserServiceUrl}/api/admin/users/pending", await AdminTokenAsync());
        return list.AsArray().FirstOrDefault(user => (string?)user?["email"] == email)?["id"]?.GetValue<int>();
    }

    public async Task<int?> ActiveUserIdAsync(string email)
    {
        var list = await RequireAsync(HttpMethod.Get, $"{E2EConfig.UserServiceUrl}/api/admin/users/active", await AdminTokenAsync());
        return list.AsArray().FirstOrDefault(user => (string?)user?["email"] == email)?["id"]?.GetValue<int>();
    }

    public async Task ApproveMemberAsync(int userId) =>
        await RequireAsync(HttpMethod.Post, $"{E2EConfig.UserServiceUrl}/api/admin/users/{userId}/accept", await AdminTokenAsync());

    public async Task<ApiResult> RevokeMemberAsync(int userId) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.UserServiceUrl}/api/admin/users/{userId}/revoke", await AdminTokenAsync());

    public async Task<ApiResult> RejectMemberAsync(int userId) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.UserServiceUrl}/api/admin/users/{userId}/reject", await AdminTokenAsync());

    /// <summary>Removes a test member: an approved one is revoked first, because only pending users can be rejected.</summary>
    public async Task DeleteMemberAsync(string email)
    {
        if (await ActiveUserIdAsync(email) is { } activeId) await RevokeMemberAsync(activeId);
        if (await PendingUserIdAsync(email) is { } pendingId) await RejectMemberAsync(pendingId);
    }

    public async Task<JsonNode> ProfileAsync(string token) =>
        await RequireAsync(HttpMethod.Get, $"{E2EConfig.UserServiceUrl}/api/user/profile", token);

    // ---------- Catalogue ----------

    public async Task<ApiResult> CreateGenreAsync(string name) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.InventoryServiceUrl}/api/genres", await AdminTokenAsync(), new { name });

    public async Task<int?> GenreIdAsync(string name)
    {
        var list = await RequireAsync(HttpMethod.Get, $"{E2EConfig.InventoryServiceUrl}/api/genres");
        return list.AsArray().FirstOrDefault(genre => string.Equals((string?)genre?["name"], name, StringComparison.OrdinalIgnoreCase))?["id"]?.GetValue<int>();
    }

    public async Task<ApiResult> DeleteGenreAsync(int id) =>
        await SendAsync(HttpMethod.Delete, $"{E2EConfig.InventoryServiceUrl}/api/genres/{id}", await AdminTokenAsync());

    public async Task<ApiResult> CreateBookRawAsync(string title, string author, string isbn, string genre, int copies,
        string? description = null, string? coverImageUrl = null) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.InventoryServiceUrl}/api/books", await AdminTokenAsync(),
            new { title, author, isbn, genre, totalCopies = copies, description, coverImageUrl });

    public async Task<BookInfo> CreateBookAsync(string title, string author, string isbn, string genre, int copies,
        string? description = null, string? coverImageUrl = null)
    {
        var result = await CreateBookRawAsync(title, author, isbn, genre, copies, description, coverImageUrl);
        // A 503 here means Kafka was down: the book was still saved.
        if (!result.Ok && result.Status != HttpStatusCode.ServiceUnavailable)
            throw new InvalidOperationException($"Create book failed: {(int)result.Status} {result.Body?.ToJsonString()}");
        var id = result.Body?["id"]?.GetValue<int>() ?? result.Body?["bookId"]?.GetValue<int>()
            ?? throw new InvalidOperationException("Create book returned no id.");
        return new BookInfo(id, title, author, isbn, genre, copies);
    }

    public async Task<JsonNode?> BookAsync(int id)
    {
        var result = await SendAsync(HttpMethod.Get, $"{E2EConfig.InventoryServiceUrl}/api/books/{id}");
        return result.Ok ? result.Body : null;
    }

    public async Task<ApiResult> UpdateBookAsync(BookInfo book, string? title = null, int? copies = null, string? description = null) =>
        await SendAsync(HttpMethod.Put, $"{E2EConfig.InventoryServiceUrl}/api/books/{book.Id}", await AdminTokenAsync(), new
        {
            title = title ?? book.Title, author = book.Author, isbn = book.Isbn, genre = book.Genre,
            totalCopies = copies ?? book.TotalCopies, description
        });

    public async Task<ApiResult> MarkUnavailableAsync(int bookId) =>
        await SendAsync(HttpMethod.Patch, $"{E2EConfig.InventoryServiceUrl}/api/books/{bookId}/mark-unavailable", await AdminTokenAsync());

    public async Task<ApiResult> MarkAvailableAsync(int bookId) =>
        await SendAsync(HttpMethod.Patch, $"{E2EConfig.InventoryServiceUrl}/api/books/{bookId}/mark-available", await AdminTokenAsync());

    public async Task<ApiResult> DeleteBookAsync(int bookId) =>
        await SendAsync(HttpMethod.Delete, $"{E2EConfig.InventoryServiceUrl}/api/books/{bookId}", await AdminTokenAsync());

    public async Task<ApiResult> CreateAuthorAsync(string name, string? description = null, string? imageUrl = null) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.InventoryServiceUrl}/api/authors", await AdminTokenAsync(), new { name, description, imageUrl });

    public async Task<int?> AuthorIdAsync(string name)
    {
        var list = await RequireAsync(HttpMethod.Get, $"{E2EConfig.InventoryServiceUrl}/api/authors");
        return list.AsArray().FirstOrDefault(author => string.Equals((string?)author?["name"], name, StringComparison.OrdinalIgnoreCase))?["id"]?.GetValue<int>();
    }

    public async Task<int[]> AllAuthorIdsAsync()
    {
        var list = await RequireAsync(HttpMethod.Get, $"{E2EConfig.InventoryServiceUrl}/api/authors");
        return list.AsArray().Select(author => author!["id"]!.GetValue<int>()).ToArray();
    }

    public async Task<ApiResult> DeleteAuthorAsync(int id) =>
        await SendAsync(HttpMethod.Delete, $"{E2EConfig.InventoryServiceUrl}/api/authors/{id}", await AdminTokenAsync());

    public async Task<ApiResult> CreateListAsync(string key, string name, string itemType) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.InventoryServiceUrl}/api/lists", await AdminTokenAsync(), new { key, name, itemType });

    public async Task<ApiResult> SetListItemsAsync(string key, IEnumerable<int> ids) =>
        await SendAsync(HttpMethod.Put, $"{E2EConfig.InventoryServiceUrl}/api/lists/{key}/items", await AdminTokenAsync(), new { itemIds = ids.ToArray() });

    public async Task<JsonNode?> ListAsync(string key)
    {
        var result = await SendAsync(HttpMethod.Get, $"{E2EConfig.InventoryServiceUrl}/api/lists/{key}");
        return result.Ok ? result.Body : null;
    }

    public async Task<ApiResult> DeleteListAsync(string key) =>
        await SendAsync(HttpMethod.Delete, $"{E2EConfig.InventoryServiceUrl}/api/lists/{key}", await AdminTokenAsync());

    public async Task<ApiResult> AddFavouriteAsync(string token, int bookId) =>
        await SendAsync(HttpMethod.Put, $"{E2EConfig.InventoryServiceUrl}/api/favourites/{bookId}", token);

    public async Task<int[]> FavouriteIdsAsync(string token)
    {
        var ids = await RequireAsync(HttpMethod.Get, $"{E2EConfig.InventoryServiceUrl}/api/favourites/ids", token);
        return ids.AsArray().Select(id => id!.GetValue<int>()).ToArray();
    }

    // ---------- Lending ----------

    public async Task<ApiResult> ReserveRawAsync(string token, int bookId) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.LendingServiceUrl}/api/Reservations", token, new { bookId });

    public async Task<int> ReserveAsync(Member member, BookInfo book)
    {
        var result = await ReserveRawAsync(member.Token, book.Id);
        if (!result.Ok) throw new InvalidOperationException($"Reserve failed: {(int)result.Status} {result.Message}");
        return result.Body!["id"]!.GetValue<int>();
    }

    public async Task<ApiResult> AcceptReservationAsync(int reservationId) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.LendingServiceUrl}/api/reservations/{reservationId}/accept", await AdminTokenAsync());

    public async Task<ApiResult> RejectReservationAsync(int reservationId) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.LendingServiceUrl}/api/reservations/{reservationId}/reject", await AdminTokenAsync());

    public async Task<ApiResult> CancelReservationAsync(Member member, int reservationId) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.LendingServiceUrl}/api/my-borrowings/reservations/{reservationId}/cancel", member.Token);

    /// <summary>Reserves and accepts in one step; returns the reservation ID.</summary>
    public async Task<int> BorrowAsync(Member member, BookInfo book)
    {
        var reservationId = await ReserveAsync(member, book);
        var accepted = await AcceptReservationAsync(reservationId);
        if (!accepted.Ok) throw new InvalidOperationException($"Accept failed: {(int)accepted.Status} {accepted.Message}");
        return reservationId;
    }

    public async Task<JsonNode> MyBorrowingsAsync(Member member) =>
        await RequireAsync(HttpMethod.Get, $"{E2EConfig.LendingServiceUrl}/api/my-borrowings", member.Token);

    public async Task<JsonArray> PendingReservationsAsync() =>
        (await RequireAsync(HttpMethod.Get, $"{E2EConfig.LendingServiceUrl}/api/reservations/pending", await AdminTokenAsync())).AsArray();

    public async Task<JsonArray> BorrowedAsync() =>
        (await RequireAsync(HttpMethod.Get, $"{E2EConfig.LendingServiceUrl}/api/reservations/borrowed", await AdminTokenAsync())).AsArray();

    public async Task<int> BorrowRecordIdAsync(int reservationId) =>
        (await BorrowedAsync()).First(record => record!["reservationId"]!.GetValue<int>() == reservationId)!["id"]!.GetValue<int>();

    public async Task<ApiResult> ReturnAsync(int borrowRecordId) =>
        await SendAsync(HttpMethod.Post, $"{E2EConfig.LendingServiceUrl}/api/returns/{borrowRecordId}", await AdminTokenAsync());

    // ---------- Notifications ----------

    public async Task<JsonArray> NotificationsAsync(string token, bool admin)
    {
        var path = admin ? "api/admin/notifications" : "api/notifications";
        return (await RequireAsync(HttpMethod.Get, $"{E2EConfig.NotificationServiceUrl}/{path}?pageSize=50", token)).AsArray();
    }

    /// <summary>Polls until a notification containing <paramref name="text"/> exists, or returns null.</summary>
    public async Task<JsonNode?> WaitForNotificationAsync(string token, bool admin, string text, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? E2EConfig.EventTimeout);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var match = (await NotificationsAsync(token, admin))
                    .FirstOrDefault(item => ((string?)item?["message"] ?? "").Contains(text, StringComparison.Ordinal));
                if (match != null) return match;
            }
            catch (HttpRequestException) { /* service restarting */ }
            catch (InvalidOperationException) { /* service restarting */ }
            await Task.Delay(1000);
        }
        return null;
    }

    public async Task<bool> HealthyAsync(string baseUrl)
    {
        try { return (await Http.GetAsync($"{baseUrl}/health")).IsSuccessStatusCode; }
        catch { return false; }
    }
}
