using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class ReportsViewModel : ViewModelBase
    {
        private readonly InventoryService _inventoryService;
        private readonly LicenseService _licenseService;

        [ObservableProperty] private ObservableCollection<Product> _reportData = new();
        [ObservableProperty] private ObservableCollection<StockMovement> _stockHistoryData = new();
        [ObservableProperty] private ObservableCollection<MonthlyProfitReport> _monthlyProfitData = new();
        [ObservableProperty] private string _reportTitle = "Current Stock Report";
        [ObservableProperty] private bool _isLowStockReport;
        [ObservableProperty] private bool _isHistoryReport;
        [ObservableProperty] private bool _isProfitReport;

        public bool IsStockReport => !IsHistoryReport && !IsProfitReport;

        public string CurrencySymbol => _settingsService.CurrentSettings.CurrencySymbol;
        public LanguageService Language { get; }

        private readonly SettingsService _settingsService;

        public ReportsViewModel(InventoryService inventoryService, LicenseService licenseService, SettingsService settingsService, LanguageService languageService)
        {
            _inventoryService = inventoryService;
            _licenseService = licenseService;
            _settingsService = settingsService;
            Language = languageService;
            LoadStockReportCommand.Execute(null);
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
    }
}
