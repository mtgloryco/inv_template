using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace InventoryManagementSystem.Services;

public record UpdateResult(bool Success, string? Version = null, string? ReleaseNotesUrl = null, bool IsDevMode = false);

public class AppConfig
{
    public string updateUrl { get; set; } = "https://ims-lilac-beta.vercel.app/updates";
}

public class UpdateService
{
    private UpdateManager? _mgr;
    public string CurrentVersion { get; private set; } = "1.0.0";
    private bool _isInitialized = false;
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient();
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        string updateUrl = "https://ims-lilac-beta.vercel.app/updates"; // Default fallback

        try
        {
            // Fetch Remote Configuration
            // This allows us to change the update provider (e.g. move to S3) without changing the app code.
            // We use the stable Vercel URL as the entry point.
            var config = await _httpClient.GetFromJsonAsync<AppConfig>("https://ims-lilac-beta.vercel.app/api/public/config");
            if (config != null && !string.IsNullOrWhiteSpace(config.updateUrl))
            {
                updateUrl = config.updateUrl;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch remote config: {ex.Message}. Using default URL.");
        }

        Console.WriteLine($"Initializing UpdateManager with Source: {updateUrl}");

        try 
        {
            IUpdateSource source;
            if (updateUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            {
                // Smart GitHub Source
                Console.WriteLine("Using GithubSource");
                source = new GithubSource(updateUrl, null, false);
            }
            else
            {
                // Standard Web Source
                Console.WriteLine("Using SimpleWebSource");
                source = new SimpleWebSource(updateUrl);
            }

            _mgr = new UpdateManager(source);
            CurrentVersion = _mgr.CurrentVersion?.ToString() ?? "1.0.0";
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize UpdateManager: {ex.Message}");
            CurrentVersion = "Unknown";
        }
    }

    public async Task<UpdateResult> CheckForUpdatesAsync()
    {
        if (!_isInitialized) await InitializeAsync();

        try
        {
             // If still null after init attempt (e.g. running on Linux without Velopack hooks or unexpected error)
            if (_mgr == null) return new UpdateResult(Success: false, IsDevMode: true);

            var newVersion = await _mgr.CheckForUpdatesAsync();
            if (newVersion == null)
            {
                return new UpdateResult(Success: false); // No updates
            }

            string targetVersion = newVersion.TargetFullRelease.Version.ToString();
            // We can also make release notes URL dynamic if we want, but sticking to standard pattern:
            string releaseNotesUrl = $"https://ims-lilac-beta.vercel.app/releases/{targetVersion}";

            return new UpdateResult(Success: true, Version: targetVersion, ReleaseNotesUrl: releaseNotesUrl);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("not installed", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Update check skipped (App is not installed/packaged).");
                return new UpdateResult(Success: false, IsDevMode: true);
            }
            else
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
            }
            return new UpdateResult(Success: false);
        }
    }

    public async Task DownloadAndRestartAsync()
    {
        if (!_isInitialized) await InitializeAsync();
        
        try
        {
            if (_mgr == null) return;

            var newVersion = await _mgr.CheckForUpdatesAsync();
            if (newVersion != null)
            {
                await _mgr.DownloadUpdatesAsync(newVersion);
                _mgr.ApplyUpdatesAndRestart(newVersion);
            }
        }
        catch (Exception ex)
        {
             Console.WriteLine($"Error applying updates: {ex.Message}");
        }
    }
}
