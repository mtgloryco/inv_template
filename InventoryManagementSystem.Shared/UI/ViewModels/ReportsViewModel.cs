using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public class ReportNavItem
    {
        public string Key { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    public partial class ReportsViewModel : ViewModelBase
    {
        private static readonly ReportNavItem[] ReportCatalog =
        {
            new() { Key = "balance-sheet", Title = "Balance Sheet", Category = "Financial Statements", Description = "Assets, liabilities, and equity snapshot" },
            new() { Key = "profit-loss", Title = "Profit & Loss", Category = "Financial Statements", Description = "Income and expenses for the period" },
            new() { Key = "budget-vs-actual", Title = "Budget vs Actual", Category = "Financial Statements", Description = "Compare budgeted vs actual spending" },
            new() { Key = "stock-status", Title = "Stock Status", Category = "Inventory", Description = "Current stock levels by product" },
            new() { Key = "stock-history", Title = "Stock History", Category = "Inventory", Description = "Recent stock movement audit trail" },
            new() { Key = "ar-aging", Title = "AR Aging", Category = "Receivables & Payables", Description = "Outstanding customer invoices by age" },
            new() { Key = "ap-aging", Title = "AP Aging", Category = "Receivables & Payables", Description = "Outstanding vendor bills by age" },
            new() { Key = "vat-return", Title = "VAT Return", Category = "Tax & Banking", Description = "Output vs input VAT for filing" },
            new() { Key = "bank-reconciliation", Title = "Bank Reconciliation", Category = "Tax & Banking", Description = "Match payments to bank statements" },
            new() { Key = "abc-analysis", Title = "ABC Analysis", Category = "Advanced Analytics", Description = "Revenue-based product classification (A/B/C)" },
            new() { Key = "dead-stock", Title = "Dead Stock", Category = "Advanced Analytics", Description = "Products with stock but no recent sales" },
            new() { Key = "margin-by-category", Title = "Margin by Category", Category = "Advanced Analytics", Description = "Gross margin breakdown by product category" },
            new() { Key = "month-close", Title = "Month Close Summary", Category = "Advanced Analytics", Description = "Trial balance and open AR/AP for period close" },
        };

        private readonly InventoryService _inventoryService;
        private readonly LicenseService _licenseService;
        private readonly SettingsService _settingsService;
        private readonly AccountingReportService _accountingReportService;
        private readonly AgingReportService _agingReportService;
        private readonly VatExportService _vatExportService;
        private readonly BudgetReportService _budgetReportService;
        private readonly PaymentService _paymentService;
        private readonly AdvancedAnalyticsService _advancedAnalyticsService;
        private readonly MonthCloseService _monthCloseService;

        [ObservableProperty] private string _selectedCategory = "Financial Statements";
        [ObservableProperty] private ReportNavItem? _selectedReportNavItem;
        [ObservableProperty] private ObservableCollection<ReportNavItem> _reportsInCategory = new();
        [ObservableProperty] private ObservableCollection<ReportLineWrapper> _balanceSheetLines = new();
        [ObservableProperty] private ObservableCollection<ReportLineWrapper> _profitAndLossLines = new();
        [ObservableProperty] private ObservableCollection<AgingLine> _arAgingLines = new();
        [ObservableProperty] private ObservableCollection<AgingLine> _apAgingLines = new();
        [ObservableProperty] private AgingSummary _arAgingSummary = new();
        [ObservableProperty] private AgingSummary _apAgingSummary = new();
        [ObservableProperty] private bool _isLoadingReport;

        [ObservableProperty] private ObservableCollection<Product> _reportData = new();
        [ObservableProperty] private ObservableCollection<StockMovement> _stockHistoryData = new();
        [ObservableProperty] private ObservableCollection<MonthlyProfitReport> _monthlyProfitData = new();
        [ObservableProperty] private string _reportTitle = "Balance Sheet";
        [ObservableProperty] private bool _isLowStockReport;
        [ObservableProperty] private bool _isHistoryReport;
        [ObservableProperty] private bool _isProfitReport;

        public bool IsStockReport => !IsHistoryReport && !IsProfitReport;

        public bool IsBalanceSheetSelected => SelectedReportNavItem?.Key == "balance-sheet";
        public bool IsProfitAndLossSelected => SelectedReportNavItem?.Key == "profit-loss";
        public bool IsStockStatusSelected => SelectedReportNavItem?.Key == "stock-status";
        public bool IsStockHistorySelected => SelectedReportNavItem?.Key == "stock-history";
        public bool IsArAgingSelected => SelectedReportNavItem?.Key == "ar-aging";
        public bool IsApAgingSelected => SelectedReportNavItem?.Key == "ap-aging";
        public bool IsVatReturnSelected => SelectedReportNavItem?.Key == "vat-return";
        public bool IsBudgetVsActualSelected => SelectedReportNavItem?.Key == "budget-vs-actual";
        public bool IsBankReconciliationSelected => SelectedReportNavItem?.Key == "bank-reconciliation";

        public List<string> ReportCategories { get; } = ReportCatalog
            .Select(r => r.Category)
            .Distinct()
            .ToList();

        [ObservableProperty] private DateTime _vatPeriodStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        [ObservableProperty] private DateTime _vatPeriodEnd = DateTime.Today;
        [ObservableProperty] private VatReturnSummary? _vatSummary;

        [ObservableProperty] private int _budgetFiscalYear = DateTime.Today.Year;
        [ObservableProperty] private int _budgetPeriodMonth;
        [ObservableProperty] private ObservableCollection<BudgetVsActualLine> _budgetVsActualLines = new();
        [ObservableProperty] private decimal _budgetTotalBudget;
        [ObservableProperty] private decimal _budgetTotalActual;
        [ObservableProperty] private decimal _budgetTotalVariance;

        [ObservableProperty] private ObservableCollection<BankAccountDisplayRow> _reconBankAccounts = new();
        [ObservableProperty] private BankAccountDisplayRow? _selectedReconBankAccount;
        [ObservableProperty] private ObservableCollection<ReconciliationCandidate> _unreconciledPayments = new();
        [ObservableProperty] private ObservableCollection<ReconciliationCandidate> _unreconciledStatementLines = new();
        [ObservableProperty] private ReconciliationCandidate? _selectedReconPayment;
        [ObservableProperty] private ReconciliationCandidate? _selectedReconLine;
        [ObservableProperty] private string _reconStatusMessage = string.Empty;

        [ObservableProperty] private ObservableCollection<AbcAnalysisLine> _abcAnalysisLines = new();
        [ObservableProperty] private ObservableCollection<ProductRecommendation> _deadStockLines = new();
        [ObservableProperty] private ObservableCollection<CategoryMarginLine> _categoryMarginLines = new();
        [ObservableProperty] private MonthCloseSummary? _monthCloseSummary;

        [ObservableProperty] private bool _isImportStatementModalOpen;
        [ObservableProperty] private DateTime _importStatementDate = DateTime.Today;
        [ObservableProperty] private decimal _importOpeningBalance;
        [ObservableProperty] private decimal _importClosingBalance;
        [ObservableProperty] private DateTime _importLineDate = DateTime.Today;
        [ObservableProperty] private string _importLineDescription = string.Empty;
        [ObservableProperty] private decimal _importLineAmount;
        [ObservableProperty] private string _importLineReference = string.Empty;
        [ObservableProperty] private string _importErrorMessage = string.Empty;

        partial void OnSelectedCategoryChanged(string value)
        {
            RefreshReportsInCategory();
        }

        partial void OnSelectedReportNavItemChanged(ReportNavItem? value)
        {
            NotifyReportSelectionProperties();
            if (value != null)
            {
                ReportTitle = value.Title;
                _ = LoadSelectedReportAsync();
            }
        }

        private void RefreshReportsInCategory()
        {
            var items = ReportCatalog
                .Where(r => r.Category == SelectedCategory)
                .ToList();

            ReportsInCategory = new ObservableCollection<ReportNavItem>(items);
            SelectedReportNavItem = items.FirstOrDefault();
        }

        private void NotifyReportSelectionProperties()
        {
            OnPropertyChanged(nameof(IsBalanceSheetSelected));
            OnPropertyChanged(nameof(IsProfitAndLossSelected));
            OnPropertyChanged(nameof(IsStockStatusSelected));
            OnPropertyChanged(nameof(IsStockHistorySelected));
            OnPropertyChanged(nameof(IsArAgingSelected));
            OnPropertyChanged(nameof(IsApAgingSelected));
            OnPropertyChanged(nameof(IsVatReturnSelected));
            OnPropertyChanged(nameof(IsBudgetVsActualSelected));
            OnPropertyChanged(nameof(IsBankReconciliationSelected));
            OnPropertyChanged(nameof(IsAbcAnalysisSelected));
            OnPropertyChanged(nameof(IsDeadStockSelected));
            OnPropertyChanged(nameof(IsMarginByCategorySelected));
            OnPropertyChanged(nameof(IsMonthCloseSelected));
        }

        public bool IsAbcAnalysisSelected => SelectedReportNavItem?.Key == "abc-analysis";
        public bool IsDeadStockSelected => SelectedReportNavItem?.Key == "dead-stock";
        public bool IsMarginByCategorySelected => SelectedReportNavItem?.Key == "margin-by-category";
        public bool IsMonthCloseSelected => SelectedReportNavItem?.Key == "month-close";

        partial void OnSelectedReconBankAccountChanged(BankAccountDisplayRow? value)
        {
            if (IsBankReconciliationSelected)
            {
                _ = LoadBankReconciliationAsync();
            }
        }

        public string CurrencySymbol => _settingsService.CurrentSettings.CurrencySymbol;
        public LanguageService Language { get; }

        public ReportsViewModel(
            InventoryService inventoryService, 
            LicenseService licenseService, 
            SettingsService settingsService, 
            LanguageService languageService,
            AccountingReportService accountingReportService,
            AgingReportService agingReportService,
            VatExportService vatExportService,
            BudgetReportService budgetReportService,
            PaymentService paymentService,
            AdvancedAnalyticsService advancedAnalyticsService,
            MonthCloseService monthCloseService)
        {
            _inventoryService = inventoryService;
            _licenseService = licenseService;
            _settingsService = settingsService;
            Language = languageService;
            _accountingReportService = accountingReportService;
            _agingReportService = agingReportService;
            _vatExportService = vatExportService;
            _budgetReportService = budgetReportService;
            _paymentService = paymentService;
            _advancedAnalyticsService = advancedAnalyticsService;
            _monthCloseService = monthCloseService;

            RefreshReportsInCategory();
        }

        public async Task LoadSelectedReportAsync()
        {
            IsLoadingReport = true;
            try
            {
                switch (SelectedReportNavItem?.Key)
                {
                    case "balance-sheet":
                        await LoadBalanceSheetAsync();
                        break;
                    case "profit-loss":
                        await LoadProfitAndLossAsync();
                        break;
                    case "stock-status":
                        if (IsLowStockReport)
                            await LoadLowStockReport();
                        else
                            await LoadStockReport();
                        break;
                    case "stock-history":
                        await LoadStockHistoryReport();
                        break;
                    case "ar-aging":
                        await LoadArAgingAsync();
                        break;
                    case "ap-aging":
                        await LoadApAgingAsync();
                        break;
                    case "vat-return":
                        await LoadVatReturnAsync();
                        break;
                    case "budget-vs-actual":
                        await LoadBudgetVsActualAsync();
                        break;
                    case "bank-reconciliation":
                        await LoadBankReconciliationAsync();
                        break;
                    case "abc-analysis":
                        await LoadAbcAnalysisAsync();
                        break;
                    case "dead-stock":
                        await LoadDeadStockAsync();
                        break;
                    case "margin-by-category":
                        await LoadMarginByCategoryAsync();
                        break;
                    case "month-close":
                        await LoadMonthCloseAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                ReportTitle = $"Error loading report: {ex.Message}";
            }
            finally
            {
                IsLoadingReport = false;
            }
        }

        [RelayCommand]
        public async Task LoadBalanceSheetAsync()
        {
            ReportTitle = "Balance Sheet";
            var reports = await _accountingReportService.GetAllReportsAsync();
            var bsReport = reports.FirstOrDefault(r => r.Name == "Balance Sheet");
            if (bsReport == null)
            {
                ReportTitle = "Balance Sheet Config Not Found";
                return;
            }

            var results = await _accountingReportService.ComputeReportBalancesAsync(bsReport.Id);
            BalanceSheetLines.Clear();
            foreach (var r in results)
            {
                BalanceSheetLines.Add(new ReportLineWrapper(r, CurrencySymbol));
            }
        }

        [RelayCommand]
        public async Task LoadProfitAndLossAsync()
        {
            ReportTitle = "Profit and Loss Statement";
            var reports = await _accountingReportService.GetAllReportsAsync();
            var pnlReport = reports.FirstOrDefault(r => r.Name == "Profit and Loss");
            if (pnlReport == null)
            {
                ReportTitle = "Profit & Loss Config Not Found";
                return;
            }

            var results = await _accountingReportService.ComputeReportBalancesAsync(pnlReport.Id);
            ProfitAndLossLines.Clear();
            foreach (var r in results)
            {
                ProfitAndLossLines.Add(new ReportLineWrapper(r, CurrencySymbol));
            }
        }

        [RelayCommand]
        private async Task LoadStockReport()
        {
            ReportTitle = "Current Stock Report";
            IsLowStockReport = false;
            IsHistoryReport = false;
            IsProfitReport = false;
            OnPropertyChanged(nameof(IsStockReport));
            var list = await _inventoryService.GetAllProductsAsync();
            ReportData = new ObservableCollection<Product>(list);
        }

        [RelayCommand]
        private async Task LoadLowStockReport()
        {
            ReportTitle = "Low Stock Report (< 5 items)";
            IsLowStockReport = true;
            IsHistoryReport = false;
            IsProfitReport = false;
            OnPropertyChanged(nameof(IsStockReport));
            var list = await _inventoryService.GetLowStockProductsAsync(5);
            ReportData = new ObservableCollection<Product>(list);
        }

        [RelayCommand]
        private async Task LoadStockHistoryReport()
        {
            if (!_licenseService.CanAccessAdvancedReports())
            {
                ReportTitle = "History is a Premium Feature. Please Upgrade.";
                return;
            }

            ReportTitle = "Stock Movement History";
            IsLowStockReport = false;
            IsHistoryReport = true;
            IsProfitReport = false;
            OnPropertyChanged(nameof(IsStockReport));
            var list = await _inventoryService.GetRecentStockMovementsAsync(100);
            StockHistoryData = new ObservableCollection<StockMovement>(list);
        }

        [RelayCommand]
        private async Task LoadMonthlyProfitReport()
        {
            if (!_licenseService.CanAccessProfitAndLoss())
            {
                ReportTitle = "Profit Reports are a Premium Feature. Please Upgrade.";
                return;
            }

            ReportTitle = "Monthly Profit & Loss Summary";
            IsLowStockReport = false;
            IsHistoryReport = false;
            IsProfitReport = true;
            OnPropertyChanged(nameof(IsStockReport));
            var list = await _inventoryService.GetMonthlyProfitSummaryAsync();
            MonthlyProfitData = new ObservableCollection<MonthlyProfitReport>(list);
        }

        [RelayCommand]
        private async Task LoadArAgingAsync()
        {
            ReportTitle = "Accounts Receivable Aging";
            var lines = await _agingReportService.GetAccountsReceivableAgingAsync();
            ArAgingLines = new ObservableCollection<AgingLine>(lines);
            ArAgingSummary = _agingReportService.Summarize(lines);
        }

        [RelayCommand]
        private async Task LoadApAgingAsync()
        {
            ReportTitle = "Accounts Payable Aging";
            var lines = await _agingReportService.GetAccountsPayableAgingAsync();
            ApAgingLines = new ObservableCollection<AgingLine>(lines);
            ApAgingSummary = _agingReportService.Summarize(lines);
        }

        [RelayCommand]
        private async Task LoadVatReturnAsync()
        {
            ReportTitle = $"VAT Return ({VatPeriodStart:yyyy-MM-dd} to {VatPeriodEnd:yyyy-MM-dd})";
            VatSummary = await _vatExportService.ComputeVatReturnAsync(VatPeriodStart, VatPeriodEnd);
        }

        [RelayCommand]
        private async Task LoadBudgetVsActualAsync()
        {
            ReportTitle = $"Budget vs Actual — FY {BudgetFiscalYear}";
            int? month = BudgetPeriodMonth is >= 1 and <= 12 ? BudgetPeriodMonth : null;
            var lines = await _budgetReportService.GetBudgetVsActualAsync(BudgetFiscalYear, month);
            BudgetVsActualLines = new ObservableCollection<BudgetVsActualLine>(lines);
            BudgetTotalBudget = lines.Sum(l => l.BudgetAmount);
            BudgetTotalActual = lines.Sum(l => l.ActualAmount);
            BudgetTotalVariance = lines.Sum(l => l.Variance);
        }

        [RelayCommand]
        private async Task LoadBankReconciliationAsync()
        {
            ReportTitle = "Bank Reconciliation";
            ReconStatusMessage = string.Empty;

            var banks = await _paymentService.GetAllBanksAsync();
            var accounts = await _paymentService.GetAllBankAccountsAsync();
            ReconBankAccounts = new ObservableCollection<BankAccountDisplayRow>(
                accounts.Select(a => new BankAccountDisplayRow
                {
                    Account = a,
                    BankName = banks.FirstOrDefault(b => b.Id == a.BankId)?.Name ?? "Unknown Bank"
                }));

            if (SelectedReconBankAccount == null && ReconBankAccounts.Count > 0)
            {
                SelectedReconBankAccount = ReconBankAccounts[0];
            }

            if (SelectedReconBankAccount == null)
            {
                ReconStatusMessage = "Add a bank account in Settings → Payments Setup first.";
                UnreconciledPayments = new ObservableCollection<ReconciliationCandidate>();
                UnreconciledStatementLines = new ObservableCollection<ReconciliationCandidate>();
                return;
            }

            var payments = await _paymentService.GetUnreconciledPaymentsAsync(SelectedReconBankAccount.Id);
            var lines = await _paymentService.GetUnreconciledStatementLinesAsync(SelectedReconBankAccount.Id);
            UnreconciledPayments = new ObservableCollection<ReconciliationCandidate>(payments);
            UnreconciledStatementLines = new ObservableCollection<ReconciliationCandidate>(lines);
            ReconStatusMessage = $"{payments.Count} unreconciled payment(s), {lines.Count} unreconciled statement line(s).";
        }

        [RelayCommand]
        private void OpenImportStatement()
        {
            if (SelectedReconBankAccount == null)
            {
                ReconStatusMessage = "Select a bank account first.";
                return;
            }

            ImportStatementDate = DateTime.Today;
            ImportOpeningBalance = 0;
            ImportClosingBalance = 0;
            ImportLineDate = DateTime.Today;
            ImportLineDescription = string.Empty;
            ImportLineAmount = 0;
            ImportLineReference = string.Empty;
            ImportErrorMessage = string.Empty;
            IsImportStatementModalOpen = true;
        }

        [RelayCommand]
        private async Task SubmitImportStatement()
        {
            ImportErrorMessage = string.Empty;
            if (SelectedReconBankAccount == null)
            {
                ImportErrorMessage = "Select a bank account first.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ImportLineDescription))
            {
                ImportErrorMessage = "Line description is required.";
                return;
            }

            if (ImportLineAmount == 0)
            {
                ImportErrorMessage = "Line amount cannot be zero.";
                return;
            }

            try
            {
                await _paymentService.ImportBankStatementAsync(
                    SelectedReconBankAccount.Id,
                    ImportStatementDate,
                    ImportOpeningBalance,
                    ImportClosingBalance,
                    new[] { (ImportLineDate, ImportLineDescription.Trim(), ImportLineAmount, ImportLineReference.Trim()) });

                IsImportStatementModalOpen = false;
                ReconStatusMessage = "Bank statement imported.";
                await LoadBankReconciliationAsync();
            }
            catch (Exception ex)
            {
                ImportErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void CloseImportStatement()
        {
            IsImportStatementModalOpen = false;
        }

        [RelayCommand]
        private async Task MatchReconciliation()
        {
            ReconStatusMessage = string.Empty;
            if (SelectedReconPayment?.Payment == null || SelectedReconLine?.StatementLine == null)
            {
                ReconStatusMessage = "Select one payment and one statement line to match.";
                return;
            }

            try
            {
                await _paymentService.MatchPaymentToStatementLineAsync(
                    SelectedReconPayment.Payment.Id,
                    SelectedReconLine.StatementLine.Id,
                    UserSession.CurrentUser?.Username ?? "System");
                ReconStatusMessage = "Payment matched to bank statement line.";
                await LoadBankReconciliationAsync();
            }
            catch (Exception ex)
            {
                ReconStatusMessage = ex.Message;
            }
        }

        [RelayCommand]
        private async Task ExportVatToCsv()
        {
            if (VatSummary == null)
            {
                await LoadVatReturnAsync();
            }

            if (VatSummary == null) return;

            if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
            {
                ReportTitle = "Error: Cannot access file system.";
                return;
            }

            var storageProvider = desktop.MainWindow.StorageProvider;
            var file = await storageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save VAT Return As",
                DefaultExtension = ".csv",
                SuggestedFileName = $"VAT_Return_{VatPeriodStart:yyyyMMdd}_{VatPeriodEnd:yyyyMMdd}",
                FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } } }
            });

            if (file == null) return;

            var csv = _vatExportService.BuildVatReturnCsv(VatSummary);
            await File.WriteAllTextAsync(file.Path.LocalPath, csv);
            ReportTitle += " (VAT Exported)";
        }

        [RelayCommand]
        private async Task ExportToCsv()
        {
            if (!_licenseService.CanAccessExport())
            {
                ReportTitle = "Export is a Premium Feature. Please Upgrade.";
                return;
            }

            if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
            {
                ReportTitle = "Error: Cannot access file system.";
                return;
            }

            var storageProvider = desktop.MainWindow.StorageProvider;
            var file = await storageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save Report As",
                DefaultExtension = ".csv",
                SuggestedFileName = $"InventoryReport_{DateTime.Now:yyyyMMdd_HHmmss}",
                FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } } }
            });

            if (file == null) return;

            // Simple Export implementation
            var sb = new StringBuilder();
            sb.AppendLine("ID,Name,SKU,Category,Stock,Unit,Price,Cost");
            foreach (var p in ReportData)
            {
                sb.AppendLine($"{p.Id},{Escape(p.Name)},{Escape(p.SKU ?? "")},{Escape(p.Category)},{p.StockQuantity},{p.Unit},{p.Price},{p.Cost}");
            }

            await File.WriteAllTextAsync(file.Path.LocalPath, sb.ToString());

            ReportTitle += " (Exported)";
        }

        private string Escape(string val)
        {
            if (val.Contains(",")) return $"\"{val}\"";
            return val;
        }

        // --- Details Modal Logic ---
        [ObservableProperty] private StockMovement? _selectedStockMovement;
        [ObservableProperty] private bool _isDetailsModalOpen;

        [RelayCommand]
        private void OpenDetails()
        {
            if (SelectedStockMovement != null)
            {
                IsDetailsModalOpen = true;
            }
        }

        [RelayCommand]
        private void CloseDetails()
        {
            IsDetailsModalOpen = false;
        }

        [RelayCommand]
        private async Task CopyToClipboard(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow : null;

            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
            }
        }

        // --- Journal Entries drill-down logic ---
        [ObservableProperty] private ObservableCollection<JournalEntryDetailRow> _detailedJournalLines = new();
        [ObservableProperty] private bool _isJournalModalOpen;
        [ObservableProperty] private string _detailedJournalTitle = string.Empty;
        [ObservableProperty] private decimal _totalDebit;
        [ObservableProperty] private decimal _totalCredit;
        [ObservableProperty] private bool _isBalanced;
        [ObservableProperty] private decimal _balanceDifference;

        [RelayCommand]
        private async Task OpenJournalEntries(ReportLineWrapper wrapper)
        {
            if (wrapper == null || !wrapper.Result.HasComputations) return;

            DetailedJournalTitle = $"Journal Entries - {wrapper.Name}";
            IsLoadingReport = true;
            try
            {
                var lines = await _accountingReportService.GetJournalLinesForReportLineAsync(wrapper.Result.LineId);
                DetailedJournalLines = new ObservableCollection<JournalEntryDetailRow>(lines);
                
                TotalDebit = lines.Sum(l => l.Debit);
                TotalCredit = lines.Sum(l => l.Credit);
                BalanceDifference = Math.Abs(TotalDebit - TotalCredit);
                IsBalanced = BalanceDifference < 0.01m;

                IsJournalModalOpen = true;
            }
            catch (Exception ex)
            {
                ReportTitle = $"Error loading journal entries: {ex.Message}";
            }
            finally
            {
                IsLoadingReport = false;
            }
        }

        [RelayCommand]
        private void CloseJournalModal()
        {
            IsJournalModalOpen = false;
        }

        private async Task LoadAbcAnalysisAsync()
        {
            ReportTitle = "ABC Analysis";
            AbcAnalysisLines.Clear();
            var lines = await _advancedAnalyticsService.GetAbcAnalysisAsync();
            foreach (var line in lines) AbcAnalysisLines.Add(line);
        }

        private async Task LoadDeadStockAsync()
        {
            ReportTitle = "Dead Stock Report (90 days)";
            DeadStockLines.Clear();
            var lines = await _advancedAnalyticsService.GetDeadStockReportAsync();
            foreach (var line in lines) DeadStockLines.Add(line);
        }

        private async Task LoadMarginByCategoryAsync()
        {
            ReportTitle = "Margin by Category";
            CategoryMarginLines.Clear();
            var lines = await _advancedAnalyticsService.GetMarginByCategoryAsync();
            foreach (var line in lines) CategoryMarginLines.Add(line);
        }

        private async Task LoadMonthCloseAsync()
        {
            var now = DateTime.Today;
            ReportTitle = $"Month Close Summary — {now:MMMM yyyy}";
            MonthCloseSummary = await _monthCloseService.GetMonthCloseSummaryAsync(now.Year, now.Month);
        }
    }

    public class ReportLineWrapper
    {
        public ReportLineResult Result { get; }
        public string CurrencySymbol { get; }
        
        public Avalonia.Thickness Margin => new Avalonia.Thickness((Result.Level - 1) * 20, 6, 10, 6);
        public bool IsHeader => Result.Level == 1;
        public bool IsSubHeader => Result.Level == 2;
        public string Name => Result.Name;
        public string FormattedBalance
        {
            get
            {
                if (!Result.HasComputations)
                {
                    return string.Empty;
                }
                if (Result.Balance < 0)
                {
                    return $"({Math.Abs(Result.Balance):N0}) {CurrencySymbol}";
                }
                return $"{Result.Balance:N0} {CurrencySymbol}";
            }
        }

        public ReportLineWrapper(ReportLineResult result, string currencySymbol)
        {
            Result = result;
            CurrencySymbol = currencySymbol;
        }
    }
}
