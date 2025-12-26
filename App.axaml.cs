using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using InventoryManagementSystem.Infrastructure;
using InventoryManagementSystem.Services;
using InventoryManagementSystem.UI.ViewModels;
using InventoryManagementSystem.UI.Views;

namespace InventoryManagementSystem;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Initialize Database
            // Initialize Database
            var dbService = new DatabaseService();
            var userService = new UserService(dbService);
            var hardwareService = new HardwareIdService();
            var cryptoService = new LicenseCryptoService();
            var licenseService = new LicenseService(dbService, hardwareService, cryptoService);
            var inventoryService = new InventoryService(dbService, licenseService);
            var analyticsService = new AnalyticsService(dbService);
            var receiptService = new ReceiptService();
            var languageService = new LanguageService();
            var updateService = new UpdateService();

            // Initialize services on a background thread to prevent UI thread deadlock
            Task.Run(async () =>
            {
                await dbService.InitializeAsync();
                await userService.InitializeAsync();
                await licenseService.InitializeAsync();

                // Auto-login Guest ONLY if license is valid
                if (licenseService.CurrentLicense.Status == "Active" || licenseService.CurrentLicense.Status == "Valid")
                {
                    var guest = await userService.AuthenticateAsync("guest", "");
                    if (guest != null) { UserSession.Login(guest); }
                }
            }).Wait();

            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(inventoryService, userService, licenseService, hardwareService, analyticsService, receiptService, languageService, updateService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}