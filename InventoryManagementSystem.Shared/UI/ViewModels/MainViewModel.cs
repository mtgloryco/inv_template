using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Services;
using System.Threading.Tasks;
using System;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly InventoryService _inventoryService;
    private readonly UserService _userService;
    private readonly LicenseService _licenseService;
    private readonly HardwareIdService _hardwareIdService;
    private readonly AnalyticsService _analyticsService;
    private readonly ReceiptService _receiptService;
    private readonly UpdateService _updateService;
    private readonly SettingsService _settingsService;
    private readonly SupplierService _supplierService;
    private readonly PurchaseOrderService _purchaseOrderService;
    private readonly ForecastingService _forecastingService;
    private readonly ExpiryService _expiryService;
    private readonly LocationService _locationService;
    private readonly ReturnsService _returnsService;
    private readonly AdvancedAnalyticsService _advancedAnalyticsService;
    private readonly BundleService _bundleService;
    private readonly AuditService _auditService;
    private readonly ReportingService _reportingService;
    private readonly CloudSyncService _cloudSyncService;
    private readonly DailyBriefingService _dailyBriefingService;

    // Navigation Stack
    private readonly System.Collections.Generic.Stack<ViewModelBase> _navigationStack = new();

    [ObservableProperty]
    private bool _canGoBack;
    
    public LanguageService Language { get; } 

    [ObservableProperty]
    private ViewModelBase _currentPage = default!;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _currentUserName = "";

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private Avalonia.Controls.GridLength _sidebarGridLength = new(0);

    // Update Properties
    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _updateStatusText = "Check Updates"; // Button Label

    [ObservableProperty]
    private string _updateVersion = "";

    [ObservableProperty]
    private string _releaseNotesUrl = "";

    public MainViewModel(
        InventoryService inventoryService,
        UserService userService,
        LicenseService licenseService,
        HardwareIdService hardwareIdService,
        AnalyticsService analyticsService,
        ReceiptService receiptService,
        LanguageService languageService,
        UpdateService updateService,
        SettingsService settingsService,
        SupplierService supplierService,
        PurchaseOrderService purchaseOrderService,
        ForecastingService forecastingService,
        ExpiryService expiryService,
        LocationService locationService,
        ReturnsService returnsService,
        AdvancedAnalyticsService advancedAnalyticsService,
        BundleService bundleService,
        AuditService auditService,
        ReportingService reportingService,
        CloudSyncService cloudSyncService,
        DailyBriefingService dailyBriefingService)
    {
        _inventoryService = inventoryService;
        _userService = userService;
        _licenseService = licenseService;
        _hardwareIdService = hardwareIdService;
        _analyticsService = analyticsService;
        _receiptService = receiptService;
        Language = languageService;
        _updateService = updateService;
        _settingsService = settingsService;
        _supplierService = supplierService;
        _purchaseOrderService = purchaseOrderService;
        _forecastingService = forecastingService;
        _expiryService = expiryService;
        _locationService = locationService;
        _returnsService = returnsService;
        _advancedAnalyticsService = advancedAnalyticsService;
        _bundleService = bundleService;
        _auditService = auditService;
        _reportingService = reportingService;
        _cloudSyncService = cloudSyncService;
        _dailyBriefingService = dailyBriefingService;

        // Check for updates on startup (fire and forget, silent)
        _ = CheckForUpdatesInternal(false);

        // 1. Strict License Check: Lock app if status is not Active/Valid
        var status = _licenseService.CurrentLicense.Status;
        if (status != "Active" && status != "Valid")
        {
            GoToLicense();
            IsLoggedIn = false;
            SidebarGridLength = new Avalonia.Controls.GridLength(0);
            return;
        }

        // 2. If License is valid, AUTO-LOGIN as Admin (Bypass Login Screen)
        if (UserSession.CurrentUser == null)
        {
            // Create a default admin session if none exists
            var defaultUser = new Domain.User { Username = "Admin", Role = "Admin" };
            UserSession.Login(defaultUser);
        }

        OnLoginSuccess();
    }

    private void OnLoginSuccess()
    {
        // Safety re-check
        if (_licenseService.CurrentLicense.Status != "Valid" && _licenseService.CurrentLicense.Status != "Active")
        {
            GoToLicense();
            return;
        }

        IsLoggedIn = true;
        SidebarGridLength = new Avalonia.Controls.GridLength(250);
        CurrentUserName = UserSession.CurrentUser?.Username ?? "Unknown";
        IsAdmin = UserSession.IsAdmin;
        
        // Start fresh on Dashboard, clearing any login/license history
        CurrentPage = new DashboardViewModel(_inventoryService, _licenseService, Language, _settingsService, _dailyBriefingService, GoToInventory, GoToReports, GoToPOS);
        _navigationStack.Clear();
        CanGoBack = false;
        OnPropertyChanged(nameof(SidebarGridLength));
    }

    [RelayCommand]
    public void GoBack()
    {
        if (_navigationStack.Count > 0)
        {
            var previousPage = _navigationStack.Pop();
            CurrentPage = previousPage; // Directly set backing field to avoid pushing to stack again
            // OnPropertyChanged(nameof(CurrentPage)); // Not needed if setting property

            
            // Update UI
            if (previousPage is DashboardViewModel)
            {
               SidebarGridLength = new Avalonia.Controls.GridLength(250);
            }
            // Add other checks if needed for sidebar visibility, though mostly Dashboard controls it
             
            CanGoBack = _navigationStack.Count > 0;
        }
    }

    private void NavigateTo(ViewModelBase newPage)
    {
        if (CurrentPage != null)
        {
            _navigationStack.Push(CurrentPage);
        }
        CurrentPage = newPage;
        CanGoBack = _navigationStack.Count > 0;
    }

    [RelayCommand]
    public void Logout()
    {
        _navigationStack.Clear(); // Clear history on logout
        CanGoBack = false;

        // "Exit" Application
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand]
    public void ChangeLanguage(string code)
    {
        Language.SetLanguage(code);
    }
    
    [RelayCommand]
    public void GoToDashboard()
    {
        NavigateTo(new DashboardViewModel(_inventoryService, _licenseService, Language, _settingsService, _dailyBriefingService, GoToInventory, GoToReports, GoToPOS));
        SidebarGridLength = new Avalonia.Controls.GridLength(250);
    }

    [RelayCommand]
    public void GoToInventory() => NavigateTo(new InventoryViewModel(_inventoryService, _licenseService, _settingsService, Language));

    [RelayCommand]
    public void GoToReports()
    {
        if (!_licenseService.CanAccessAdvancedReports())
        {
            // Redirect to License/Upgrade page if not premium
            GoToLicense(); 
            return;
        }
        NavigateTo(new ReportsViewModel(_inventoryService, _licenseService, _settingsService, Language));
    }

    [RelayCommand]
    public void GoToPOS()
    {
        if (!_licenseService.CanAccessPOS())
        {
            // Redirect to License/Upgrade page if not premium
            GoToLicense();
            return;
        }
        NavigateTo(new POSViewModel(_inventoryService, _licenseService, _receiptService, _settingsService, Language));
    }

    [RelayCommand]
    public void GoToAnalytics()
    {
         if (!_licenseService.CanAccessAnalytics())
        {
             // Analytics is a premium feature
            GoToLicense();
            return;
        }
        NavigateTo(new AnalyticsViewModel(_analyticsService, Language));
    }

    [RelayCommand]
    public void GoToUsers()
    {
        if (IsAdmin)
        {
            NavigateTo(new UsersViewModel(_userService));
        }
    }

    [RelayCommand]
    public void GoToSettings()
    {
        NavigateTo(new SettingsViewModel(_settingsService, Language));
    }

    [RelayCommand]
    public void GoToSuppliers()
    {
        if (!_licenseService.CanAccessSupplierManagement())
        {
            GoToLicense();
            return;
        }

        NavigateTo(new SuppliersViewModel(_supplierService));
    }

    [RelayCommand]
    public void GoToPurchaseOrders()
    {
        if (!_licenseService.CanAccessPurchaseOrders())
        {
            GoToLicense();
            return;
        }

        NavigateTo(new PurchaseOrdersViewModel(_purchaseOrderService));
    }

    [RelayCommand]
    public void GoToForecasting()
    {
        if (!_licenseService.CanAccessForecasting())
        {
            GoToLicense();
            return;
        }

        NavigateTo(new ForecastingViewModel(_forecastingService));
    }

    [RelayCommand]
    public void GoToReorderDashboard()
    {
        if (!_licenseService.CanAccessForecasting())
        {
            GoToLicense();
            return;
        }

        NavigateTo(new ReorderDashboardViewModel(_forecastingService, _purchaseOrderService));
    }

    [RelayCommand]
    public void GoToExpiryDashboard()
    {
        if (!_licenseService.CanAccessExpiryTracking())
        {
            GoToLicense();
            return;
        }

        NavigateTo(new ExpiryDashboardViewModel(_expiryService));
    }

    [RelayCommand]
    public void GoToLocations()
    {
        if (!_licenseService.CanAccessMultiLocation())
        {
            GoToLicense();
            return;
        }
        NavigateTo(new LocationsViewModel(_locationService));
    }

    [RelayCommand]
    public void GoToStockTransfer()
    {
        if (!_licenseService.CanAccessMultiLocation())
        {
            GoToLicense();
            return;
        }
        NavigateTo(new StockTransferViewModel(_locationService, _inventoryService));
    }

    [RelayCommand]
    public void GoToReturns()
    {
        NavigateTo(new ReturnsViewModel(_returnsService, _inventoryService));
    }

    [RelayCommand]
    public void GoToAdvancedAnalytics()
    {
        if (!_licenseService.CanAccessAdvancedAnalytics())
        {
            GoToLicense();
            return;
        }
        NavigateTo(new AdvancedAnalyticsViewModel(_advancedAnalyticsService));
    }

    [RelayCommand]
    public void GoToBundles()
    {
        if (!_licenseService.CanAccessKitting())
        {
            GoToLicense();
            return;
        }
        NavigateTo(new BundleViewModel(_bundleService, _inventoryService));
    }

    [RelayCommand]
    public void GoToAuditTrail()
    {
        if (!_licenseService.CanAccessAuditTrail())
        {
            GoToLicense();
            return;
        }
        NavigateTo(new AuditTrailViewModel(_auditService));
    }

    private void OnActivationSuccess()
    {
        // After activation, Bypass Login -> Auto-Login and Go to Dashboard
        if (UserSession.CurrentUser == null)
        {
            var defaultUser = new Domain.User { Username = "Admin", Role = "Admin" };
            UserSession.Login(defaultUser);
        }
        OnLoginSuccess();
    }

    [RelayCommand]
    public void GoToLicense() => NavigateTo(new LicenseViewModel(_licenseService, _hardwareIdService, OnActivationSuccess));

    [RelayCommand]
    public Task CheckForUpdates() => CheckForUpdatesInternal(true);

    private async Task CheckForUpdatesInternal(bool userInitiated)
    {
        if (UpdateStatusText == "Checking...") return; // Prevent spam

        if (userInitiated) UpdateStatusText = "Checking...";
        
        var result = await _updateService.CheckForUpdatesAsync();
        
        if (result.Success && result.Version != null)
        {
            UpdateVersion = result.Version;
            ReleaseNotesUrl = result.ReleaseNotesUrl ?? "";
            IsUpdateAvailable = true;
            UpdateStatusText = "Update Found!";
        }
        else
        {
            IsUpdateAvailable = false;
            if (userInitiated)
            {
                if (result.IsDevMode)
                {
                     UpdateStatusText = "Dev Mode (Skipped)";
                }
                else
                {
                    UpdateStatusText = "Up to Date";
                }
                
                // Reset text after 3 seconds
                await Task.Delay(3000);
                UpdateStatusText = "Check Updates";
            }
        }
    }

    [RelayCommand]
    public void OpenReleaseNotes()
    {
        if (!string.IsNullOrEmpty(ReleaseNotesUrl))
        {
            try
            {
               // Cross-platform open url
               System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
               {
                   FileName = ReleaseNotesUrl,
                   UseShellExecute = true
               });
            }
            catch { /* handle error */ }
        }
    }

    [RelayCommand]
    public async Task InstallUpdate()
    {
        await _updateService.DownloadAndRestartAsync();
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        var app = Avalonia.Application.Current;
        if (app is not null)
        {
            var currentTheme = app.RequestedThemeVariant;
            app.RequestedThemeVariant = currentTheme == Avalonia.Styling.ThemeVariant.Dark 
                ? Avalonia.Styling.ThemeVariant.Light 
                : Avalonia.Styling.ThemeVariant.Dark;
        }
    }
}
