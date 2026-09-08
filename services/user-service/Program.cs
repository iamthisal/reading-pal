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

// Add JWT Authentication
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddControllers();
builder.Services.AddOpenApi();
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] 
    ?? builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
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
    var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any(u => u.Email == "dahamyakulandi21@gmail.com"))
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

    if (!db.Users.Any(u => u.Email == "dahamku@gmail.com"))
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
