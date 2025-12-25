using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CsvHelper;
using CsvHelper.Configuration;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly InventoryService _inventoryService;
        private readonly LicenseService _licenseService;
        private readonly Action _navigateToInventory;
        private readonly Action _navigateToReports;
        private readonly Action _navigateToPOS;

        [ObservableProperty] private int _totalProducts;
        [ObservableProperty] private int _lowStockCount;

        // Financials
        [ObservableProperty] private decimal _totalRevenue;
        [ObservableProperty] private decimal _totalProfit;
        [ObservableProperty] private decimal _totalInventoryValue;

        [ObservableProperty]
        private ObservableCollection<StockMovement> _recentMovements = new();

        [ObservableProperty]
        private string _welcomeMessage = "Welcome to IMS";

        [ObservableProperty] private string _licenseStatusDisplay = "Checking...";
        [ObservableProperty] private string _licenseTypeDisplay = "Unknown";
        [ObservableProperty] private bool _isLicenseValid;
        [ObservableProperty] private bool _isPremium;
        [ObservableProperty] private bool _showFinancials;
        [ObservableProperty] private bool _hideFinancials;
        [ObservableProperty] private bool _isGuest;

        // Capability Flags
        [ObservableProperty] private bool _canAddProduct;
        [ObservableProperty] private bool _canStockMove;
        [ObservableProperty] private bool _canBulkImport;
        [ObservableProperty] private bool _canViewReports;

        // Modal / Quick Action logic
        [ObservableProperty] private bool _isModalOpen;
        [ObservableProperty] private string _modalTitle = string.Empty;
        [ObservableProperty] private Product _formProduct = new();
        [ObservableProperty] private int _adjQuantity;
        [ObservableProperty] private decimal _adjSellingPrice; // Custom Selling Price (Stock OUT)
        [ObservableProperty] private decimal _adjCostPrice;    // Custom Cost Price (Stock IN)
        [ObservableProperty] private string _adjReason = string.Empty;
        [ObservableProperty] private string _adjType = "IN";
        [ObservableProperty] private bool _showStockForm;
        [ObservableProperty] private bool _showProductForm;
        [ObservableProperty] private string _modalError = string.Empty;

        [ObservableProperty] private ObservableCollection<Product> _allProducts = new();
        [ObservableProperty] private Product? _selectedTargetProduct;

        // Import Logic Properties
        [ObservableProperty] private bool _isImporting;
        [ObservableProperty] private double _importProgress;
        [ObservableProperty] private bool _showImportSummary;
        [ObservableProperty] private string _importSummaryText = string.Empty;

        public List<string> AdjTypes { get; } = new() { "IN", "OUT", "ADJUST" };

        public DashboardViewModel(InventoryService inventoryService, LicenseService licenseService, Action navigateToInventory, Action navigateToReports, Action navigateToPOS)
        {
            _inventoryService = inventoryService;
            _licenseService = licenseService;
            _navigateToInventory = navigateToInventory;
            _navigateToReports = navigateToReports;
            _navigateToPOS = navigateToPOS;

            InitializeState();
            LoadDashboardDataCommand.Execute(null);
        }

        private void InitializeState()
        {
            var user = UserSession.CurrentUser;
            WelcomeMessage = $"Welcome back, {user?.Username ?? "Guest"}";
            IsGuest = user?.Role == "Guest";

            var license = _licenseService.CurrentLicense;
            IsLicenseValid = license != null && (license.Status == "Active" || license.Status == "Valid" || license.Status == "GracePeriod");

            // Premium includes Monthly and Yearly
            IsPremium = license?.Type == "Enterprise Yearly" || license?.Type == "Professional Monthly";

            LicenseTypeDisplay = license?.Type ?? "None";
            if (license != null)
            {
                if (license.Status == "Expired") LicenseStatusDisplay = $"Expired on {license.ExpirationDate:d}";
                else if (license.Status == "Locked") LicenseStatusDisplay = "Software Locked. Activation Required.";
                else if (license.Status == "HardwareMismatch") LicenseStatusDisplay = "Hardware ID Mismatch.";
                else LicenseStatusDisplay = $"Valid until {license.ExpirationDate:d}";
            }
            else
            {
                LicenseStatusDisplay = "No License Found";
            }

            // Enforce Capabilities based on Service
            CanAddProduct = IsLicenseValid;
            CanStockMove = IsLicenseValid;
            CanBulkImport = _licenseService.IsFeatureAllowed("BulkImport");
            CanViewReports = _licenseService.IsFeatureAllowed("ViewReports");
        }

        [ObservableProperty] private bool _hasMovements;
        
        public bool IsStockOut => AdjType == "OUT";
        public bool IsStockIn => AdjType == "IN";

        [RelayCommand]
        private async Task LoadDashboardData()
        {
            TotalProducts = await _inventoryService.GetTotalProductCountAsync();
            var lowStockList = await _inventoryService.GetLowStockProductsAsync(5);
            LowStockCount = lowStockList.Count;

            var financial = await _inventoryService.GetFinancialOverviewAsync();
            TotalRevenue = financial.TotalRevenue;
            TotalInventoryValue = financial.TotalInventoryValue;

            // Strict Gating: Free/Hobby users cannot see Profit
            if (IsPremium)
            {
                TotalProfit = financial.TotalProfit;
                ShowFinancials = true;
                HideFinancials = false;
            }
            else
            {
                TotalProfit = 0; // Hides the actual value
                ShowFinancials = false; // UI can use this to show "Locked"
                HideFinancials = true;
            }

            // Strict Gating: Free/Hobby users limited to top 5 movements
            int movementLimit = IsPremium ? 10 : 5;
            var movements = await _inventoryService.GetRecentStockMovementsAsync(movementLimit);
            RecentMovements = new ObservableCollection<StockMovement>(movements);
            HasMovements = RecentMovements.Count > 0;

            var products = await _inventoryService.GetAllProductsAsync();
            AllProducts = new ObservableCollection<Product>(products);
        }

        [RelayCommand]
        public void OpenAddProductModal()
        {
            ModalTitle = "Add New Product";
            FormProduct = new Product();
            ModalError = string.Empty;
            ShowProductForm = true;
            ShowStockForm = false;
            IsModalOpen = true;
        }

        [RelayCommand]
        public void OpenStockModal(string type)
        {
            ModalTitle = type == "IN" ? "Stock IN" : "Stock OUT";
            AdjType = type;
            OnPropertyChanged(nameof(IsStockOut)); // Notify UI
            OnPropertyChanged(nameof(IsStockIn)); // Notify UI
            AdjQuantity = 0;
            AdjSellingPrice = 0; // Reset
            AdjCostPrice = 0;    // Reset
            AdjReason = string.Empty;
            ModalError = string.Empty;
            ShowProductForm = false;
            ShowStockForm = true;
            IsModalOpen = true;
        }
        
        // Helper to update default price when product is selected
        partial void OnSelectedTargetProductChanged(Product? value)
        {
            if (value != null)
            {
                if (AdjType == "OUT") AdjSellingPrice = value.Price;
                if (AdjType == "IN") AdjCostPrice = value.Cost; // Pre-fill with known cost
            }
        }

        [RelayCommand]
        public void CloseModal()
        {
            IsModalOpen = false;
            ModalError = string.Empty;
        }

        [RelayCommand]
        public async Task SaveModal()
        {
            try
            {
                if (ShowProductForm)
                {
                    if (string.IsNullOrWhiteSpace(FormProduct.Name))
                    {
                        ModalError = "Product Name is required.";
                        return;
                    }
                    await _inventoryService.AddProductAsync(FormProduct);
                }
                else if (ShowStockForm)
                {
                    if (SelectedTargetProduct == null)
                    {
                        ModalError = "Please select a product.";
                        return;
                    }
                    if (AdjQuantity <= 0)
                    {
                        ModalError = "Quantity must be greater than 0.";
                        return;
                    }

                    var user = UserSession.CurrentUser?.Username ?? "Unknown";
                    
                    // Pass params based on type
                    // For IN: unitPrice is used to update the Master Price
                    // For OUT: unitPrice is the actual transaction price
                    decimal? unitPrice = AdjSellingPrice > 0 ? AdjSellingPrice : null;
                    decimal? customCost = (AdjType == "IN") ? AdjCostPrice : null;
                    
                    await _inventoryService.AddStockMovementAsync(SelectedTargetProduct.Id, AdjQuantity, AdjType, AdjReason, user, customCost: customCost, unitPrice: unitPrice);
                }

                IsModalOpen = false;
                await LoadDashboardData();
            }
            catch (Exception ex)
            {
                ModalError = ex.Message;
            }
        }

        [RelayCommand]
        public async Task RunBulkImport()
        {
            if (!CanBulkImport) return;

            // Get the storage provider from the main window
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
                return;

            var topLevel = desktop.MainWindow;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select CSV File",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } } }
            });

            if (files.Count == 0) return;

            var file = files[0];
            IsImporting = true;
            ImportProgress = 0;

            int total = 0;
            int success = 0;
            int skipped = 0;
            var errors = new List<string>();

            try
            {
                using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HeaderValidated = null,
                    MissingFieldFound = null,
                });

                var records = csv.GetRecords<dynamic>().ToList();
                total = records.Count;

                var productsToImport = new List<Product>();

                for (int i = 0; i < total; i++)
                {
                    var record = (IDictionary<string, object>)records[i];
                    try
                    {
                        // Validation
                        if (!record.ContainsKey("Name") || string.IsNullOrWhiteSpace(record["Name"]?.ToString()))
                        {
                            skipped++;
                            errors.Add($"Row {i + 1}: Missing Name");
                            continue;
                        }

                        var product = new Product
                        {
                            Name = record["Name"].ToString() ?? string.Empty,
                            SKU = record.ContainsKey("SKU") ? record["SKU"]?.ToString() : null,
                            Unit = record.ContainsKey("Unit") ? record["Unit"]?.ToString() ?? "Pcs" : "Pcs",
                            Category = record.ContainsKey("Category") ? record["Category"]?.ToString() ?? "General" : "General",
                            Price = record.ContainsKey("Price") && decimal.TryParse(record["Price"]?.ToString(), out var p) ? p : 0,
                            Cost = record.ContainsKey("Cost") && decimal.TryParse(record["Cost"]?.ToString(), out var c) ? c : 0,
                            StockQuantity = record.ContainsKey("StockQuantity") && int.TryParse(record["StockQuantity"]?.ToString(), out var s) ? s : 0
                        };

                        if (product.StockQuantity < 0)
                        {
                            skipped++;
                            errors.Add($"Row {i + 1}: Negative stock ({product.StockQuantity}) is not allowed.");
                            continue;
                        }

                        productsToImport.Add(product);
                        success++;
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        errors.Add($"Row {i + 1}: {ex.Message}");
                    }

                    ImportProgress = (double)(i + 1) / total * 100;
                    await Task.Delay(10); // Slight delay for UI feedback on progress
                }

                if (productsToImport.Any())
                {
                    var user = UserSession.CurrentUser?.Username ?? "System";
                    await _inventoryService.BulkAddProductsWithMovementsAsync(productsToImport, user, "CSV Import");
                }

                ImportSummaryText = $"Import Complete!\n\nTotal Rows: {total}\nSuccess: {success}\nSkipped: {skipped}";
                if (errors.Any())
                {
                    ImportSummaryText += "\n\nErrors:\n" + string.Join("\n", errors.Take(10));
                    if (errors.Count > 10) ImportSummaryText += "\n...and more.";
                }

                ShowImportSummary = true;
                await LoadDashboardData();
            }
            catch (Exception ex)
            {
                ImportSummaryText = $"Critical Error during import: {ex.Message}";
                ShowImportSummary = true;
            }
            finally
            {
                IsImporting = false;
            }
        }

        [RelayCommand]
        public void CloseImportSummary() => ShowImportSummary = false;

        [RelayCommand] public void GoToInventory() => _navigateToInventory?.Invoke();
        [RelayCommand] public void GoToReports() => _navigateToReports?.Invoke();
        [RelayCommand] public void GoToPOS() => _navigateToPOS?.Invoke();
    }
}
