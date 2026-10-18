using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MySqlConnector;

namespace ReadingPal.E2E.Support;

public static class TestIds
{
    /// <summary>Password for every member the tests register. A test value for the local app only.</summary>
    public const string MemberPassword = "E2e-pass-123";

    /// <summary>A short random suffix that keeps each test's data apart from every other run.</summary>
    public static string Short() => Guid.NewGuid().ToString("N")[..8];

    public static string Isbn() => $"E2E-{Guid.NewGuid():N}"[..20];
}

/// <summary>
/// Direct MySQL access for the two things the UI cannot set up: a second admin account and loans whose
/// dates lie in the past (TEST-PLAN.md §3).
/// </summary>
public static class Database
{
    public static async Task<int> ExecuteAsync(string database, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new MySqlConnection(E2EConfig.MySqlConnection(database));
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>Moves a loan into the past so it is <paramref name="daysOverdue"/> Sri Lankan days late (negative: due in that many days).</summary>
    public static Task BackdateLoanAsync(int reservationId, int daysOverdue)
    {
        var due = DateTime.UtcNow.AddDays(-daysOverdue);
        return ExecuteAsync(E2EConfig.LendingDatabase,
            "UPDATE BorrowRecords SET DueDate = @due, CheckoutDate = @checkout, DueReminderSentAtUtc = NULL WHERE ReservationId = @id",
            ("@due", due), ("@checkout", due.AddDays(-14)), ("@id", reservationId));
    }

    /// <summary>Puts a loan's due date at a given UTC moment, for the Sri Lanka midnight boundary case.</summary>
    public static Task SetDueDateAsync(int reservationId, DateTime dueUtc) =>
        ExecuteAsync(E2EConfig.LendingDatabase,
            "UPDATE BorrowRecords SET DueDate = @due, CheckoutDate = @checkout WHERE ReservationId = @id",
            ("@due", dueUtc), ("@checkout", dueUtc.AddDays(-14)), ("@id", reservationId));

    public static Task PromoteToAdminAsync(string email) =>
        ExecuteAsync(E2EConfig.UserDatabase, "UPDATE Users SET Role = 'Admin', IsValidated = 1 WHERE Email = @email", ("@email", email));
}

/// <summary>Stops and starts the local Docker containers for the outage (resilience) cases.</summary>
public static class Containers
{
    public const string Inventory = "readingpal-inventory-service";
    public const string Lending = "readingpal-lending-service";
    public const string User = "readingpal-user-service";
    public const string Notification = "readingpal-notification-service";
    public const string Kafka = "readingpal-events-kafka-1";

    public static void Run(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("docker", arguments)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        }) ?? throw new InvalidOperationException("docker could not be started.");
        process.WaitForExit(120_000);
        if (process.ExitCode != 0) throw new InvalidOperationException($"docker {arguments}: {process.StandardError.ReadToEnd()}");
    }

    public static void Stop(string container) => Run($"stop -t 5 {container}");
    public static void Pause(string container) => Run($"pause {container}");
    public static void Unpause(string container) => Run($"unpause {container}");

    /// <summary>Starts a container and waits until its health endpoint answers again.</summary>
    public static async Task StartAsync(string container)
    {
        Run($"start {container}");
        if (container == Kafka)
        {
            // Kafka has no HTTP health endpoint; wait for Docker's health check, then let clients reconnect.
            var deadline = DateTime.UtcNow.AddMinutes(3);
            while (DateTime.UtcNow < deadline && !KafkaHealthy()) await Task.Delay(2000);
            await Task.Delay(15_000);
            return;
        }
        var url = container switch
        {
            Inventory => E2EConfig.InventoryServiceUrl,
            Lending => E2EConfig.LendingServiceUrl,
            User => E2EConfig.UserServiceUrl,
            _ => E2EConfig.NotificationServiceUrl
        };
        var api = new ReadingPalApi();
        var until = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < until && !await api.HealthyAsync(url)) await Task.Delay(1000);
        // The Notification Service also has to rejoin its Kafka consumer group.
        if (container == Notification) await Task.Delay(10_000);
    }

    public static async Task RestartAsync(string container)
    {
        Stop(container);
        await StartAsync(container);
    }

    private static bool KafkaHealthy()
    {
        using var process = Process.Start(new ProcessStartInfo("docker", $"inspect -f {{{{.State.Health.Status}}}} {Kafka}")
        {
            RedirectStandardOutput = true, UseShellExecute = false
        });
        if (process == null) return false;
        var status = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return status == "healthy";
    }
}

/// <summary>
/// Builds unsigned JWTs for the session cases. The frontend only decodes the token (it never checks the
/// signature), so these exercise its expiry handling; the services reject them, as they should.
/// </summary>
public static class FakeJwt
{
    public static string Create(DateTimeOffset expires, string role = "User", bool validated = true, string email = "fake@reader.test")
    {
        static string Encode(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = Encode(new { alg = "HS256", typ = "JWT" });
        var payload = Encode(new Dictionary<string, object>
        {
            ["sub"] = "999999",
            ["email"] = email,
            ["role"] = role,
            ["IsValidated"] = validated ? "True" : "False",
            ["exp"] = expires.ToUnixTimeSeconds()
        });
        return $"{header}.{payload}.c2lnbmF0dXJl";
    }
}
