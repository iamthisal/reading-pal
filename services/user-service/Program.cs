using UserService.OpenApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using UserService.Data;
using UserService.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));

// Add CORS
var allowedOrigin = builder.Configuration["Frontend:Url"] ?? "http://localhost:5173";   
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(allowedOrigin).AllowAnyHeader().AllowAnyMethod());
});

// Add JWT Authentication. The signing key is never stored in appsettings.json: it comes from
// appsettings.Development.json locally and from the Jwt__Key setting everywhere else.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key is not configured. Set the Jwt__Key environment variable or app setting (at least 32 bytes).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] 
    ?? builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

var app = builder.Build();

// Public, interactive API docs at /swagger (the endpoints themselves stay JWT-protected).
app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Reading Pal User Service v1"));

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    db.Database.Migrate();

    // Demo accounts with a known password: only ever create them in local development.
    if (app.Environment.IsDevelopment() && !db.Users.Any(u => u.Email == "dahamyakulandi21@gmail.com"))
    {
        db.Users.Add(new User
        {
            Email = "dahamyakulandi21@gmail.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pwd123*"),
            Role = "User",
            FirstName = "Dahamya",
            LastName = "Kulandi",
            IsValidated = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    if (app.Environment.IsDevelopment() && !db.Users.Any(u => u.Email == "dahamku@gmail.com"))
    {
        db.Users.Add(new User
        {
            Email = "dahamku@gmail.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pwd123*"),
            Role = "User",
            FirstName = "Dahamya",
            LastName = "Kulandi",
            IsValidated = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    db.SaveChanges();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not run database migration/seed on startup.");
}

app.Run();
