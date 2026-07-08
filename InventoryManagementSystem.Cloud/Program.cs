using System.Text;
using InventoryManagementSystem.Cloud.Data;
using InventoryManagementSystem.Cloud.Models;
using InventoryManagementSystem.Cloud.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<CloudDatabase>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<BackupService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

builder.Services.AddAuthorization();

var app = builder.Build();

var db = app.Services.GetRequiredService<CloudDatabase>();
await db.InitializeAsync();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "Inventory Management System Cloud API",
    tenancy = "organization/workspace",
    docs = "/health"
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }));

app.MapPost("/api/auth/register", async (RegisterRequest request, AuthService auth) =>
{
    var result = await auth.RegisterAsync(request);
    return result is null ? Results.BadRequest(new { error = "Registration failed. Email may already exist." }) : Results.Ok(result);
});

app.MapPost("/api/auth/login", async (LoginRequest request, AuthService auth) =>
{
    var result = await auth.LoginAsync(request);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
});

app.MapGet("/api/backup/info", async (HttpContext http, BackupService backup) =>
{
    var orgId = CloudClaims.GetOrganizationId(http.User);
    if (orgId == Guid.Empty) return Results.Unauthorized();
    return Results.Ok(await backup.GetInfoAsync(orgId));
}).RequireAuthorization();

app.MapPost("/api/backup", async (HttpContext http, BackupService backup) =>
{
    var orgId = CloudClaims.GetOrganizationId(http.User);
    if (orgId == Guid.Empty) return Results.Unauthorized();

    await using var ms = new MemoryStream();
    await http.Request.Body.CopyToAsync(ms);
    ms.Position = 0;

    if (ms.Length == 0) return Results.BadRequest(new { error = "Empty backup payload." });

    await backup.SaveBackupAsync(orgId, ms, ms.Length);
    return Results.Ok(new { success = true, sizeBytes = ms.Length });
}).RequireAuthorization();

app.MapGet("/api/backup", async (HttpContext http, BackupService backup) =>
{
    var orgId = CloudClaims.GetOrganizationId(http.User);
    if (orgId == Guid.Empty) return Results.Unauthorized();

    var backupFile = await backup.GetBackupAsync(orgId);
    if (backupFile is null) return Results.NotFound(new { error = "No backup found for this organization." });

    var (stream, fileName) = backupFile.Value;
    return Results.File(stream, "application/gzip", fileName);
}).RequireAuthorization();

app.MapPost("/api/sync/push", async (HttpContext http, SyncService sync, SyncPushRequest request) =>
{
    var orgId = CloudClaims.GetOrganizationId(http.User);
    if (orgId == Guid.Empty) return Results.Unauthorized();

    var accepted = await sync.PushAsync(orgId, request);
    return Results.Ok(new SyncPushResponse(accepted));
}).RequireAuthorization();

app.MapGet("/api/sync/pull", async (HttpContext http, SyncService sync, DateTime? since) =>
{
    var orgId = CloudClaims.GetOrganizationId(http.User);
    if (orgId == Guid.Empty) return Results.Unauthorized();

    var sinceUtc = since ?? DateTime.MinValue;
    var response = await sync.PullAsync(orgId, sinceUtc.ToUniversalTime());
    return Results.Ok(response);
}).RequireAuthorization();

app.Run();

public partial class Program;
