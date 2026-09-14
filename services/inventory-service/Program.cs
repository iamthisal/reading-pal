using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using InventoryService.Data;
using InventoryService.Kafka;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<InventoryDbContext>(options =>
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
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<IProducer<string, string>>(serviceProvider =>
{
    var kafkaOptions = serviceProvider.GetRequiredService<IOptions<KafkaOptions>>().Value;
    if (string.IsNullOrWhiteSpace(kafkaOptions.BootstrapServers))
    {
        throw new InvalidOperationException("Kafka:BootstrapServers must be configured.");
    }

    if (!Enum.TryParse<SecurityProtocol>(kafkaOptions.SecurityProtocol, ignoreCase: true, out var securityProtocol))
    {
        throw new InvalidOperationException($"Unsupported Kafka security protocol '{kafkaOptions.SecurityProtocol}'.");
    }

    var producerConfig = new ProducerConfig
    {
        BootstrapServers = kafkaOptions.BootstrapServers,
        SecurityProtocol = securityProtocol,
        EnableIdempotence = true,
        Acks = Acks.All,
        MessageTimeoutMs = 10000
    };

    return new ProducerBuilder<string, string>(producerConfig).Build();
});
builder.Services.AddSingleton<IBookEventPublisher, KafkaBookEventPublisher>();

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
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
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
