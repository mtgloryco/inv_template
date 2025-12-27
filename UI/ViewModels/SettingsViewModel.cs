using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private string _storeName;

    [ObservableProperty]
    private string _storeAddress;

    [ObservableProperty]
    private string _currencySymbol;

    [ObservableProperty]
    private string _printerName;

    [ObservableProperty]
    private string _statusMessage;

    public LanguageService Language { get; }

    public SettingsViewModel(SettingsService settingsService, LanguageService languageService)
    {
        _settingsService = settingsService;
        Language = languageService;
        var s = _settingsService.CurrentSettings;

        _storeName = s.StoreName;
        _storeAddress = s.StoreAddress;
        _currencySymbol = s.CurrencySymbol;
        _printerName = s.PrinterName;
        _statusMessage = "";
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            var s = _settingsService.CurrentSettings;
            s.StoreName = StoreName;
            s.StoreAddress = StoreAddress;
            s.CurrencySymbol = CurrencySymbol;
            s.PrinterName = PrinterName;

            _settingsService.SaveSettings();
            StatusMessage = "Settings saved successfully!";
        }
        catch
        {
            StatusMessage = "Failed to save settings.";
        }
    }
}
