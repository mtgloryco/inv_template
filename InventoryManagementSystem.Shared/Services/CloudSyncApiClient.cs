using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services
{
    public class CloudSyncApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string _baseUrl = CloudSyncDefaults.DefaultApiBaseUrl;

        public string? AuthToken { get; set; }
        public string? OrganizationId { get; set; }

        public CloudSyncApiClient(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public string BaseUrl
        {
            get => _baseUrl;
            set => _baseUrl = value.TrimEnd('/');
        }

        public async Task<CloudAuthResponse> RegisterAsync(string email, string password, string organizationName, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/api/auth/register",
                new CloudAuthRequest { Email = email, Password = password, OrganizationName = organizationName },
                ct);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CloudAuthResponse>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Empty auth response.");
            ApplyAuth(result);
            return result;
        }

        public async Task<CloudAuthResponse> LoginAsync(string email, string password, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/api/auth/login",
                new CloudAuthRequest { Email = email, Password = password },
                ct);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CloudAuthResponse>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Empty auth response.");
            ApplyAuth(result);
            return result;
        }

        public async Task<BackupInfoResponse> GetBackupInfoAsync(CancellationToken ct = default)
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Get, $"{BaseUrl}/api/backup/info");
            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<BackupInfoResponse>(cancellationToken: ct)
                ?? new BackupInfoResponse();
        }

        public async Task UploadBackupAsync(Stream compressedDatabase, CancellationToken ct = default)
        {
            using var content = new StreamContent(compressedDatabase);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");

            using var request = CreateAuthorizedRequest(HttpMethod.Post, $"{BaseUrl}/api/backup");
            request.Content = content;

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }

        public async Task<Stream> DownloadBackupAsync(CancellationToken ct = default)
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Get, $"{BaseUrl}/api/backup");
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync(ct);
        }

        public async Task<SyncPushResponse> PushChangesAsync(SyncPushRequest pushRequest, CancellationToken ct = default)
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Post, $"{BaseUrl}/api/sync/push");
            request.Content = JsonContent.Create(pushRequest);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SyncPushResponse>(cancellationToken: ct)
                ?? new SyncPushResponse();
        }

        public async Task<SyncPullResponse> PullChangesAsync(DateTime sinceUtc, CancellationToken ct = default)
        {
            var since = Uri.EscapeDataString(sinceUtc.ToUniversalTime().ToString("O"));
            using var request = CreateAuthorizedRequest(HttpMethod.Get, $"{BaseUrl}/api/sync/pull?since={since}");
            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SyncPullResponse>(cancellationToken: ct)
                ?? new SyncPullResponse { ServerTime = DateTime.UtcNow };
        }

        private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrWhiteSpace(AuthToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
            }

            if (!string.IsNullOrWhiteSpace(OrganizationId))
            {
                request.Headers.Add("X-Organization-Id", OrganizationId);
            }

            return request;
        }

        private void ApplyAuth(CloudAuthResponse result)
        {
            AuthToken = result.Token;
            OrganizationId = result.OrganizationId;
        }

        public void Dispose() => _httpClient.Dispose();
    }

    public static class CloudSyncDefaults
    {
        public const string DefaultApiBaseUrl = "http://localhost:5080";

        public static string ResolveApiBaseUrl()
        {
            var fromEnv = Environment.GetEnvironmentVariable("IMS_CLOUD_API_URL");
            return string.IsNullOrWhiteSpace(fromEnv) ? DefaultApiBaseUrl : fromEnv.TrimEnd('/');
        }
    }
}
