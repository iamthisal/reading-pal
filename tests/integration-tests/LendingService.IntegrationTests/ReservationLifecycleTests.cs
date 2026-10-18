using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using LendingService.IntegrationTests.TestSupport;

namespace LendingService.IntegrationTests;

/// <summary>
/// Drives a reservation through its whole real lifecycle via HTTP, the way a client actually
/// would — proving the endpoints work together, not just in isolation. This is the scenario
/// unit tests structurally cannot cover: they call one controller action at a time against a
/// hand-built DbContext, never a previous request's own committed state.
/// </summary>
public class ReservationLifecycleTests : IntegrationTestBase
{
    private const int BookId = 10;
    private const string UserId = "20";

    private static HttpResponseMessage RespondToEveryDownstreamCall(HttpRequestMessage request, DateTime checkoutDateUtc)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get && path.Contains("/api/user/profile"))
            return JsonResponse.Ok(new { isValidated = true });   // reserving checks the member is still approved (#92)
        if (request.Method == HttpMethod.Get && path.Contains("/api/admin/users/active"))
            return JsonResponse.Ok(new[] { new { id = 20, firstName = "Jane", lastName = "Doe" } });
        if (request.Method == HttpMethod.Get && path.Contains("/api/admin/users/pending"))
            return JsonResponse.Ok(Array.Empty<object>());
        if (request.Method == HttpMethod.Get && path.Contains($"/api/Books/{BookId}"))
            return JsonResponse.Ok(new { title = "Clean Code", availableCopies = 3 });
        if (request.Method == HttpMethod.Post && path.Contains("/return"))
            return JsonResponse.Ok(new { });
        if (request.Method == HttpMethod.Post && path.Contains("/checkouts/"))
            return JsonResponse.Ok(new { checkoutDateUtc });
        throw new InvalidOperationException($"Unexpected {request.Method} {path}");
    }

    [Fact]
    public async Task ReserveAcceptAndReturn_FlowsCorrectlyThroughAllEndpointsAndLeavesConsistentHistory()
    {
        var checkoutDate = DateTime.UtcNow;
        Factory.Responder.Responder = request => RespondToEveryDownstreamCall(request, checkoutDate);

        using var userClient = CreateClient(JwtTestTokenFactory.CreateUserToken(UserId));
        using var adminClient = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        // 1. User reserves the book.
        var reserveResponse = await userClient.PostAsJsonAsync("/api/reservations", new { bookId = BookId });
        Assert.Equal(HttpStatusCode.Created, reserveResponse.StatusCode);
        var reservationId = (await BodyAsync(reserveResponse)).GetProperty("id").GetInt32();

        // 2. It shows up in the admin's pending queue.
        var pendingResponse = await adminClient.GetAsync("/api/reservations/pending");
        var pendingIds = (await BodyAsync(pendingResponse)).EnumerateArray().Select(e => e.GetProperty("id").GetInt32());
        Assert.Contains(reservationId, pendingIds);

        // 3. Admin accepts it; Inventory confirms the checkout.
        var acceptResponse = await adminClient.PostAsync($"/api/reservations/{reservationId}/accept", content: null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var acceptBody = await BodyAsync(acceptResponse);
        Assert.Equal("Borrowed", acceptBody.GetProperty("status").GetString());
        var borrowRecordId = acceptBody.GetProperty("borrowRecordId").GetInt32();

        // 4. It now shows up in the admin's borrowed queue, not overdue.
        var borrowedResponse = await adminClient.GetAsync("/api/reservations/borrowed");
        var borrowedEntry = (await BodyAsync(borrowedResponse)).EnumerateArray()
            .Single(e => e.GetProperty("reservationId").GetInt32() == reservationId);
        Assert.False(borrowedEntry.GetProperty("isOverdue").GetBoolean());

        // 5. Admin returns the book on time.
        var returnResponse = await adminClient.PostAsync($"/api/returns/{borrowRecordId}", content: null);
        Assert.Equal(HttpStatusCode.OK, returnResponse.StatusCode);
        var returnBody = await BodyAsync(returnResponse);
        Assert.Equal(0m, returnBody.GetProperty("fineAmount").GetDecimal());

        // 6. It has left both the pending and borrowed queues...
        var pendingAfter = await adminClient.GetAsync("/api/reservations/pending");
        Assert.DoesNotContain((await BodyAsync(pendingAfter)).EnumerateArray(), e => e.GetProperty("id").GetInt32() == reservationId);
        var borrowedAfter = await adminClient.GetAsync("/api/reservations/borrowed");
        Assert.DoesNotContain((await BodyAsync(borrowedAfter)).EnumerateArray(), e => e.GetProperty("reservationId").GetInt32() == reservationId);

        // ...and now appears in history as Returned, with no fine.
        var historyResponse = await adminClient.GetAsync("/api/reservations/history");
        var historyEntry = (await BodyAsync(historyResponse)).EnumerateArray().Single(e => e.GetProperty("id").GetInt32() == reservationId);
        Assert.Equal("Returned", historyEntry.GetProperty("status").GetString());
        Assert.Equal(0, historyEntry.GetProperty("daysOverdue").GetInt32());

        // 7. And in the user's own borrowing history, no longer active.
        var myBorrowingsResponse = await userClient.GetAsync("/api/my-borrowings");
        var myBorrowingsBody = await BodyAsync(myBorrowingsResponse);
        Assert.DoesNotContain(myBorrowingsBody.GetProperty("pending").EnumerateArray(), e => e.GetProperty("id").GetInt32() == reservationId);
        Assert.DoesNotContain(myBorrowingsBody.GetProperty("active").EnumerateArray(), e => e.GetProperty("id").GetInt32() == borrowRecordId);
        Assert.Contains(myBorrowingsBody.GetProperty("history").EnumerateArray(), e => e.GetProperty("id").GetInt32() == borrowRecordId);
    }

    [Fact]
    public async Task ReserveThenCancel_RemovesItFromThePendingQueue()
    {
        Factory.Responder.Responder = request => RespondToEveryDownstreamCall(request, DateTime.UtcNow);
        using var userClient = CreateClient(JwtTestTokenFactory.CreateUserToken(UserId));
        using var adminClient = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var reserveResponse = await userClient.PostAsJsonAsync("/api/reservations", new { bookId = BookId });
        var reservationId = (await BodyAsync(reserveResponse)).GetProperty("id").GetInt32();

        var cancelResponse = await userClient.PostAsync($"/api/my-borrowings/reservations/{reservationId}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var pendingResponse = await adminClient.GetAsync("/api/reservations/pending");
        Assert.DoesNotContain((await BodyAsync(pendingResponse)).EnumerateArray(), e => e.GetProperty("id").GetInt32() == reservationId);

        using var db = Factory.CreateDbContext();
        var reservation = await db.Reservations.SingleAsync(r => r.Id == reservationId);
        Assert.Equal("Cancelled", reservation.Status);
    }
}
