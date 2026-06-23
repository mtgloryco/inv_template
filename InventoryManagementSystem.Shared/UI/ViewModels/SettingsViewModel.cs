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
    private readonly AccountService _accountService;
    private readonly JournalService _journalService;
    private readonly AccountingReportService _accountingReportService;

    // Tabs
    [ObservableProperty]
    private string _selectedTab = "General"; // "General", "Taxes", "Accounting"

    public bool IsGeneralTabActive => SelectedTab == "General";
    public bool IsTaxesTabActive => SelectedTab == "Taxes";
    public bool IsAccountingTabActive => SelectedTab == "Accounting";

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsGeneralTabActive));
        OnPropertyChanged(nameof(IsTaxesTabActive));
        OnPropertyChanged(nameof(IsAccountingTabActive));

        if (value == "Accounting")
        {
            _ = LoadAccountsAsync();
            _ = LoadAvailableTaxesAsync();
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

    public SettingsViewModel(SettingsService settingsService, LanguageService languageService, TaxService taxService, AccountService accountService, JournalService journalService, AccountingReportService accountingReportService)
    {
        _settingsService = settingsService;
        Language = languageService;
        _taxService = taxService;
        _accountService = accountService;
        _journalService = journalService;
        _accountingReportService = accountingReportService;

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

    [RelayCommand]
    public async Task LoadAccountsAsync()
    {
        try
        {
            var list = await _accountService.GetAllAccountsAsync();
            _allAccounts = list ?? new List<Account>();
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
        JournalErrorMessage = string.Empty;
        IsJournalFormVisible = true;
    }

    [RelayCommand]
    private async Task OpenEditJournalFormAsync(JournalListItem item)
    {
        if (item == null) return;

        if (_allAccounts.Count == 0)
            await LoadAccountsAsync();

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
