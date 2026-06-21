using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly TaxService _taxService;

    // Tabs
    [ObservableProperty]
    private string _selectedTab = "General"; // "General" or "Taxes"

    public bool IsGeneralTabActive => SelectedTab == "General";
    public bool IsTaxesTabActive => SelectedTab == "Taxes";

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsGeneralTabActive));
        OnPropertyChanged(nameof(IsTaxesTabActive));
    }

    // General Settings Properties
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

    // Taxes Properties
    [ObservableProperty]
    private ObservableCollection<Tax> _taxes = new();

    [ObservableProperty]
    private Tax? _selectedTax;

    [ObservableProperty]
    private bool _isTaxFormVisible;

    // Tax Form Properties
    [ObservableProperty]
    private int _taxId;

    [ObservableProperty]
    private string _taxName = string.Empty;

    [ObservableProperty]
    private string _taxComputation = "Percentage"; // Percentage, Fixed

    [ObservableProperty]
    private decimal _taxAmount;

    [ObservableProperty]
    private string _taxType = "Sales"; // Sales, Purchases

    [ObservableProperty]
    private string _taxDescription = string.Empty;

    [ObservableProperty]
    private string _taxLabelOnInvoice = string.Empty;

    [ObservableProperty]
    private string _taxScope = "Goods"; // Goods, Services

    [ObservableProperty]
    private string _includedInPrice = "Exclude"; // Include, Exclude

    [ObservableProperty]
    private string _taxErrorMessage = string.Empty;

    [ObservableProperty]
    private bool _taxIsActive = true;

    public ObservableCollection<string> ComputationOptions { get; } = new() { "Percentage", "Fixed" };
    public ObservableCollection<string> TaxTypeOptions { get; } = new() { "Sales", "Purchases" };
    public ObservableCollection<string> ScopeOptions { get; } = new() { "Goods", "Services" };
    public ObservableCollection<string> IncludedInPriceOptions { get; } = new() { "Include", "Exclude" };

    public LanguageService Language { get; }

    public SettingsViewModel(SettingsService settingsService, LanguageService languageService, TaxService taxService)
    {
        _settingsService = settingsService;
        Language = languageService;
        _taxService = taxService;

        var s = _settingsService.CurrentSettings;
        _storeName = s.StoreName;
        _storeAddress = s.StoreAddress;
        _currencySymbol = s.CurrencySymbol;
        _printerName = s.PrinterName;
        _statusMessage = "";

        // Load taxes
        _ = LoadTaxesAsync();
    }

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        SelectedTab = tabName;
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

    [RelayCommand]
    public async Task LoadTaxesAsync()
    {
        try
        {
            var taxList = await _taxService.GetAllTaxesAsync();
            Taxes.Clear();
            foreach (var tax in taxList)
            {
                Taxes.Add(tax);
            }
        }
        catch (Exception ex)
        {
            TaxErrorMessage = $"Failed to load taxes: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenAddTaxForm()
    {
        TaxId = 0;
        TaxName = string.Empty;
        TaxComputation = "Percentage";
        TaxAmount = 0;
        TaxType = "Sales";
        TaxDescription = string.Empty;
        TaxLabelOnInvoice = string.Empty;
        TaxScope = "Goods";
        IncludedInPrice = "Exclude";
        TaxErrorMessage = string.Empty;
        TaxIsActive = true;
        IsTaxFormVisible = true;
    }

    [RelayCommand]
    private void OpenEditTaxForm(Tax tax)
    {
        if (tax == null) return;
        TaxId = tax.Id;
        TaxName = tax.Name;
        TaxComputation = tax.Computation;
        TaxAmount = tax.Amount;
        TaxType = tax.TaxType;
        TaxDescription = tax.Description;
        TaxLabelOnInvoice = tax.LabelOnInvoice;
        TaxScope = tax.Scope;
        IncludedInPrice = tax.IncludedInPrice;
        TaxErrorMessage = string.Empty;
        TaxIsActive = tax.IsActive;
        IsTaxFormVisible = true;
    }

    [RelayCommand]
    private void CloseTaxForm()
    {
        IsTaxFormVisible = false;
    }

    [RelayCommand]
    private async Task SaveTaxAsync()
    {
        if (string.IsNullOrWhiteSpace(TaxName))
        {
            TaxErrorMessage = "Tax name is required.";
            return;
        }

        if (TaxAmount < 0)
        {
            TaxErrorMessage = "Amount cannot be negative.";
            return;
        }

        try
        {
            var tax = new Tax
            {
                Id = TaxId,
                Name = TaxName,
                Computation = TaxComputation,
                Amount = TaxAmount,
                TaxType = TaxType,
                Description = TaxDescription,
                LabelOnInvoice = string.IsNullOrWhiteSpace(TaxLabelOnInvoice) ? TaxName : TaxLabelOnInvoice,
                Scope = TaxScope,
                IncludedInPrice = IncludedInPrice,
                IsActive = TaxIsActive
            };

            if (TaxId == 0)
            {
                await _taxService.AddTaxAsync(tax);
            }
            else
            {
                await _taxService.UpdateTaxAsync(tax);
            }

            IsTaxFormVisible = false;
            await LoadTaxesAsync();
        }
        catch (Exception ex)
        {
            TaxErrorMessage = $"Error saving tax: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteTaxAsync(Tax tax)
    {
        if (tax == null) return;
        try
        {
            await _taxService.DeleteTaxAsync(tax.Id);
            await LoadTaxesAsync();
        }
        catch (Exception ex)
        {
            TaxErrorMessage = $"Error deleting tax: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleTaxStatusAsync(Tax tax)
    {
        if (tax == null) return;
        try
        {
            tax.IsActive = !tax.IsActive;
            await _taxService.UpdateTaxAsync(tax);
            await LoadTaxesAsync();
        }
        catch (Exception ex)
        {
            TaxErrorMessage = $"Error toggling tax status: {ex.Message}";
        }
    }
}
