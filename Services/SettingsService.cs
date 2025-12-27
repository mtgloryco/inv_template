using System;
using System.IO;
using System.Text.Json;

namespace InventoryManagementSystem.Services
{
    public class AppSettings
    {
        public string StoreName { get; set; } = "My Store";
        public string StoreAddress { get; set; } = "Kigali, Rwanda";
        public string CurrencySymbol { get; set; } = "RWF";
        public decimal DefaultTaxRate { get; set; } = 0.18m;
        public string PrinterName { get; set; } = "";
        public bool IsDarkTheme { get; set; } = true;
    }

    public class SettingsService
    {
        private readonly string _settingsFilePath;
        public AppSettings CurrentSettings { get; private set; } = new AppSettings();

        public SettingsService()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InventoryManagementSystem");
            Directory.CreateDirectory(folder);
            _settingsFilePath = Path.Combine(folder, "settings.json");
            LoadSettings();
        }

        public void LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    CurrentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    CurrentSettings = new AppSettings();
                }
            }
            else
            {
                CurrentSettings = new AppSettings();
                SaveSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(CurrentSettings, options);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
