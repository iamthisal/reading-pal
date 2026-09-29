using System.Text;
using LendingService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using LendingService.Kafka;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<LendingDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)));
    }
});

// Add CORS
var allowedOrigin = builder.Configuration["Frontend:Url"] ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(allowedOrigin, "http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ThisIsAVerySecretKeyThatShouldBeStoredSecurelyAndLongEnough";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ReadingPal.UserService";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ReadingPal.Frontend";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHttpClient();
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<IProducer<string, string>>(services =>
{
    var options = services.GetRequiredService<IOptions<KafkaOptions>>().Value;
    if (string.IsNullOrWhiteSpace(options.BootstrapServers)
        || !Enum.TryParse<SecurityProtocol>(options.SecurityProtocol, true, out var protocol))
        throw new InvalidOperationException("Configure Kafka BootstrapServers and a valid SecurityProtocol.");
    return new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = options.BootstrapServers,
        SecurityProtocol = protocol,
        EnableIdempotence = true,
        Acks = Acks.All,
        MessageTimeoutMs = 10000
    }).Build();
});
builder.Services.AddSingleton<IReservationEventPublisher, KafkaReservationEventPublisher>();
builder.Services.AddScoped<ReservationOutboxDispatcher>();
builder.Services.AddHostedService<ReservationOutboxWorker>();

var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] 
    ?? builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LendingDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not run database migration on startup.");
}

app.Run();

// Needed for WebApplicationFactory in integration tests
public partial class Program { }
