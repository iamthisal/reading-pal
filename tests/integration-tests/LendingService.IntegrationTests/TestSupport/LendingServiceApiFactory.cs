using LendingService.Data;
using LendingService.Kafka;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LendingService.IntegrationTests.TestSupport;

/// <summary>
/// Boots the real LendingService host (real routing, JWT auth pipeline, controllers, JSON) through
/// <see cref="WebApplicationFactory{TEntryPoint}"/>, replacing only the true external boundaries:
/// the MySQL-backed <see cref="LendingDbContext"/> (swapped for a fresh EF Core InMemory database
/// per factory instance) and outbound calls to User Service / Inventory Service (swapped for
/// <see cref="Responder"/>, a fake <see cref="HttpMessageHandler"/> each test configures).
/// The Kafka publisher is also swapped for an in-memory recorder so no real broker is contacted.
///
/// Create a fresh instance per test (implements <see cref="IDisposable"/> via the base class) so
/// tests never share a database or a pending HTTP response with one another.
/// </summary>
public sealed class LendingServiceApiFactory : WebApplicationFactory<Program>
{
    // Program.cs reads Jwt:Key/Issuer/Audience into local variables while the host is being built,
    // before ConfigureAppConfiguration below runs, so overriding them there has no effect. Environment
    // variables are read when the builder is created, so they do reach the app; JwtTestTokenFactory
    // signs its tokens with these same values.
    public const string JwtKey = "integration-tests-only-signing-key-0123456789";
    public const string JwtIssuer = "ReadingPal.UserService";
    public const string JwtAudience = "ReadingPal.Frontend";

    static LendingServiceApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", JwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", JwtAudience);
    }

    private readonly string _databaseName = Guid.NewGuid().ToString();

    public RespondingHttpMessageHandler Responder { get; } = new();

    public RecordingEventPublisher Publisher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        // These two ARE read fresh from IConfiguration at request time (inside controller
        // methods), not captured into locals at startup, so overriding them here does work.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UserService:BaseUrl"] = "http://user-service.test",
                ["InventoryService:BaseUrl"] = "http://inventory-service.test"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<LendingDbContext>>();
            services.RemoveAll<LendingDbContext>();
            services.AddDbContext<LendingDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            // Real Program.cs registers IHttpClientFactory via AddHttpClient(); replace it so every
            // CreateClient() call in the controllers is backed by this test's fake handler instead
            // of attempting a real network call to User/Inventory Service.
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(_ => new FakeHttpClientFactory(Responder));

            // Replace the Kafka-backed publisher so the ReservationOutboxWorker background service
            // (which runs for real during these tests) never tries to reach a live Kafka broker.
            services.RemoveAll<IReservationEventPublisher>();
            services.AddSingleton<IReservationEventPublisher>(Publisher);
        });
    }

    /// <summary>
    /// Opens a fresh, independently-disposable <see cref="LendingDbContext"/> against this
    /// factory's database, for seeding state before a request or asserting state after one.
    /// Deliberately built outside the app's DI container (rather than via <c>Services.CreateScope()</c>)
    /// so the caller owns its lifetime with a plain <c>using</c>, same as the unit tests' <c>ControllerTestFactory</c>.
    /// </summary>
    public LendingDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LendingDbContext>().UseInMemoryDatabase(_databaseName).Options);
}
