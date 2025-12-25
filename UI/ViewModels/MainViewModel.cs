using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly InventoryService _inventoryService;
    private readonly UserService _userService;
    private readonly LicenseService _licenseService;
    private readonly HardwareIdService _hardwareIdService;
    private readonly AnalyticsService _analyticsService;

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

    public MainViewModel(InventoryService inventoryService, UserService userService, LicenseService licenseService, HardwareIdService hardwareIdService, AnalyticsService analyticsService)
    {
        _inventoryService = inventoryService;
        _userService = userService;
        _licenseService = licenseService;
        _hardwareIdService = hardwareIdService;
        _analyticsService = analyticsService;

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
        GoToDashboard();
    }

    [RelayCommand]
    public void Logout()
    {
        // "Exit" Application
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand]
    public void GoToDashboard() => CurrentPage = new DashboardViewModel(_inventoryService, _licenseService, GoToInventory, GoToReports, GoToPOS);

    [RelayCommand]
    public void GoToInventory() => CurrentPage = new InventoryViewModel(_inventoryService, _licenseService);

    [RelayCommand]
    public void GoToReports()
    {
        if (!_licenseService.IsPremiumActive())
        {
            // Redirect to License/Upgrade page if not premium
            GoToLicense(); 
            return;
        }
        CurrentPage = new ReportsViewModel(_inventoryService, _licenseService);
    }

    [RelayCommand]
    public void GoToPOS()
    {
        if (!_licenseService.IsPremiumActive())
        {
            // Redirect to License/Upgrade page if not premium
            GoToLicense();
            return;
        }
        CurrentPage = new POSViewModel(_inventoryService, _licenseService);
    }

    [RelayCommand]
    public void GoToAnalytics()
    {
         if (!_licenseService.IsPremiumActive())
        {
             // Analytics is a premium feature
            GoToLicense();
            return;
        }
        CurrentPage = new AnalyticsViewModel(_analyticsService);
    }

    [RelayCommand]
    public void GoToUsers()
    {
        if (IsAdmin)
        {
            CurrentPage = new UsersViewModel(_userService);
        }
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
    public void GoToLicense() => CurrentPage = new LicenseViewModel(_licenseService, _hardwareIdService, OnActivationSuccess);
}
