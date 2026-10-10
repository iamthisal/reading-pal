using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NotificationService.Data;
using NotificationService.Kafka;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<NotificationDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        // Retry transient failures (e.g. a connect timeout during deploy) instead of failing the request or migration.
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)),
            mySql => mySql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null));
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

// Add JWT Authentication (same issuer, audience and key as the User Service)
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
builder.Services.AddScoped<IBookTitleLookup, InventoryBookTitleLookup>();
builder.Services.AddScoped<ICustomerDirectory, UserServiceCustomerDirectory>();
builder.Services.AddScoped<LendingEventHandler>();
builder.Services.AddScoped<CatalogEventHandler>();
builder.Services.AddHostedService<NotificationConsumerWorker>();

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
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
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
