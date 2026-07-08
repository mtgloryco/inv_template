using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryManagementSystem.Cloud.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace InventoryManagementSystem.Tests;

public class CloudSyncApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CloudSyncApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Register_Login_Push_Pull_Backup_Flow_Works()
    {
        var client = _factory.CreateClient();
        var email = $"sync-{Guid.NewGuid():N}@example.com";
        const string password = "Secret123!";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password, "Test Org"));
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var pushResponse = await client.PostAsJsonAsync("/api/sync/push", new SyncPushRequest(
            "test-device",
            new List<SyncChangeRecord>
            {
                new(
                    "Product",
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    JsonSerializer.Serialize(new
                    {
                        SyncId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        Name = "Server Product",
                        SKU = "SP-1",
                        Unit = "Pcs",
                        Price = 12.5m,
                        Cost = 7m,
                        StockQuantity = 3,
                        Category = "General",
                        UpdatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    }),
                    DateTime.UtcNow,
                    false)
            }));

        pushResponse.EnsureSuccessStatusCode();
        var pushResult = await pushResponse.Content.ReadFromJsonAsync<SyncPushResponse>();
        Assert.NotNull(pushResult);
        Assert.Equal(1, pushResult.Accepted);

        var pullResponse = await client.GetAsync("/api/sync/pull?since=1970-01-01T00:00:00Z");
        pullResponse.EnsureSuccessStatusCode();
        var pullResult = await pullResponse.Content.ReadFromJsonAsync<SyncPullResponse>();
        Assert.NotNull(pullResult);
        Assert.NotEmpty(pullResult.Changes);

        await using var backupStream = new MemoryStream(new byte[] { 0x1f, 0x8b, 0x08, 0x00 });
        using var backupContent = new StreamContent(backupStream);
        backupContent.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
        var backupUpload = await client.PostAsync("/api/backup", backupContent);
        backupUpload.EnsureSuccessStatusCode();

        var backupInfo = await client.GetFromJsonAsync<BackupInfoResponse>("/api/backup/info");
        Assert.NotNull(backupInfo);
        Assert.True(backupInfo.Exists);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        loginResponse.EnsureSuccessStatusCode();
    }
}
