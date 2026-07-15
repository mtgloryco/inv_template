using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    private readonly AccountService _accountService;
    private readonly JournalService _journalService;
    private readonly AccountingReportService _accountingReportService;
    private readonly PaymentService _paymentService;
    private readonly CustomFieldService _customFieldService;
    private readonly CurrencyService _currencyService;
    private readonly BudgetReportService _budgetReportService;
    private readonly Action? _onRequestShowWizard;
    private readonly Action? _onModulesChanged;

    // Tabs
    [ObservableProperty]
    private string _selectedTab = "General"; // "General", "Taxes", "Accounting", "Payments", "Financial", "BusinessSetup", "CustomFields", "Terminology", "Modules"

    public bool IsGeneralTabActive => SelectedTab == "General";
    public bool IsTaxesTabActive => SelectedTab == "Taxes";
    public bool IsAccountingTabActive => SelectedTab == "Accounting";
    public bool IsPaymentsTabActive => SelectedTab == "Payments";
    public bool IsFinancialTabActive => SelectedTab == "Financial";
    public bool IsBusinessSetupTabActive => SelectedTab == "BusinessSetup";
    public bool IsCustomFieldsTabActive => SelectedTab == "CustomFields";
    public bool IsTerminologyTabActive => SelectedTab == "Terminology";
    public bool IsModulesTabActive => SelectedTab == "Modules";

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsGeneralTabActive));
        OnPropertyChanged(nameof(IsTaxesTabActive));
        OnPropertyChanged(nameof(IsAccountingTabActive));
        OnPropertyChanged(nameof(IsPaymentsTabActive));
        OnPropertyChanged(nameof(IsFinancialTabActive));
        OnPropertyChanged(nameof(IsBusinessSetupTabActive));
        OnPropertyChanged(nameof(IsCustomFieldsTabActive));
        OnPropertyChanged(nameof(IsTerminologyTabActive));
        OnPropertyChanged(nameof(IsModulesTabActive));

        if (value == "Accounting" || value == "Taxes")
        {
            _ = LoadAccountsAsync();
        }
        if (value == "Accounting")
        {
            _ = LoadAvailableTaxesAsync();
        }
        if (value == "Payments")
        {
            _ = LoadPaymentsDataAsync();
        }
        if (value == "Financial")
        {
            _ = LoadFinancialDataAsync();
        }
        if (value == "BusinessSetup")
        {
            OnPropertyChanged(nameof(CurrentBusinessTypeDisplay));
            OnPropertyChanged(nameof(IsBusinessSetupCompleted));
        }
        if (value == "CustomFields")
        {
            _ = LoadCustomFieldDefinitionsAsync();
        }
        if (value == "Terminology")
        {
            LoadTerminologyTerms();
        }
        if (value == "Modules")
        {
            LoadModuleToggles();
        }
    }

    // Chart of Accounts Properties
    private List<Account> _allAccounts = new();

    [ObservableProperty]
    private ObservableCollection<Account> _filteredAccounts = new();

    [ObservableProperty]
    private string _accountSearchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedAccountGroup = "All"; // "All", "1", "2", "3", "4", "5"

    [ObservableProperty]
    private string _selectedGroupText = "All Accounts";

    public ObservableCollection<string> Groups { get; } = new()
    {
        "All Accounts",
        "Group 1: Assets",
        "Group 2: Liabilities",
        "Group 3: Equity",
        "Group 4: Income",
        "Group 5: Expenses"
    };

    partial void OnSelectedGroupTextChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (value.Contains("1")) SelectedAccountGroup = "1";
        else if (value.Contains("2")) SelectedAccountGroup = "2";
        else if (value.Contains("3")) SelectedAccountGroup = "3";
        else if (value.Contains("4")) SelectedAccountGroup = "4";
        else if (value.Contains("5")) SelectedAccountGroup = "5";
        else SelectedAccountGroup = "All";
    }

    [ObservableProperty]
    private bool _isAccountFormVisible;

    partial void OnIsAccountFormVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsChartOfAccountsVisible));
        OnPropertyChanged(nameof(IsCreateAccountButtonVisible));
    }

    [ObservableProperty]
    private int _accountId;

    [ObservableProperty]
    private string _newAccountCode = string.Empty;

    [ObservableProperty]
    private string _newAccountName = string.Empty;

    [ObservableProperty]
    private string _newAccountType = string.Empty;

    [ObservableProperty]
    private Tax? _newAccountSelectedTax;

    [ObservableProperty]
    private string _newAccountCurrency = "RWF";

    [ObservableProperty]
    private bool _newAccountIsActive = true;

    [ObservableProperty]
    private string _newAccountDescription = string.Empty;

    [ObservableProperty]
    private bool _newAccountPaymentReconciliation = false;

    [ObservableProperty]
    private string _accountErrorMessage = string.Empty;

    [ObservableProperty]
    private string _selectedAccountingSubTab = "Chart of Accounts";

    public ObservableCollection<string> AccountingSubTabs { get; } = new()
    {
        "Chart of Accounts",
        "Journals",
        "Accounting Reports"
    };

    // Sub-tab active flags
    public bool IsChartOfAccountsSubTabActive => SelectedAccountingSubTab == "Chart of Accounts";
    public bool IsJournalsSubTabActive => SelectedAccountingSubTab == "Journals";
    public bool IsAccountingReportsSubTabActive => SelectedAccountingSubTab == "Accounting Reports";

    // Computed visibility flags for the content panels
    public bool IsChartOfAccountsVisible => IsChartOfAccountsSubTabActive && !IsAccountFormVisible;
    public bool IsCreateAccountButtonVisible => IsChartOfAccountsSubTabActive && !IsAccountFormVisible;
    public bool IsJournalsVisible => IsJournalsSubTabActive && !IsJournalFormVisible;
    public bool IsCreateJournalButtonVisible => IsJournalsSubTabActive && !IsJournalFormVisible;
    public bool IsAccountingReportsVisible => IsAccountingReportsSubTabActive && !IsReportDetailVisible;

    partial void OnSelectedAccountingSubTabChanged(string value)
    {
        // Close any open forms when switching sub-tabs
        IsAccountFormVisible = false;
        IsJournalFormVisible = false;
        IsReportDetailVisible = false;
        IsLineFormVisible = false;

        OnPropertyChanged(nameof(IsChartOfAccountsSubTabActive));
        OnPropertyChanged(nameof(IsJournalsSubTabActive));
        OnPropertyChanged(nameof(IsAccountingReportsSubTabActive));
        OnPropertyChanged(nameof(IsChartOfAccountsVisible));
        OnPropertyChanged(nameof(IsCreateAccountButtonVisible));
        OnPropertyChanged(nameof(IsJournalsVisible));
        OnPropertyChanged(nameof(IsCreateJournalButtonVisible));
        OnPropertyChanged(nameof(IsAccountingReportsVisible));

        if (value == "Journals")
            _ = LoadJournalsAsync();
        else if (value == "Accounting Reports")
            _ = LoadReportsAsync();
        else
        {
            _ = LoadAccountsAsync();
            _ = LoadAvailableTaxesAsync();
        }
    }

    public ObservableCollection<string> AccountTypeOptions { get; } = new()
    {
        "Asset: Receivable",
        "Asset: Bank and Cash",
        "Asset: Current Asset",
        "Asset: Fixed Asset",
        "Asset: Non-current Asset",
        "Asset: Pre Payments",
        "Liability: Payable",
        "Liability: Credit Card",
        "Liability: Current Liability",
        "Liability: Non-current Liability",
        "Equity: Equity",
        "Equity: Current Year Earnings",
        "Income: Income",
        "Income: Other Incomes",
        "Expense: Expenses",
        "Expense: Other Expenses",
        "Expense: Cost of Revenue"
    };

    public ObservableCollection<string> CurrencyOptions { get; } = new() { "RWF", "USD", "EUR", "KES", "UGX" };

    [ObservableProperty]
    private ObservableCollection<Tax> _availableTaxes = new();

    // ── Journals ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<JournalListItem> _journals = new();

    [ObservableProperty]
    private bool _isJournalFormVisible;

    partial void OnIsJournalFormVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsJournalsVisible));
        OnPropertyChanged(nameof(IsCreateJournalButtonVisible));
    }

    [ObservableProperty]
    private int _journalId;

    [ObservableProperty]
    private string _journalName = string.Empty;

    [ObservableProperty]
    private string _journalType = "Miscellaneous";

    public bool IsJournalTypeBank => JournalType == "Bank";

    partial void OnJournalTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsJournalTypeBank));
    }

    [ObservableProperty]
    private string _journalSequencePrefix = string.Empty;

    [ObservableProperty]
    private Account? _journalDefaultAccount;

    // Account search-as-you-type for the journal form
    [ObservableProperty]
    private string _journalAccountSearch = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Account> _journalAccountResults = new();

    [ObservableProperty]
    private bool _isJournalAccountResultsVisible;

    // Bank Journal Clearing Accounts
    [ObservableProperty]
    private Account? _journalBankAccount;
    [ObservableProperty]
    private string _journalBankAccountSearch = string.Empty;
    [ObservableProperty]
    private ObservableCollection<Account> _journalBankAccountResults = new();
    [ObservableProperty]
    private bool _isJournalBankAccountResultsVisible;

    [ObservableProperty]
    private Account? _journalSuspenseAccount;
    [ObservableProperty]
    private string _journalSuspenseAccountSearch = string.Empty;
    [ObservableProperty]
    private ObservableCollection<Account> _journalSuspenseAccountResults = new();
    [ObservableProperty]
    private bool _isJournalSuspenseAccountResultsVisible;

    [ObservableProperty]
    private Account? _journalProfitAccount;
    [ObservableProperty]
    private string _journalProfitAccountSearch = string.Empty;
    [ObservableProperty]
    private ObservableCollection<Account> _journalProfitAccountResults = new();
    [ObservableProperty]
    private bool _isJournalProfitAccountResultsVisible;

    [ObservableProperty]
    private Account? _journalLossAccount;
    [ObservableProperty]
    private string _journalLossAccountSearch = string.Empty;
    [ObservableProperty]
    private ObservableCollection<Account> _journalLossAccountResults = new();
    [ObservableProperty]
    private bool _isJournalLossAccountResultsVisible;

    [ObservableProperty]
    private Account? _journalOutstandingReceiptsAccount;
    [ObservableProperty]
    private string _journalOutstandingReceiptsAccountSearch = string.Empty;
    [ObservableProperty]
    private ObservableCollection<Account> _journalOutstandingReceiptsAccountResults = new();
    [ObservableProperty]
    private bool _isJournalOutstandingReceiptsAccountResultsVisible;

    [ObservableProperty]
    private Account? _journalOutstandingPaymentsAccount;
    [ObservableProperty]
    private string _journalOutstandingPaymentsAccountSearch = string.Empty;
    [ObservableProperty]
    private ObservableCollection<Account> _journalOutstandingPaymentsAccountResults = new();
    [ObservableProperty]
    private bool _isJournalOutstandingPaymentsAccountResultsVisible;

    [ObservableProperty]
    private BankAccountDisplayRow? _journalSelectedLinkedBankAccount;

    partial void OnJournalAccountSearchChanged(string value)
    {
        JournalAccountResults.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            IsJournalAccountResultsVisible = false;
            return;
        }

        var lower = value.ToLower();
        foreach (var acc in _allAccounts)
        {
            if ((acc.Code?.ToLower().Contains(lower) == true) ||
                (acc.Name?.ToLower().Contains(lower) == true))
            {
                JournalAccountResults.Add(acc);
                if (JournalAccountResults.Count >= 10) break;
            }
        }
        IsJournalAccountResultsVisible = JournalAccountResults.Count > 0;
    }

    [RelayCommand]
    private void SelectJournalAccount(Account? account)
    {
        JournalDefaultAccount = account;
        JournalAccountSearch = account != null ? $"{account.Code} {account.Name}" : string.Empty;
        JournalAccountResults.Clear();
        IsJournalAccountResultsVisible = false;
    }

    partial void OnJournalBankAccountSearchChanged(string value)
    {
        JournalBankAccountResults.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            IsJournalBankAccountResultsVisible = false;
            return;
        }

        var lower = value.ToLower();
        foreach (var acc in _allAccounts)
        {
            if ((acc.Code?.ToLower().Contains(lower) == true) ||
                (acc.Name?.ToLower().Contains(lower) == true))
            {
                JournalBankAccountResults.Add(acc);
                if (JournalBankAccountResults.Count >= 10) break;
            }
        }
        IsJournalBankAccountResultsVisible = JournalBankAccountResults.Count > 0;
    }

    [RelayCommand]
    private void SelectJournalBankAccount(Account? account)
    {
        JournalBankAccount = account;
        JournalBankAccountSearch = account != null ? $"{account.Code} {account.Name}" : string.Empty;
        JournalBankAccountResults.Clear();
        IsJournalBankAccountResultsVisible = false;
    }

    partial void OnJournalSuspenseAccountSearchChanged(string value)
    {
        JournalSuspenseAccountResults.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            IsJournalSuspenseAccountResultsVisible = false;
            return;
        }

        var lower = value.ToLower();
        foreach (var acc in _allAccounts)
        {
            if ((acc.Code?.ToLower().Contains(lower) == true) ||
                (acc.Name?.ToLower().Contains(lower) == true))
            {
                JournalSuspenseAccountResults.Add(acc);
                if (JournalSuspenseAccountResults.Count >= 10) break;
            }
        }
        IsJournalSuspenseAccountResultsVisible = JournalSuspenseAccountResults.Count > 0;
    }

    [RelayCommand]
    private void SelectJournalSuspenseAccount(Account? account)
    {
        JournalSuspenseAccount = account;
        JournalSuspenseAccountSearch = account != null ? $"{account.Code} {account.Name}" : string.Empty;
        JournalSuspenseAccountResults.Clear();
        IsJournalSuspenseAccountResultsVisible = false;
    }

    partial void OnJournalProfitAccountSearchChanged(string value)
    {
        JournalProfitAccountResults.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            IsJournalProfitAccountResultsVisible = false;
            return;
        }

        var lower = value.ToLower();
        foreach (var acc in _allAccounts)
        {
            if ((acc.Code?.ToLower().Contains(lower) == true) ||
                (acc.Name?.ToLower().Contains(lower) == true))
            {
                JournalProfitAccountResults.Add(acc);
                if (JournalProfitAccountResults.Count >= 10) break;
            }
        }
        IsJournalProfitAccountResultsVisible = JournalProfitAccountResults.Count > 0;
    }

    [RelayCommand]
    private void SelectJournalProfitAccount(Account? account)
    {
        JournalProfitAccount = account;
        JournalProfitAccountSearch = account != null ? $"{account.Code} {account.Name}" : string.Empty;
        JournalProfitAccountResults.Clear();
        IsJournalProfitAccountResultsVisible = false;
    }

    partial void OnJournalLossAccountSearchChanged(string value)
    {
        JournalLossAccountResults.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            IsJournalLossAccountResultsVisible = false;
            return;
        }

        var lower = value.ToLower();
        foreach (var acc in _allAccounts)
        {
            if ((acc.Code?.ToLower().Contains(lower) == true) ||
                (acc.Name?.ToLower().Contains(lower) == true))
            {
                JournalLossAccountResults.Add(acc);
                if (JournalLossAccountResults.Count >= 10) break;
            }
        }
        IsJournalLossAccountResultsVisible = JournalLossAccountResults.Count > 0;
    }

    [RelayCommand]
    private void SelectJournalLossAccount(Account? account)
    {
        JournalLossAccount = account;
        JournalLossAccountSearch = account != null ? $"{account.Code} {account.Name}" : string.Empty;
        JournalLossAccountResults.Clear();
        IsJournalLossAccountResultsVisible = false;
    }

    partial void OnJournalOutstandingReceiptsAccountSearchChanged(string value)
    {
        JournalOutstandingReceiptsAccountResults.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            IsJournalOutstandingReceiptsAccountResultsVisible = false;
            return;
        }

        var lower = value.ToLower();
        foreach (var acc in _allAccounts)
        {
            if ((acc.Code?.ToLower().Contains(lower) == true) ||
                (acc.Name?.ToLower().Contains(lower) == true))
            {
                JournalOutstandingReceiptsAccountResults.Add(acc);
                if (JournalOutstandingReceiptsAccountResults.Count >= 10) break;
            }
        }
        IsJournalOutstandingReceiptsAccountResultsVisible = JournalOutstandingReceiptsAccountResults.Count > 0;
    }

    [RelayCommand]
    private void SelectJournalOutstandingReceiptsAccount(Account? account)
    {
        JournalOutstandingReceiptsAccount = account;
        JournalOutstandingReceiptsAccountSearch = account != null ? $"{account.Code} {account.Name}" : string.Empty;
        JournalOutstandingReceiptsAccountResults.Clear();
        IsJournalOutstandingReceiptsAccountResultsVisible = false;
    }

    partial void OnJournalOutstandingPaymentsAccountSearchChanged(string value)
    {
        JournalOutstandingPaymentsAccountResults.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            IsJournalOutstandingPaymentsAccountResultsVisible = false;
            return;
        }

        var lower = value.ToLower();
        foreach (var acc in _allAccounts)
        {
            if ((acc.Code?.ToLower().Contains(lower) == true) ||
                (acc.Name?.ToLower().Contains(lower) == true))
            {
                JournalOutstandingPaymentsAccountResults.Add(acc);
                if (JournalOutstandingPaymentsAccountResults.Count >= 10) break;
            }
        }
        IsJournalOutstandingPaymentsAccountResultsVisible = JournalOutstandingPaymentsAccountResults.Count > 0;
    }

    [RelayCommand]
    private void SelectJournalOutstandingPaymentsAccount(Account? account)
    {
        JournalOutstandingPaymentsAccount = account;
        JournalOutstandingPaymentsAccountSearch = account != null ? $"{account.Code} {account.Name}" : string.Empty;
        JournalOutstandingPaymentsAccountResults.Clear();
        IsJournalOutstandingPaymentsAccountResultsVisible = false;
    }

    [ObservableProperty]
    private string _journalCurrency = "RWF";

    [ObservableProperty]
    private string _journalPaymentCommunicationType = "Based on Invoice";

    [ObservableProperty]
    private string _journalPaymentCommunicationStandard = string.Empty;

    [ObservableProperty]
    private string _journalErrorMessage = string.Empty;

    public ObservableCollection<string> JournalTypeOptions { get; } = new()
    {
        "Sales", "Purchase", "Cash", "Bank", "Credit Card", "Miscellaneous"
    };

    public ObservableCollection<string> PaymentCommunicationTypeOptions { get; } = new()
    {
        "Based on Invoice", "Based on Customer"
    };

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

    [ObservableProperty]
    private bool _isSudoModeEnabled;

    // Taxes Properties
    [ObservableProperty]
    private ObservableCollection<Tax> _taxes = new();

    [ObservableProperty]
    private ObservableCollection<Account> _taxAccounts = new();

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
    private Account? _taxSelectedAccount;

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

    public SettingsViewModel(
        SettingsService settingsService,
        LanguageService languageService,
        TaxService taxService,
        AccountService accountService,
        JournalService journalService,
        AccountingReportService accountingReportService,
        PaymentService paymentService,
        CustomFieldService customFieldService,
        CurrencyService currencyService,
        BudgetReportService budgetReportService,
        Action? onRequestShowWizard = null,
        Action? onModulesChanged = null)
    {
        _settingsService = settingsService;
        Language = languageService;
        _taxService = taxService;
        _accountService = accountService;
        _journalService = journalService;
        _accountingReportService = accountingReportService;
        _paymentService = paymentService;
        _customFieldService = customFieldService;
        _currencyService = currencyService;
        _budgetReportService = budgetReportService;
        _onRequestShowWizard = onRequestShowWizard;
        _onModulesChanged = onModulesChanged;

        var s = _settingsService.CurrentSettings;
        _storeName = s.StoreName;
        _storeAddress = s.StoreAddress;
        _currencySymbol = s.CurrencySymbol;
        _printerName = s.PrinterName;
        _isSudoModeEnabled = s.IsSudoModeEnabled;
        _statusMessage = "";

        foreach (var template in IndustryTemplateService.GetAvailableTemplates())
        {
            AvailableBusinessTemplates.Add(template);
        }

        // Load taxes
        _ = LoadTaxesAsync();
        // Load payments
        _ = LoadPaymentsDataAsync();
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
            s.IsSudoModeEnabled = IsSudoModeEnabled;

            _settingsService.SaveSettings();
            StatusMessage = "Settings saved successfully!";
        }
        catch
        {
            StatusMessage = "Failed to save settings.";
        }
    }

    [RelayCommand]
    private async Task ClearTransactionData()
    {
        try
        {
            await _accountService.ClearAllTransactionsAndStockAsync();
            StatusMessage = "All transaction and stock data cleared successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to clear data: {ex.Message}";
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
        TaxSelectedAccount = null;
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
        TaxSelectedAccount = _allAccounts.FirstOrDefault(a => a.Id == tax.AccountId);
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
                IsActive = TaxIsActive,
                AccountId = TaxSelectedAccount?.Id
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

    [RelayCommand]
    public async Task LoadAccountsAsync()
    {
        try
        {
            var list = await _accountService.GetAllAccountsAsync();
            _allAccounts = list ?? new List<Account>();
            
            TaxAccounts.Clear();
            foreach (var acc in _allAccounts)
            {
                TaxAccounts.Add(acc);
            }
            
            FilterAccounts();
        }
        catch (Exception ex)
        {
            AccountErrorMessage = $"Error loading accounts: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task LoadAvailableTaxesAsync()
    {
        try
        {
            var list = await _taxService.GetAllTaxesAsync();
            AvailableTaxes.Clear();
            foreach (var tax in list)
            {
                if (tax.IsActive)
                {
                    AvailableTaxes.Add(tax);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading available taxes: {ex.Message}");
        }
    }

    partial void OnAccountSearchQueryChanged(string value)
    {
        FilterAccounts();
    }

    partial void OnSelectedAccountGroupChanged(string value)
    {
        FilterAccounts();
    }

    private void FilterAccounts()
    {
        var query = AccountSearchQuery?.Trim().ToLower() ?? string.Empty;
        var group = SelectedAccountGroup ?? "All";

        var filtered = new List<Account>();
        foreach (var account in _allAccounts)
        {
            // Group prefix filter (prefix matches first digit of account code)
            if (group != "All")
            {
                if (string.IsNullOrEmpty(account.Code) || !account.Code.StartsWith(group))
                {
                    continue;
                }
            }

            // Search query filter
            if (!string.IsNullOrEmpty(query))
            {
                bool matchesCode = account.Code?.ToLower().Contains(query) == true;
                bool matchesName = account.Name?.ToLower().Contains(query) == true;
                bool matchesType = account.Type?.ToLower().Contains(query) == true;
                if (!matchesCode && !matchesName && !matchesType)
                {
                    continue;
                }
            }

            filtered.Add(account);
        }

        FilteredAccounts.Clear();
        foreach (var acc in filtered)
        {
            FilteredAccounts.Add(acc);
        }
    }

    [RelayCommand]
    private void OpenAddAccountForm()
    {
        AccountId = 0;
        NewAccountCode = string.Empty;
        NewAccountName = string.Empty;
        NewAccountType = AccountTypeOptions.Count > 0 ? AccountTypeOptions[0] : string.Empty;
        NewAccountSelectedTax = null;
        NewAccountCurrency = "RWF";
        NewAccountIsActive = true;
        NewAccountDescription = string.Empty;
        NewAccountPaymentReconciliation = false;
        AccountErrorMessage = string.Empty;
        IsAccountFormVisible = true;
    }

    [RelayCommand]
    private async Task OpenEditAccountFormAsync(Account account)
    {
        if (account == null) return;
        AccountId = account.Id;
        NewAccountCode = account.Code;
        NewAccountName = account.Name;
        NewAccountType = account.Type;
        NewAccountCurrency = account.Currency;
        NewAccountIsActive = account.IsActive;
        NewAccountDescription = account.Description;
        NewAccountPaymentReconciliation = account.PaymentReconciliation;
        AccountErrorMessage = string.Empty;

        // Map tax
        await LoadAvailableTaxesAsync();
        if (account.DefaultTaxId.HasValue)
        {
            NewAccountSelectedTax = null;
            foreach (var tax in AvailableTaxes)
            {
                if (tax.Id == account.DefaultTaxId.Value)
                {
                    NewAccountSelectedTax = tax;
                    break;
                }
            }
        }
        else
        {
            NewAccountSelectedTax = null;
        }

        IsAccountFormVisible = true;
    }

    [RelayCommand]
    private void CloseAccountForm()
    {
        IsAccountFormVisible = false;
    }

    [RelayCommand]
    private async Task SaveAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAccountCode))
        {
            AccountErrorMessage = "Code is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewAccountName))
        {
            AccountErrorMessage = "Account name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewAccountType))
        {
            AccountErrorMessage = "Account type is required.";
            return;
        }

        try
        {
            var account = new Account
            {
                Id = AccountId,
                Code = NewAccountCode.Trim(),
                Name = NewAccountName.Trim(),
                Type = NewAccountType,
                DefaultTaxId = NewAccountSelectedTax?.Id,
                Currency = NewAccountCurrency,
                IsActive = NewAccountIsActive,
                Description = NewAccountDescription?.Trim() ?? string.Empty,
                PaymentReconciliation = NewAccountPaymentReconciliation
            };

            if (AccountId == 0)
            {
                await _accountService.AddAccountAsync(account);
            }
            else
            {
                await _accountService.UpdateAccountAsync(account);
            }

            IsAccountFormVisible = false;
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            AccountErrorMessage = $"Error saving account: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteAccountAsync(Account account)
    {
        if (account == null) return;
        try
        {
            await _accountService.DeleteAccountAsync(account.Id);
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            AccountErrorMessage = $"Error deleting account: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleAccountStatusAsync(Account account)
    {
        if (account == null) return;
        try
        {
            account.IsActive = !account.IsActive;
            await _accountService.UpdateAccountAsync(account);
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            AccountErrorMessage = $"Error toggling status: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectGroup(string group)
    {
        SelectedAccountGroup = group;
    }

    // ── Journal Commands ──────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadJournalsAsync()
    {
        try
        {
            // Ensure accounts are loaded so display names can be resolved
            if (_allAccounts.Count == 0)
                await LoadAccountsAsync();

            var list = await _journalService.GetJournalListItemsAsync();
            Journals.Clear();
            foreach (var item in list)
                Journals.Add(item);
        }
        catch (Exception ex)
        {
            JournalErrorMessage = $"Error loading journals: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenAddJournalFormAsync()
    {
        if (_allAccounts.Count == 0)
            await LoadAccountsAsync();

        await LoadPaymentsDataAsync();

        JournalId = 0;
        JournalName = string.Empty;
        JournalType = "Miscellaneous";
        JournalSequencePrefix = string.Empty;
        JournalDefaultAccount = null;
        JournalAccountSearch = string.Empty;
        JournalAccountResults.Clear();
        IsJournalAccountResultsVisible = false;
        JournalCurrency = "RWF";
        JournalPaymentCommunicationType = "Based on Invoice";
        JournalPaymentCommunicationStandard = string.Empty;

        JournalBankAccount = null;
        JournalBankAccountSearch = string.Empty;
        JournalBankAccountResults.Clear();
        IsJournalBankAccountResultsVisible = false;

        JournalSuspenseAccount = null;
        JournalSuspenseAccountSearch = string.Empty;
        JournalSuspenseAccountResults.Clear();
        IsJournalSuspenseAccountResultsVisible = false;

        JournalProfitAccount = null;
        JournalProfitAccountSearch = string.Empty;
        JournalProfitAccountResults.Clear();
        IsJournalProfitAccountResultsVisible = false;

        JournalLossAccount = null;
        JournalLossAccountSearch = string.Empty;
        JournalLossAccountResults.Clear();
        IsJournalLossAccountResultsVisible = false;

        JournalOutstandingReceiptsAccount = null;
        JournalOutstandingReceiptsAccountSearch = string.Empty;
        JournalOutstandingReceiptsAccountResults.Clear();
        IsJournalOutstandingReceiptsAccountResultsVisible = false;

        JournalOutstandingPaymentsAccount = null;
        JournalOutstandingPaymentsAccountSearch = string.Empty;
        JournalOutstandingPaymentsAccountResults.Clear();
        IsJournalOutstandingPaymentsAccountResultsVisible = false;

        JournalSelectedLinkedBankAccount = null;

        JournalErrorMessage = string.Empty;
        IsJournalFormVisible = true;
    }

    [RelayCommand]
    private async Task OpenEditJournalFormAsync(JournalListItem item)
    {
        if (item == null) return;

        if (_allAccounts.Count == 0)
            await LoadAccountsAsync();

        await LoadPaymentsDataAsync();

        var journal = item.Journal;
        JournalId = journal.Id;
        JournalName = journal.Name;
        JournalType = journal.Type;
        JournalSequencePrefix = journal.SequencePrefix;
        JournalCurrency = journal.Currency;
        JournalPaymentCommunicationType = journal.PaymentCommunicationType;
        JournalPaymentCommunicationStandard = journal.PaymentCommunicationStandard;
        JournalErrorMessage = string.Empty;

        JournalDefaultAccount = null;
        JournalAccountSearch = string.Empty;
        JournalAccountResults.Clear();
        IsJournalAccountResultsVisible = false;

        if (journal.DefaultAccountId.HasValue)
        {
            foreach (var acc in _allAccounts)
            {
                if (acc.Id == journal.DefaultAccountId.Value)
                {
                    JournalDefaultAccount = acc;
                    JournalAccountSearch = $"{acc.Code} {acc.Name}";
                    break;
                }
            }
        }

        // Bank Account
        JournalBankAccount = null;
        JournalBankAccountSearch = string.Empty;
        JournalBankAccountResults.Clear();
        IsJournalBankAccountResultsVisible = false;
        if (journal.BankAccountId.HasValue)
        {
            var acc = _allAccounts.FirstOrDefault(a => a.Id == journal.BankAccountId.Value);
            if (acc != null)
            {
                JournalBankAccount = acc;
                JournalBankAccountSearch = $"{acc.Code} {acc.Name}";
            }
        }

        // Suspense Account
        JournalSuspenseAccount = null;
        JournalSuspenseAccountSearch = string.Empty;
        JournalSuspenseAccountResults.Clear();
        IsJournalSuspenseAccountResultsVisible = false;
        if (journal.SuspenseAccountId.HasValue)
        {
            var acc = _allAccounts.FirstOrDefault(a => a.Id == journal.SuspenseAccountId.Value);
            if (acc != null)
            {
                JournalSuspenseAccount = acc;
                JournalSuspenseAccountSearch = $"{acc.Code} {acc.Name}";
            }
        }

        // Profit Account
        JournalProfitAccount = null;
        JournalProfitAccountSearch = string.Empty;
        JournalProfitAccountResults.Clear();
        IsJournalProfitAccountResultsVisible = false;
        if (journal.ProfitAccountId.HasValue)
        {
            var acc = _allAccounts.FirstOrDefault(a => a.Id == journal.ProfitAccountId.Value);
            if (acc != null)
            {
                JournalProfitAccount = acc;
                JournalProfitAccountSearch = $"{acc.Code} {acc.Name}";
            }
        }

        // Loss Account
        JournalLossAccount = null;
        JournalLossAccountSearch = string.Empty;
        JournalLossAccountResults.Clear();
        IsJournalLossAccountResultsVisible = false;
        if (journal.LossAccountId.HasValue)
        {
            var acc = _allAccounts.FirstOrDefault(a => a.Id == journal.LossAccountId.Value);
            if (acc != null)
            {
                JournalLossAccount = acc;
                JournalLossAccountSearch = $"{acc.Code} {acc.Name}";
            }
        }

        // Outstanding Receipts Account
        JournalOutstandingReceiptsAccount = null;
        JournalOutstandingReceiptsAccountSearch = string.Empty;
        JournalOutstandingReceiptsAccountResults.Clear();
        IsJournalOutstandingReceiptsAccountResultsVisible = false;
        if (journal.OutstandingReceiptsAccountId.HasValue)
        {
            var acc = _allAccounts.FirstOrDefault(a => a.Id == journal.OutstandingReceiptsAccountId.Value);
            if (acc != null)
            {
                JournalOutstandingReceiptsAccount = acc;
                JournalOutstandingReceiptsAccountSearch = $"{acc.Code} {acc.Name}";
            }
        }

        // Outstanding Payments Account
        JournalOutstandingPaymentsAccount = null;
        JournalOutstandingPaymentsAccountSearch = string.Empty;
        JournalOutstandingPaymentsAccountResults.Clear();
        IsJournalOutstandingPaymentsAccountResultsVisible = false;
        if (journal.OutstandingPaymentsAccountId.HasValue)
        {
            var acc = _allAccounts.FirstOrDefault(a => a.Id == journal.OutstandingPaymentsAccountId.Value);
            if (acc != null)
            {
                JournalOutstandingPaymentsAccount = acc;
                JournalOutstandingPaymentsAccountSearch = $"{acc.Code} {acc.Name}";
            }
        }

        // Linked Bank Account
        JournalSelectedLinkedBankAccount = null;
        if (journal.LinkedBankAccountId.HasValue)
        {
            JournalSelectedLinkedBankAccount = BankAccounts.FirstOrDefault(b => b.Id == journal.LinkedBankAccountId.Value);
        }

        IsJournalFormVisible = true;
    }

    [RelayCommand]
    private void CloseJournalForm()
    {
        IsJournalFormVisible = false;
    }

    [RelayCommand]
    private async Task SaveJournalAsync()
    {
        if (string.IsNullOrWhiteSpace(JournalName))
        {
            JournalErrorMessage = "Journal name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(JournalSequencePrefix))
        {
            JournalErrorMessage = "Sequence prefix is required.";
            return;
        }

        try
        {
            var journal = new Journal
            {
                Id = JournalId,
                Name = JournalName.Trim(),
                Type = JournalType,
                SequencePrefix = JournalSequencePrefix.Trim().ToUpper(),
                DefaultAccountId = JournalDefaultAccount?.Id,
                Currency = JournalCurrency,
                PaymentCommunicationType = JournalPaymentCommunicationType,
                PaymentCommunicationStandard = JournalPaymentCommunicationStandard?.Trim() ?? string.Empty
            };

            if (JournalType == "Bank")
            {
                journal.BankAccountId = JournalBankAccount?.Id;
                journal.SuspenseAccountId = JournalSuspenseAccount?.Id;
                journal.ProfitAccountId = JournalProfitAccount?.Id;
                journal.LossAccountId = JournalLossAccount?.Id;
                journal.OutstandingReceiptsAccountId = JournalOutstandingReceiptsAccount?.Id;
                journal.OutstandingPaymentsAccountId = JournalOutstandingPaymentsAccount?.Id;
                journal.LinkedBankAccountId = JournalSelectedLinkedBankAccount?.Id;
            }

            if (JournalId == 0)
                await _journalService.AddJournalAsync(journal);
            else
                await _journalService.UpdateJournalAsync(journal);

            IsJournalFormVisible = false;
            await LoadJournalsAsync();
        }
        catch (Exception ex)
        {
            JournalErrorMessage = $"Error saving journal: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteJournalAsync(JournalListItem item)
    {
        if (item == null) return;
        try
        {
            await _journalService.DeleteJournalAsync(item.Journal.Id);
            await LoadJournalsAsync();
        }
        catch (Exception ex)
        {
            JournalErrorMessage = $"Error deleting journal: {ex.Message}";
        }
    }

    #region Accounting Reports

    [ObservableProperty]
    private string _reportErrorMessage = string.Empty;

    [ObservableProperty]
    private bool _isReportDetailVisible;

    [ObservableProperty]
    private AccountingReportWrapper? _selectedReport;

    [ObservableProperty]
    private string _reportDetailTitle = string.Empty;

    [ObservableProperty]
    private bool _isLineFormVisible;

    [ObservableProperty]
    private string _lineFormTitle = "Open: Lines";

    [ObservableProperty]
    private int _editingLineId;

    [ObservableProperty]
    private string _lineName = string.Empty;

    [ObservableProperty]
    private string _lineCode = string.Empty;

    [ObservableProperty]
    private int _lineLevel = 1;

    [ObservableProperty]
    private string _lineFoldability = "Foldable";

    [ObservableProperty]
    private string _lineGroupBy = "Use report's 'Group By'";

    [ObservableProperty]
    private bool _linePrintOnNewPage;

    [ObservableProperty]
    private bool _lineHideIfZero;

    [ObservableProperty]
    private string _lineAction = string.Empty;

    public ObservableCollection<AccountingReportWrapper> Reports { get; } = new();
    public ObservableCollection<ReportLineResult> ReportLines { get; } = new();
    public ObservableCollection<ReportLineComputation> LineComputations { get; } = new();

    public ObservableCollection<string> FoldabilityOptions { get; } = new() { "Foldable", "Always Expanded", "Never Foldable" };
    public ObservableCollection<string> ComputationEngineOptions { get; } = new() { "Prefix of Account Codes", "Sum of other lines", "Custom SQL" };

    private bool _selectAllReports;
    public bool SelectAllReports
    {
        get => _selectAllReports;
        set
        {
            if (SetProperty(ref _selectAllReports, value))
            {
                foreach (var r in Reports)
                {
                    r.IsSelected = value;
                }
            }
        }
    }

    [RelayCommand]
    public async Task LoadReportsAsync()
    {
        try
        {
            ReportErrorMessage = string.Empty;
            var list = await _accountingReportService.GetAllReportsAsync();
            Reports.Clear();
            foreach (var r in list)
            {
                Reports.Add(new AccountingReportWrapper(r));
            }
        }
        catch (Exception ex)
        {
            ReportErrorMessage = $"Failed to load reports: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task GoInsideReport(AccountingReportWrapper wrapper)
    {
        if (wrapper == null) return;
        ReportErrorMessage = string.Empty;
        SelectedReport = wrapper;
        ReportDetailTitle = wrapper.Report.Name;
        IsReportDetailVisible = true;
        OnPropertyChanged(nameof(IsAccountingReportsVisible));
        await LoadReportLinesAsync();
    }

    [RelayCommand]
    public void GoBackToReportsList()
    {
        SelectedReport = null;
        IsReportDetailVisible = false;
        OnPropertyChanged(nameof(IsAccountingReportsVisible));
    }

    [RelayCommand]
    public async Task LoadReportLinesAsync()
    {
        if (SelectedReport == null) return;
        try
        {
            ReportErrorMessage = string.Empty;
            var results = await _accountingReportService.ComputeReportBalancesAsync(SelectedReport.Report.Id);
            ReportLines.Clear();
            foreach (var res in results)
            {
                ReportLines.Add(res);
            }
        }
        catch (Exception ex)
        {
            ReportErrorMessage = $"Failed to load report lines: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenAddLineForm()
    {
        LineFormTitle = "Open: Lines";
        EditingLineId = 0;
        LineName = string.Empty;
        LineCode = string.Empty;
        LineLevel = 1;
        LineFoldability = "Foldable";
        LineGroupBy = "Use report's 'Group By'";
        LinePrintOnNewPage = false;
        LineHideIfZero = false;
        LineAction = string.Empty;
        LineComputations.Clear();
        LineComputations.Add(new ReportLineComputation { Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "" });
        IsLineFormVisible = true;
    }

    [RelayCommand]
    public async Task OpenEditLineForm(ReportLineResult res)
    {
        if (res == null) return;
        try
        {
            LineFormTitle = "Open: Lines";
            EditingLineId = res.LineId;
            LineName = res.Name;
            LineCode = res.Code;
            LineLevel = res.Level;
            LineFoldability = res.Foldability;
            LineGroupBy = res.GroupBy;
            LinePrintOnNewPage = res.PrintOnNewPage;
            LineHideIfZero = res.HideIfZero;
            LineAction = string.Empty;

            var comps = await _accountingReportService.GetReportLineComputationsAsync(res.LineId);
            LineComputations.Clear();
            foreach (var c in comps)
            {
                LineComputations.Add(c);
            }
            if (LineComputations.Count == 0)
            {
                LineComputations.Add(new ReportLineComputation { Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "" });
            }
            IsLineFormVisible = true;
        }
        catch (Exception ex)
        {
            ReportErrorMessage = $"Failed to load report line details: {ex.Message}";
        }
    }

    [RelayCommand]
    public void AddComputationRow()
    {
        LineComputations.Add(new ReportLineComputation { Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "" });
    }

    [RelayCommand]
    public void RemoveComputationRow(ReportLineComputation comp)
    {
        if (comp != null)
        {
            LineComputations.Remove(comp);
        }
    }

    [RelayCommand]
    public async Task SaveLine()
    {
        if (SelectedReport == null) return;
        if (string.IsNullOrWhiteSpace(LineName))
        {
            ReportErrorMessage = "Report line name is required.";
            return;
        }
        if (string.IsNullOrWhiteSpace(LineCode))
        {
            ReportErrorMessage = "Code is required.";
            return;
        }

        try
        {
            var line = new ReportLine
            {
                Id = EditingLineId,
                ReportId = SelectedReport.Report.Id,
                Name = LineName,
                Code = LineCode,
                Level = LineLevel,
                Foldability = LineFoldability,
                GroupBy = LineGroupBy,
                PrintOnNewPage = LinePrintOnNewPage,
                HideIfZero = LineHideIfZero
            };

            var compsList = LineComputations.ToList();

            if (EditingLineId == 0)
            {
                await _accountingReportService.AddReportLineAsync(line, compsList);
            }
            else
            {
                await _accountingReportService.UpdateReportLineAsync(line, compsList);
            }

            IsLineFormVisible = false;
            await LoadReportLinesAsync();
        }
        catch (Exception ex)
        {
            ReportErrorMessage = $"Failed to save report line: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteLine(ReportLineResult res)
    {
        if (res == null) return;
        try
        {
            await _accountingReportService.DeleteReportLineAsync(res.LineId);
            await LoadReportLinesAsync();
        }
        catch (Exception ex)
        {
            ReportErrorMessage = $"Failed to delete report line: {ex.Message}";
        }
    }

    [RelayCommand]
    public void DiscardLine()
    {
        IsLineFormVisible = false;
    }

    #endregion

    #region Payments Management

    // Payments Properties
    [ObservableProperty]
    private string _selectedPaymentsManagementType = "Banks"; // "Banks" or "Bank Accounts"

    public bool IsBanksSubTabActive => SelectedPaymentsManagementType == "Banks";
    public bool IsBankAccountsSubTabActive => SelectedPaymentsManagementType == "Bank Accounts";

    partial void OnSelectedPaymentsManagementTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsBanksSubTabActive));
        OnPropertyChanged(nameof(IsBankAccountsSubTabActive));
    }

    public ObservableCollection<string> PaymentsManagementTypes { get; } = new() { "Banks", "Bank Accounts" };

    // Banks
    [ObservableProperty]
    private ObservableCollection<Bank> _banks = new();

    [ObservableProperty]
    private Bank? _selectedBank;

    [ObservableProperty]
    private bool _isBankFormVisible;

    [ObservableProperty]
    private int _bankId;

    [ObservableProperty]
    private string _bankName = string.Empty;

    [ObservableProperty]
    private string _bankStreet = string.Empty;

    [ObservableProperty]
    private string _bankCity = string.Empty;

    [ObservableProperty]
    private string _bankCountry = string.Empty;

    [ObservableProperty]
    private string _bankPhone = string.Empty;

    [ObservableProperty]
    private string _bankEmail = string.Empty;

    [ObservableProperty]
    private string _bankErrorMessage = string.Empty;

    // Bank Accounts
    [ObservableProperty]
    private ObservableCollection<BankAccountDisplayRow> _bankAccounts = new();

    [ObservableProperty]
    private BankAccount? _selectedBankAccount;

    [ObservableProperty]
    private bool _isBankAccountFormVisible;

    [ObservableProperty]
    private int _bankAccountId;

    [ObservableProperty]
    private string _bankAccountNumber = string.Empty;

    [ObservableProperty]
    private string _bankAccountHolder = string.Empty;

    [ObservableProperty]
    private string _bankSearchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Bank> _matchedBanks = new();

    [ObservableProperty]
    private Bank? _bankAccountSelectedBank;

    [ObservableProperty]
    private string _bankAccountCurrency = "RWF";

    [ObservableProperty]
    private bool _bankAccountSendMoney = false;

    [ObservableProperty]
    private string _bankAccountErrorMessage = string.Empty;

    private List<Bank> _allBanksForSearch = new();

    public bool IsBankDropdownVisible => BankAccountSelectedBank == null && !string.IsNullOrWhiteSpace(BankSearchText) && MatchedBanks.Count > 0;

    partial void OnBankSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || (BankAccountSelectedBank != null && value == BankAccountSelectedBank.Name))
        {
            MatchedBanks.Clear();
            OnPropertyChanged(nameof(IsBankDropdownVisible));
            return;
        }

        var query = value.ToLower();
        var matches = _allBanksForSearch.Where(b => b.Name.ToLower().Contains(query)).Take(5).ToList();
        MatchedBanks.Clear();
        foreach (var m in matches)
        {
            MatchedBanks.Add(m);
        }
        OnPropertyChanged(nameof(IsBankDropdownVisible));
    }

    partial void OnBankAccountSelectedBankChanged(Bank? value)
    {
        if (value != null)
        {
            BankSearchText = value.Name;
        }
        OnPropertyChanged(nameof(IsBankDropdownVisible));
    }

    public async Task LoadPaymentsDataAsync()
    {
        try
        {
            var banksList = await _paymentService.GetAllBanksAsync();
            _allBanksForSearch = banksList;
            Banks = new ObservableCollection<Bank>(banksList);

            var accountsList = await _paymentService.GetAllBankAccountsAsync();
            var displayList = new List<BankAccountDisplayRow>();
            foreach (var acc in accountsList)
            {
                var bank = banksList.FirstOrDefault(b => b.Id == acc.BankId);
                displayList.Add(new BankAccountDisplayRow
                {
                    Account = acc,
                    BankName = bank?.Name ?? "Unknown Bank"
                });
            }
            BankAccounts = new ObservableCollection<BankAccountDisplayRow>(displayList);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load payments data: {ex.Message}");
        }
    }

    // Bank Actions
    [RelayCommand]
    private void OpenAddBankForm()
    {
        BankId = 0;
        BankName = string.Empty;
        BankStreet = string.Empty;
        BankCity = string.Empty;
        BankCountry = string.Empty;
        BankPhone = string.Empty;
        BankEmail = string.Empty;
        BankErrorMessage = string.Empty;
        IsBankFormVisible = true;
    }

    [RelayCommand]
    private void OpenEditBankForm(Bank bank)
    {
        if (bank == null) return;
        BankId = bank.Id;
        BankName = bank.Name;
        BankStreet = bank.Street;
        BankCity = bank.City;
        BankCountry = bank.Country;
        BankPhone = bank.Phone;
        BankEmail = bank.Email;
        BankErrorMessage = string.Empty;
        IsBankFormVisible = true;
    }

    [RelayCommand]
    private void CancelBankForm()
    {
        IsBankFormVisible = false;
    }

    [RelayCommand]
    private async Task SaveBankAsync()
    {
        if (string.IsNullOrWhiteSpace(BankName))
        {
            BankErrorMessage = "Bank Name is required.";
            return;
        }

        try
        {
            var bank = new Bank
            {
                Id = BankId,
                Name = BankName,
                Street = BankStreet,
                City = BankCity,
                Country = BankCountry,
                Phone = BankPhone,
                Email = BankEmail
            };

            if (BankId == 0)
            {
                await _paymentService.AddBankAsync(bank);
            }
            else
            {
                await _paymentService.UpdateBankAsync(bank);
            }

            IsBankFormVisible = false;
            await LoadPaymentsDataAsync();
        }
        catch (Exception ex)
        {
            BankErrorMessage = $"Error saving bank: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteBankAsync(Bank bank)
    {
        if (bank == null) return;
        try
        {
            await _paymentService.DeleteBankAsync(bank.Id);
            await LoadPaymentsDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete bank: {ex.Message}");
        }
    }

    // Bank Account Actions
    [RelayCommand]
    private void OpenAddBankAccountForm()
    {
        BankAccountId = 0;
        BankAccountNumber = string.Empty;
        BankAccountHolder = StoreName; // Default to company name using it
        BankAccountSelectedBank = null;
        BankSearchText = string.Empty;
        BankAccountCurrency = "RWF";
        BankAccountSendMoney = false;
        BankAccountErrorMessage = string.Empty;
        IsBankAccountFormVisible = true;
    }

    [RelayCommand]
    private void OpenEditBankAccountForm(BankAccountDisplayRow row)
    {
        if (row == null) return;
        var acc = row.Account;
        BankAccountId = acc.Id;
        BankAccountNumber = acc.AccountNumber;
        BankAccountHolder = acc.AccountHolder;
        BankAccountCurrency = acc.Currency;
        BankAccountSendMoney = acc.SendMoney;
        BankAccountErrorMessage = string.Empty;

        var bank = _allBanksForSearch.FirstOrDefault(b => b.Id == acc.BankId);
        BankAccountSelectedBank = bank;
        BankSearchText = bank?.Name ?? string.Empty;

        IsBankAccountFormVisible = true;
    }

    [RelayCommand]
    private void CancelBankAccountForm()
    {
        IsBankAccountFormVisible = false;
    }

    [RelayCommand]
    private void SelectSearchBank(Bank bank)
    {
        BankAccountSelectedBank = bank;
        MatchedBanks.Clear();
        OnPropertyChanged(nameof(IsBankDropdownVisible));
    }

    [RelayCommand]
    private async Task SaveBankAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(BankAccountNumber))
        {
            BankAccountErrorMessage = "Account Number is required.";
            return;
        }

        if (BankAccountSelectedBank == null)
        {
            BankAccountErrorMessage = "Please select a valid linked Bank.";
            return;
        }

        try
        {
            var acc = new BankAccount
            {
                Id = BankAccountId,
                AccountNumber = BankAccountNumber,
                AccountHolder = BankAccountHolder,
                BankId = BankAccountSelectedBank.Id,
                Currency = BankAccountCurrency,
                SendMoney = BankAccountSendMoney
            };

            if (BankAccountId == 0)
            {
                await _paymentService.AddBankAccountAsync(acc);
            }
            else
            {
                await _paymentService.UpdateBankAccountAsync(acc);
            }

            IsBankAccountFormVisible = false;
            await LoadPaymentsDataAsync();
        }
        catch (Exception ex)
        {
            BankAccountErrorMessage = $"Error saving bank account: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleSendMoneyAsync(BankAccountDisplayRow row)
    {
        if (row == null) return;
        try
        {
            row.Account.SendMoney = !row.Account.SendMoney;
            await _paymentService.UpdateBankAccountAsync(row.Account);
            await LoadPaymentsDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to toggle Send Money: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteBankAccountAsync(BankAccountDisplayRow row)
    {
        if (row == null) return;
        try
        {
            await _paymentService.DeleteBankAccountAsync(row.Id);
            await LoadPaymentsDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete bank account: {ex.Message}");
        }
    }

    #endregion

    #region Financial (Exchange Rates & Budget Lines)

    [ObservableProperty]
    private ObservableCollection<ExchangeRate> _exchangeRates = new();

    [ObservableProperty]
    private int _exchangeRateId;

    [ObservableProperty]
    private string _exchangeFromCurrency = "USD";

    [ObservableProperty]
    private string _exchangeToCurrency = "RWF";

    [ObservableProperty]
    private decimal _exchangeRateValue = 1m;

    [ObservableProperty]
    private DateTime _exchangeEffectiveDate = DateTime.Today;

    [ObservableProperty]
    private string _exchangeRateErrorMessage = string.Empty;

    [ObservableProperty]
    private int _budgetLineFiscalYear = DateTime.Today.Year;

    [ObservableProperty]
    private ObservableCollection<BudgetLineDisplayRow> _budgetLines = new();

    [ObservableProperty]
    private int _budgetLineId;

    [ObservableProperty]
    private Account? _budgetLineSelectedAccount;

    [ObservableProperty]
    private string _budgetLineAccountSearch = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Account> _budgetLineAccountResults = new();

    [ObservableProperty]
    private bool _isBudgetLineAccountResultsVisible;

    [ObservableProperty]
    private int _budgetLinePeriodMonth;

    [ObservableProperty]
    private decimal _budgetLineAmount;

    [ObservableProperty]
    private string _budgetLineNotes = string.Empty;

    [ObservableProperty]
    private string _budgetLineErrorMessage = string.Empty;

    [ObservableProperty]
    private string _financialStatusMessage = string.Empty;

    public ObservableCollection<int> BudgetPeriodMonthOptions { get; } = new()
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12
    };

    partial void OnBudgetLineAccountSearchChanged(string value)
    {
        BudgetLineAccountResults.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            IsBudgetLineAccountResultsVisible = false;
            return;
        }

        var lower = value.ToLower();
        foreach (var acc in _allAccounts)
        {
            if ((acc.Code?.ToLower().Contains(lower) == true) ||
                (acc.Name?.ToLower().Contains(lower) == true))
            {
                BudgetLineAccountResults.Add(acc);
                if (BudgetLineAccountResults.Count >= 10) break;
            }
        }
        IsBudgetLineAccountResultsVisible = BudgetLineAccountResults.Count > 0;
    }

    [RelayCommand]
    private void SelectBudgetLineAccount(Account? account)
    {
        BudgetLineSelectedAccount = account;
        BudgetLineAccountSearch = account != null ? $"{account.Code} {account.Name}" : string.Empty;
        BudgetLineAccountResults.Clear();
        IsBudgetLineAccountResultsVisible = false;
    }

    public async Task LoadFinancialDataAsync()
    {
        try
        {
            if (_allAccounts.Count == 0)
            {
                await LoadAccountsAsync();
            }

            var rates = await _currencyService.GetAllRatesAsync();
            ExchangeRates = new ObservableCollection<ExchangeRate>(rates);
            await LoadBudgetLinesAsync();
            FinancialStatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            FinancialStatusMessage = $"Failed to load financial settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadBudgetLinesAsync()
    {
        var lines = await _budgetReportService.GetBudgetLinesAsync(BudgetLineFiscalYear);
        var display = lines.Select(l => new BudgetLineDisplayRow
        {
            Line = l,
            AccountDisplay = _allAccounts.FirstOrDefault(a => a.Id == l.AccountId) is { } acc
                ? $"{acc.Code} - {acc.Name}"
                : $"Account #{l.AccountId}"
        }).ToList();
        BudgetLines = new ObservableCollection<BudgetLineDisplayRow>(display);
    }

    partial void OnBudgetLineFiscalYearChanged(int value)
    {
        if (SelectedTab == "Financial")
        {
            _ = LoadBudgetLinesAsync();
        }
    }

    [RelayCommand]
    private void OpenAddExchangeRateForm()
    {
        ExchangeRateId = 0;
        ExchangeFromCurrency = "USD";
        ExchangeToCurrency = "RWF";
        ExchangeRateValue = 1m;
        ExchangeEffectiveDate = DateTime.Today;
        ExchangeRateErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void OpenEditExchangeRateForm(ExchangeRate rate)
    {
        if (rate == null) return;
        ExchangeRateId = rate.Id;
        ExchangeFromCurrency = rate.FromCurrency;
        ExchangeToCurrency = rate.ToCurrency;
        ExchangeRateValue = rate.Rate;
        ExchangeEffectiveDate = rate.EffectiveDate;
        ExchangeRateErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SaveExchangeRateAsync()
    {
        if (string.IsNullOrWhiteSpace(ExchangeFromCurrency) || string.IsNullOrWhiteSpace(ExchangeToCurrency))
        {
            ExchangeRateErrorMessage = "Both currencies are required.";
            return;
        }

        if (ExchangeRateValue <= 0)
        {
            ExchangeRateErrorMessage = "Rate must be greater than zero.";
            return;
        }

        try
        {
            var rate = new ExchangeRate
            {
                Id = ExchangeRateId,
                FromCurrency = ExchangeFromCurrency,
                ToCurrency = ExchangeToCurrency,
                Rate = ExchangeRateValue,
                EffectiveDate = ExchangeEffectiveDate
            };
            await _currencyService.SaveRateAsync(rate);
            await LoadFinancialDataAsync();
            FinancialStatusMessage = "Exchange rate saved.";
            ExchangeRateErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ExchangeRateErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteExchangeRateAsync(ExchangeRate rate)
    {
        if (rate == null) return;
        try
        {
            await _currencyService.DeleteRateAsync(rate.Id);
            await LoadFinancialDataAsync();
            FinancialStatusMessage = "Exchange rate deleted.";
        }
        catch (Exception ex)
        {
            ExchangeRateErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenAddBudgetLineForm()
    {
        BudgetLineId = 0;
        BudgetLineSelectedAccount = null;
        BudgetLineAccountSearch = string.Empty;
        BudgetLinePeriodMonth = 0;
        BudgetLineAmount = 0;
        BudgetLineNotes = string.Empty;
        BudgetLineErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void OpenEditBudgetLineForm(BudgetLineDisplayRow row)
    {
        if (row == null) return;
        var line = row.Line;
        BudgetLineId = line.Id;
        BudgetLinePeriodMonth = line.PeriodMonth;
        BudgetLineAmount = line.BudgetAmount;
        BudgetLineNotes = line.Notes;
        BudgetLineSelectedAccount = _allAccounts.FirstOrDefault(a => a.Id == line.AccountId);
        BudgetLineAccountSearch = BudgetLineSelectedAccount != null
            ? $"{BudgetLineSelectedAccount.Code} {BudgetLineSelectedAccount.Name}"
            : string.Empty;
        BudgetLineErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SaveBudgetLineAsync()
    {
        if (BudgetLineSelectedAccount == null)
        {
            BudgetLineErrorMessage = "Select an account.";
            return;
        }

        if (BudgetLineAmount < 0)
        {
            BudgetLineErrorMessage = "Budget amount cannot be negative.";
            return;
        }

        try
        {
            var line = new BudgetLine
            {
                Id = BudgetLineId,
                FiscalYear = BudgetLineFiscalYear,
                AccountId = BudgetLineSelectedAccount.Id,
                PeriodMonth = BudgetLinePeriodMonth,
                BudgetAmount = BudgetLineAmount,
                Notes = BudgetLineNotes?.Trim() ?? string.Empty
            };
            await _budgetReportService.SaveBudgetLineAsync(line);
            await LoadBudgetLinesAsync();
            FinancialStatusMessage = "Budget line saved.";
            BudgetLineErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            BudgetLineErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteBudgetLineAsync(BudgetLineDisplayRow row)
    {
        if (row == null) return;
        try
        {
            await _budgetReportService.DeleteBudgetLineAsync(row.Line.Id);
            await LoadBudgetLinesAsync();
            FinancialStatusMessage = "Budget line deleted.";
        }
        catch (Exception ex)
        {
            BudgetLineErrorMessage = ex.Message;
        }
    }

    #endregion

    #region Business Setup

    public ObservableCollection<IndustryTemplate> AvailableBusinessTemplates { get; } = new();

    public string CurrentBusinessTypeDisplay
    {
        get
        {
            var type = _settingsService.CurrentSettings.BusinessType;
            if (string.IsNullOrWhiteSpace(type)) return "Not configured yet";
            if (type == "custom") return "Custom (manually configured)";
            var match = AvailableBusinessTemplates.FirstOrDefault(t => t.Key == type);
            return match?.DisplayName ?? type;
        }
    }

    public bool IsBusinessSetupCompleted => _settingsService.CurrentSettings.SetupCompleted;

    [RelayCommand]
    private void RunSetupWizard()
    {
        _onRequestShowWizard?.Invoke();
    }

    #endregion

    #region Custom Fields

    public ObservableCollection<string> CustomFieldEntityTypes { get; } = new()
    {
        "Product", "Customer", "Supplier", "SalesOrder", "PurchaseOrder"
    };

    public ObservableCollection<string> CustomFieldTypeOptions { get; } = new()
    {
        "Text", "Number", "Date", "Boolean", "Choice"
    };

    [ObservableProperty]
    private string _selectedCustomFieldEntityType = "Product";

    partial void OnSelectedCustomFieldEntityTypeChanged(string value)
    {
        _ = LoadCustomFieldDefinitionsAsync();
    }

    [ObservableProperty]
    private ObservableCollection<CustomFieldDefinition> _customFieldDefinitions = new();

    [ObservableProperty]
    private bool _isCustomFieldFormVisible;

    [ObservableProperty]
    private int _customFieldFormId;

    [ObservableProperty]
    private string _customFieldFormLabel = string.Empty;

    [ObservableProperty]
    private string _customFieldFormType = "Text";

    public bool IsCustomFieldChoiceOptionsVisible => CustomFieldFormType == "Choice";

    partial void OnCustomFieldFormTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsCustomFieldChoiceOptionsVisible));
    }

    [ObservableProperty]
    private string _customFieldFormChoiceOptions = string.Empty;

    [ObservableProperty]
    private bool _customFieldFormIsRequired;

    [ObservableProperty]
    private string _customFieldErrorMessage = string.Empty;

    [RelayCommand]
    public async Task LoadCustomFieldDefinitionsAsync()
    {
        try
        {
            var defs = await _customFieldService.GetDefinitionsAsync(SelectedCustomFieldEntityType, activeOnly: false);
            CustomFieldDefinitions = new ObservableCollection<CustomFieldDefinition>(defs);
        }
        catch (Exception ex)
        {
            CustomFieldErrorMessage = $"Error loading custom fields: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenAddCustomFieldForm()
    {
        CustomFieldFormId = 0;
        CustomFieldFormLabel = string.Empty;
        CustomFieldFormType = "Text";
        CustomFieldFormChoiceOptions = string.Empty;
        CustomFieldFormIsRequired = false;
        CustomFieldErrorMessage = string.Empty;
        IsCustomFieldFormVisible = true;
    }

    [RelayCommand]
    private void OpenEditCustomFieldForm(CustomFieldDefinition def)
    {
        if (def == null) return;
        CustomFieldFormId = def.Id;
        CustomFieldFormLabel = def.FieldLabel;
        CustomFieldFormType = def.FieldType;
        CustomFieldFormChoiceOptions = def.ChoiceOptions;
        CustomFieldFormIsRequired = def.IsRequired;
        CustomFieldErrorMessage = string.Empty;
        IsCustomFieldFormVisible = true;
    }

    [RelayCommand]
    private void CloseCustomFieldForm()
    {
        IsCustomFieldFormVisible = false;
    }

    [RelayCommand]
    private async Task SaveCustomFieldAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomFieldFormLabel))
        {
            CustomFieldErrorMessage = "Field label is required.";
            return;
        }

        try
        {
            if (CustomFieldFormId == 0)
            {
                var fieldKey = new string(CustomFieldFormLabel.Where(char.IsLetterOrDigit).ToArray());
                if (string.IsNullOrWhiteSpace(fieldKey))
                {
                    fieldKey = $"Field{DateTime.UtcNow.Ticks}";
                }

                var def = new CustomFieldDefinition
                {
                    EntityType = SelectedCustomFieldEntityType,
                    FieldKey = fieldKey,
                    FieldLabel = CustomFieldFormLabel.Trim(),
                    FieldType = CustomFieldFormType,
                    ChoiceOptions = CustomFieldFormType == "Choice" ? CustomFieldFormChoiceOptions.Trim() : string.Empty,
                    IsRequired = CustomFieldFormIsRequired,
                    IsActive = true
                };
                await _customFieldService.AddDefinitionAsync(def);
            }
            else
            {
                var existing = CustomFieldDefinitions.FirstOrDefault(d => d.Id == CustomFieldFormId);
                if (existing == null) return;
                existing.FieldLabel = CustomFieldFormLabel.Trim();
                existing.FieldType = CustomFieldFormType;
                existing.ChoiceOptions = CustomFieldFormType == "Choice" ? CustomFieldFormChoiceOptions.Trim() : string.Empty;
                existing.IsRequired = CustomFieldFormIsRequired;
                await _customFieldService.UpdateDefinitionAsync(existing);
            }

            IsCustomFieldFormVisible = false;
            await LoadCustomFieldDefinitionsAsync();
        }
        catch (Exception ex)
        {
            CustomFieldErrorMessage = $"Error saving custom field: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeactivateCustomFieldAsync(CustomFieldDefinition def)
    {
        if (def == null) return;
        try
        {
            await _customFieldService.DeactivateDefinitionAsync(def.Id);
            await LoadCustomFieldDefinitionsAsync();
        }
        catch (Exception ex)
        {
            CustomFieldErrorMessage = $"Error deactivating custom field: {ex.Message}";
        }
    }

    #endregion

    #region Terminology

    private static readonly (string Key, string DefaultLabel)[] TerminologyKeys =
    {
        ("Product", "Product"),
        ("Customer", "Customer"),
        ("Supplier", "Supplier"),
        ("Category", "Category"),
        ("Order", "Order"),
        ("Invoice", "Invoice")
    };

    public ObservableCollection<TerminologyTermItem> TerminologyTerms { get; } = new();

    [ObservableProperty]
    private string _terminologyStatusMessage = string.Empty;

    private void LoadTerminologyTerms()
    {
        var overrides = _settingsService.CurrentSettings.TerminologyOverrides;
        TerminologyTerms.Clear();
        foreach (var (key, defaultLabel) in TerminologyKeys)
        {
            TerminologyTerms.Add(new TerminologyTermItem
            {
                Key = key,
                DefaultLabel = defaultLabel,
                OverrideText = overrides.TryGetValue(key, out var val) ? val : string.Empty
            });
        }
        TerminologyStatusMessage = string.Empty;
    }

    [RelayCommand]
    private void SaveTerminology()
    {
        try
        {
            var overrides = _settingsService.CurrentSettings.TerminologyOverrides;
            foreach (var term in TerminologyTerms)
            {
                if (string.IsNullOrWhiteSpace(term.OverrideText))
                {
                    overrides.Remove(term.Key);
                }
                else
                {
                    overrides[term.Key] = term.OverrideText.Trim();
                }
            }

            _settingsService.SaveSettings();
            Language.SetTerminologyOverrides(overrides);
            TerminologyStatusMessage = "Terminology saved successfully!";
        }
        catch (Exception ex)
        {
            TerminologyStatusMessage = $"Failed to save terminology: {ex.Message}";
        }
    }

    #endregion

    #region Modules

    private static readonly (string Key, string DisplayName)[] ModuleKeys =
    {
        ("POS", "Point of Sale"),
        ("Manufacturing", "Manufacturing"),
        ("BOM", "Bundles / Bill of Materials"),
        ("Expiry", "Expiry & Batch Tracking"),
        ("MultiLocation", "Multi-Location & Stock Transfers")
    };

    public ObservableCollection<ModuleToggleItem> ModuleToggles { get; } = new();

    [ObservableProperty]
    private string _modulesStatusMessage = string.Empty;

    private void LoadModuleToggles()
    {
        var modules = _settingsService.CurrentSettings.EnabledModules;
        ModuleToggles.Clear();
        foreach (var (key, displayName) in ModuleKeys)
        {
            ModuleToggles.Add(new ModuleToggleItem
            {
                Key = key,
                DisplayName = displayName,
                IsEnabled = !modules.TryGetValue(key, out var enabled) || enabled
            });
        }
        ModulesStatusMessage = string.Empty;
    }

    [RelayCommand]
    private void SaveModules()
    {
        try
        {
            var modules = _settingsService.CurrentSettings.EnabledModules;
            foreach (var item in ModuleToggles)
            {
                modules[item.Key] = item.IsEnabled;
            }

            _settingsService.SaveSettings();
            _onModulesChanged?.Invoke();
            ModulesStatusMessage = "Modules saved successfully!";
        }
        catch (Exception ex)
        {
            ModulesStatusMessage = $"Failed to save modules: {ex.Message}";
        }
    }

    #endregion
}

public class TerminologyTermItem : ObservableObject
{
    public string Key { get; set; } = string.Empty;
    public string DefaultLabel { get; set; } = string.Empty;

    private string _overrideText = string.Empty;
    public string OverrideText
    {
        get => _overrideText;
        set => SetProperty(ref _overrideText, value);
    }
}

public class ModuleToggleItem : ObservableObject
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}

public class BudgetLineDisplayRow
{
    public BudgetLine Line { get; set; } = new();
    public string AccountDisplay { get; set; } = string.Empty;
    public int Id => Line.Id;
    public int FiscalYear => Line.FiscalYear;
    public int PeriodMonth => Line.PeriodMonth;
    public decimal BudgetAmount => Line.BudgetAmount;
    public string Notes => Line.Notes;
}

public class BankAccountDisplayRow
{
    public BankAccount Account { get; set; } = new();
    public string BankName { get; set; } = string.Empty;
    public int Id => Account.Id;
    public string AccountNumber => Account.AccountNumber;
    public string AccountHolder => Account.AccountHolder;
    public string Currency => Account.Currency;
    public bool SendMoney => Account.SendMoney;
}

public class AccountingReportWrapper : ObservableObject
{
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public AccountingReport Report { get; }

    public AccountingReportWrapper(AccountingReport report)
    {
        Report = report;
    }
}
