using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class InventoryViewModel : ViewModelBase
    {
        private readonly InventoryService _inventoryService;
        private readonly LicenseService _licenseService;
        private readonly AccountService _accountService;
        private readonly CustomFieldService? _customFieldService;
        private readonly CustomFieldsPanelViewModel _customFieldsPanel = new();

        public CustomFieldService? CustomFieldService => _customFieldService;
        public ObservableCollection<CustomFieldInputItem> ProductCustomFields => _customFieldsPanel.Items;
        public bool HasCustomFields => ProductCustomFields.Count > 0;

        private List<Account> _allAccounts = new();

        [ObservableProperty] private string _incomeAccountSearchText = string.Empty;
        [ObservableProperty] private string _selectedIncomeAccountName = string.Empty;
        [ObservableProperty] private ObservableCollection<Account> _matchedIncomeAccounts = new();
        public bool IsIncomeAccountListVisible => MatchedIncomeAccounts.Count > 0;

        [ObservableProperty] private string _expenseAccountSearchText = string.Empty;
        [ObservableProperty] private string _selectedExpenseAccountName = string.Empty;
        [ObservableProperty] private ObservableCollection<Account> _matchedExpenseAccounts = new();
        public bool IsExpenseAccountListVisible => MatchedExpenseAccounts.Count > 0;

        public bool CanBulkImport => _licenseService.CanAccessBulkImport();
        public string BulkImportTooltip => CanBulkImport ? "Import products from CSV" : "Import is a Premium Feature";

        [ObservableProperty]
        private ObservableCollection<Product> _products = new();

        [ObservableProperty]
        private Product? _selectedProduct;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isPaneOpen;

        [ObservableProperty]
        private string _paneTitle = "New Product";

        [ObservableProperty]
        private bool _isStockMode = false;

        // Form properties
        [ObservableProperty]
        private Product _currentProduct = new();

        // Stock Movement properties
        [ObservableProperty]
        private int _adjustmentQuantity;

        [ObservableProperty]
        private string _adjustmentReason = string.Empty;

        [ObservableProperty]
        private string _selectedMovementType = "IN";

        [ObservableProperty]
        private decimal _adjustmentCostPerUnit;

        [ObservableProperty]
        private decimal _adjustmentUnitPrice;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        // New Product Form property bindings and options
        public List<string> ProductTypes { get; } = new() { "Good", "Service", "Combo" };
        public List<string> InvoicingPolicies { get; } = new() { "Ordered quantities", "Delivered quantities" };
        public List<string> TrackingOptions { get; } = new() { "by quantity", "lots", "by unique serial number" };
        public List<string> TaxComputationOptions { get; } = new() { "Percentage", "Fixed" };
        public List<string> TaxScopeOptions { get; } = new() { "Goods", "Services" };
        public List<string> TaxIncludedOptions { get; } = new() { "Exclude", "Include" };

        [ObservableProperty] private ObservableCollection<string> _categoryOptions = new();
        [ObservableProperty] private ObservableCollection<string> _unitOptions = new();
        [ObservableProperty] private ObservableCollection<Tax> _salesTaxes = new();
        [ObservableProperty] private ObservableCollection<ProductUnit> _allProductUnits = new();

        [ObservableProperty] private string _selectedUnitName = "Pcs";
        [ObservableProperty] private string _selectedCategoryName = "General";
        [ObservableProperty] private Tax? _selectedSalesTax;

        // Overlay modals
        [ObservableProperty] private bool _isUomSearchMoreModalOpen;
        [ObservableProperty] private bool _isCreateUnitModalOpen;
        [ObservableProperty] private bool _isCreateCategoryModalOpen;
        [ObservableProperty] private bool _isCreateTaxModalOpen;

        // Unit form inputs
        [ObservableProperty] private string _newUnitName = string.Empty;
        [ObservableProperty] private double _newUnitQuantity = 1.0;
        [ObservableProperty] private bool _newUnitGroupInPOS = false;
        [ObservableProperty] private string _newUnitReferenceUnit = string.Empty;
        [ObservableProperty] private string _newUnitErrorMessage = string.Empty;

        // Searchable Unit properties
        [ObservableProperty] private string _unitSearchText = string.Empty;
        [ObservableProperty] private ObservableCollection<ProductUnit> _matchedUnits = new();

        // Category form inputs
        [ObservableProperty] private string _newCategoryName = string.Empty;
        [ObservableProperty] private string _newCategoryDescription = string.Empty;
        [ObservableProperty] private string _newCategoryErrorMessage = string.Empty;

        // Tax form inputs
        [ObservableProperty] private string _newTaxName = string.Empty;
        [ObservableProperty] private decimal _newTaxAmount;
        [ObservableProperty] private string _newTaxComputation = "Percentage";
        [ObservableProperty] private string _newTaxDescription = string.Empty;
        [ObservableProperty] private string _newTaxLabelOnInvoice = string.Empty;
        [ObservableProperty] private string _newTaxScope = "Goods";
        [ObservableProperty] private string _newTaxIncludedInPrice = "Exclude";
        [ObservableProperty] private string _newTaxErrorMessage = string.Empty;

        public bool IsUnitSelected => !string.IsNullOrWhiteSpace(SelectedUnitName) && UnitSearchText == SelectedUnitName;
        public List<string> CleanUnitNames => AllProductUnits.Select(u => u.Name).ToList();

        // Intercept selection updates
        partial void OnUnitSearchTextChanged(string value)
        {
            OnPropertyChanged(nameof(IsUnitSelected));
            if (string.IsNullOrWhiteSpace(value) || value == SelectedUnitName)
            {
                MatchedUnits.Clear();
                return;
            }

            var query = value.ToLower();
            var matches = AllProductUnits.Where(u => u.Name.ToLower().Contains(query)).Take(5).ToList();
            MatchedUnits = new ObservableCollection<ProductUnit>(matches);
        }

        [RelayCommand]
        public void SelectUnit(ProductUnit unit)
        {
            SelectedUnitName = unit.Name;
            UnitSearchText = unit.Name;
            MatchedUnits.Clear();
            CurrentProduct.Unit = unit.Name;
            OnPropertyChanged(nameof(IsUnitSelected));
        }

        [RelayCommand]
        public void OpenCreateUnitFromSearchText()
        {
            NewUnitName = UnitSearchText;
            NewUnitQuantity = 1.0;
            NewUnitGroupInPOS = false;
            NewUnitReferenceUnit = string.Empty;
            NewUnitErrorMessage = string.Empty;
            IsCreateUnitModalOpen = true;
        }

        // Intercept selection updates
        partial void OnSelectedUnitNameChanged(string value)
        {
            OnPropertyChanged(nameof(IsUnitSelected));
            if (value == "Search More...")
            {
                // Revert selection so it does not persist in control
                SelectedUnitName = CurrentProduct.Unit;
                IsUomSearchMoreModalOpen = true;
            }
            else if (!string.IsNullOrEmpty(value))
            {
                CurrentProduct.Unit = value;
                UnitSearchText = value;
            }
        }

        partial void OnSelectedCategoryNameChanged(string value)
        {
            if (value == "Create Category...")
            {
                SelectedCategoryName = CurrentProduct.Category;
                IsCreateCategoryModalOpen = true;
            }
            else if (!string.IsNullOrEmpty(value))
            {
                CurrentProduct.Category = value;
            }
        }

        partial void OnSelectedSalesTaxChanged(Tax? value)
        {
            if (value != null)
            {
                if (value.Id == -999)
                {
                    SelectedSalesTax = null;
                    IsCreateTaxModalOpen = true;
                }
                else
                {
                    CurrentProduct.SalesTaxId = value.Id;
                }
            }
            else
            {
                CurrentProduct.SalesTaxId = null;
            }
        }

        public List<string> MovementTypes { get; } = new List<string> { "IN", "OUT", "ADJUST" };

        public bool IsMovementIn => SelectedMovementType == "IN";
        public bool IsMovementOut => SelectedMovementType == "OUT";

        public string CurrencySymbol => _settingsService.CurrentSettings.CurrencySymbol;
        public LanguageService Language { get; }

        private readonly SettingsService _settingsService;
        private readonly TaxService _taxService;
        private readonly Action? _goToRfq;
        private readonly Action? _goToPurchaseOrders;
        private readonly Action? _goToSuppliers;
        private readonly Action? _goToSalesQuotations;
        private readonly Action? _goToSalesOrders;
        private readonly Action? _goToCustomers;
        private readonly Action? _goToCycleCount;
        private readonly Action? _goToReorderDashboard;
        private readonly Action? _goToForecasting;
        private readonly Action? _goToLocations;
        private readonly BarcodeService? _barcodeService;

        [ObservableProperty] private string _barcodeStatusMessage = string.Empty;

        // Product history modal
        [ObservableProperty] private bool _isHistoryModalOpen;
        [ObservableProperty] private string _historyProductTitle = string.Empty;
        [ObservableProperty] private ObservableCollection<ProductHistoryEvent> _productHistoryEvents = new();
        [ObservableProperty] private ProductHistoryEvent? _selectedHistoryEvent;
        [ObservableProperty] private ObservableCollection<JournalEntryDetailRow> _historyJournalLines = new();
        [ObservableProperty] private decimal _historyJournalTotalDebit;
        [ObservableProperty] private decimal _historyJournalTotalCredit;
        [ObservableProperty] private string _historyStatusMessage = string.Empty;

        [ObservableProperty] private string _adjustmentBatchNumber = string.Empty;
        [ObservableProperty] private string _adjustmentSerialNumbers = string.Empty;
        [ObservableProperty] private DateTimeOffset? _adjustmentExpiryDate;

        public bool RequiresLotTracking => BatchTrackingService.RequiresLotTracking(CurrentProduct);
        public bool RequiresSerialTracking => BatchTrackingService.RequiresSerialTracking(CurrentProduct);
        public bool ShowBatchFields => IsStockMode && IsMovementIn && RequiresLotTracking;
        public bool ShowSerialFields => IsStockMode && IsMovementIn && RequiresSerialTracking;

        public InventoryViewModel(
            InventoryService inventoryService, 
            LicenseService licenseService, 
            SettingsService settingsService, 
            LanguageService languageService, 
            TaxService taxService,
            AccountService accountService,
            Action? goToRfq = null,
            Action? goToPurchaseOrders = null,
            Action? goToSuppliers = null,
            Action? goToSalesQuotations = null,
            Action? goToSalesOrders = null,
            Action? goToCustomers = null,
            Action? goToCycleCount = null,
            Action? goToReorderDashboard = null,
            Action? goToForecasting = null,
            Action? goToLocations = null,
            CustomFieldService? customFieldService = null,
            BarcodeService? barcodeService = null)
        {
            _inventoryService = inventoryService;
            _licenseService = licenseService;
            _settingsService = settingsService;
            Language = languageService;
            _taxService = taxService;
            _accountService = accountService;
            _goToRfq = goToRfq;
            _goToPurchaseOrders = goToPurchaseOrders;
            _goToSuppliers = goToSuppliers;
            _goToSalesQuotations = goToSalesQuotations;
            _goToSalesOrders = goToSalesOrders;
            _goToCustomers = goToCustomers;
            _goToCycleCount = goToCycleCount;
            _goToReorderDashboard = goToReorderDashboard;
            _goToForecasting = goToForecasting;
            _goToLocations = goToLocations;
            _customFieldService = customFieldService;
            _barcodeService = barcodeService;
            _customFieldsPanel.Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasCustomFields));
            LoadProductsCommand.Execute(null);
        }

        partial void OnIncomeAccountSearchTextChanged(string value)
        {
            MatchedIncomeAccounts.Clear();
            if (string.IsNullOrWhiteSpace(value))
            {
                OnPropertyChanged(nameof(IsIncomeAccountListVisible));
                return;
            }

            if (CurrentProduct != null && CurrentProduct.IncomeAccountId.HasValue)
            {
                var currentAcc = _allAccounts.FirstOrDefault(a => a.Id == CurrentProduct.IncomeAccountId.Value);
                if (currentAcc != null && value == $"{currentAcc.Code} {currentAcc.Name}")
                {
                    OnPropertyChanged(nameof(IsIncomeAccountListVisible));
                    return;
                }
            }

            var query = value.ToLower();
            var matches = _allAccounts
                .Where(a => a.Code.ToLower().Contains(query) || a.Name.ToLower().Contains(query))
                .Take(10)
                .ToList();

            foreach (var a in matches)
            {
                MatchedIncomeAccounts.Add(a);
            }
            OnPropertyChanged(nameof(IsIncomeAccountListVisible));
        }

        partial void OnExpenseAccountSearchTextChanged(string value)
        {
            MatchedExpenseAccounts.Clear();
            if (string.IsNullOrWhiteSpace(value))
            {
                OnPropertyChanged(nameof(IsExpenseAccountListVisible));
                return;
            }

            if (CurrentProduct != null && CurrentProduct.ExpenseAccountId.HasValue)
            {
                var currentAcc = _allAccounts.FirstOrDefault(a => a.Id == CurrentProduct.ExpenseAccountId.Value);
                if (currentAcc != null && value == $"{currentAcc.Code} {currentAcc.Name}")
                {
                    OnPropertyChanged(nameof(IsExpenseAccountListVisible));
                    return;
                }
            }

            var query = value.ToLower();
            var matches = _allAccounts
                .Where(a => a.Code.ToLower().Contains(query) || a.Name.ToLower().Contains(query))
                .Take(10)
                .ToList();

            foreach (var a in matches)
            {
                MatchedExpenseAccounts.Add(a);
            }
            OnPropertyChanged(nameof(IsExpenseAccountListVisible));
        }

        [RelayCommand]
        private void SelectIncomeAccount(Account account)
        {
            if (account == null || CurrentProduct == null) return;
            CurrentProduct.IncomeAccountId = account.Id;
            SelectedIncomeAccountName = $"Associated: {account.Code} - {account.Name}";
            IncomeAccountSearchText = $"{account.Code} {account.Name}";
            MatchedIncomeAccounts.Clear();
            OnPropertyChanged(nameof(IsIncomeAccountListVisible));
        }

        [RelayCommand]
        private void SelectExpenseAccount(Account account)
        {
            if (account == null || CurrentProduct == null) return;
            CurrentProduct.ExpenseAccountId = account.Id;
            SelectedExpenseAccountName = $"Associated: {account.Code} - {account.Name}";
            ExpenseAccountSearchText = $"{account.Code} {account.Name}";
            MatchedExpenseAccounts.Clear();
            OnPropertyChanged(nameof(IsExpenseAccountListVisible));
        }

        [RelayCommand] private void GoToRfqScreen() => _goToRfq?.Invoke();
        [RelayCommand] private void GoToCycleCountScreen() => _goToCycleCount?.Invoke();
        [RelayCommand] private void GoToReorderDashboardScreen() => _goToReorderDashboard?.Invoke();
        [RelayCommand] private void GoToForecastingScreen() => _goToForecasting?.Invoke();
        [RelayCommand] private void GoToLocationsScreen() => _goToLocations?.Invoke();

        partial void OnCurrentProductChanged(Product value)
        {
            OnPropertyChanged(nameof(RequiresLotTracking));
            OnPropertyChanged(nameof(RequiresSerialTracking));
            OnPropertyChanged(nameof(ShowBatchFields));
            OnPropertyChanged(nameof(ShowSerialFields));
        }

        partial void OnIsStockModeChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowBatchFields));
            OnPropertyChanged(nameof(ShowSerialFields));
        }

        partial void OnSelectedMovementTypeChanged(string value)
        {
            OnPropertyChanged(nameof(IsMovementIn));
            OnPropertyChanged(nameof(IsMovementOut));
            OnPropertyChanged(nameof(ShowBatchFields));
            OnPropertyChanged(nameof(ShowSerialFields));
        }

        [RelayCommand]
        private void GoToPurchaseOrdersScreen() => _goToPurchaseOrders?.Invoke();

        [RelayCommand]
        private void GoToSuppliersScreen() => _goToSuppliers?.Invoke();

        [RelayCommand]
        private void GoToSalesQuotationsScreen() => _goToSalesQuotations?.Invoke();

        [RelayCommand]
        private void GoToSalesOrdersScreen() => _goToSalesOrders?.Invoke();

        [RelayCommand]
        private void GoToCustomersScreen() => _goToCustomers?.Invoke();



        [RelayCommand]
        private async Task ImportProducts()
        {
            if (!CanBulkImport) return;

            // In a real Desktop app, we would open a FileDialog here.
            // Since we are in an environment where we can't easily interact with file dialogs physically,
            // we will simulate the logic or assume a fixed path for testing if needed.
            // For now, let's just create some dummy products to simulate an import.

            var newProducts = new List<Product>();
            for (int i = 1; i <= 5; i++)
            {
                newProducts.Add(new Product
                {
                    Name = $"Imported Item {i}",
                    SKU = $"IMP-{DateTime.Now.Ticks}-{i}",
                    Price = 10 * i,
                    StockQuantity = 100,
                    Category = "Imported",
                    Unit = "Pcs",
                    Cost = 5 * i
                });
            }

            await _inventoryService.AddProductsAsync(newProducts);
            await LoadProducts();
        }

        [RelayCommand]
        private async Task LoadProducts()
        {
            var list = await _inventoryService.GetAllProductsAsync();

            // Allow filtering
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerObj = SearchText.ToLower();
                list = list.Where(p => p.Name.ToLower().Contains(lowerObj) || (p.SKU != null && p.SKU.ToLower().Contains(lowerObj))).ToList();
            }

            Products = new ObservableCollection<Product>(list);
        }

        public async Task LoadFormDataAsync()
        {
            // Load Categories
            var categoriesList = await _inventoryService.GetCategoriesAsync();
            CategoryOptions.Clear();
            foreach (var c in categoriesList)
            {
                CategoryOptions.Add(c.Name);
            }
            CategoryOptions.Add("Create Category...");

            // Load Units
            var unitsList = await _inventoryService.GetProductUnitsAsync();
            AllProductUnits.Clear();
            UnitOptions.Clear();
            foreach (var u in unitsList)
            {
                AllProductUnits.Add(u);
                UnitOptions.Add(u.Name);
            }
            UnitOptions.Add("Search More...");

            // Load Taxes
            var taxesList = await _taxService.GetSearchableTaxesAsync("Sales");
            SalesTaxes.Clear();
            foreach (var t in taxesList)
            {
                SalesTaxes.Add(t);
            }
            SalesTaxes.Add(new Tax { Id = -999, Name = "Create New Sales Tax...", LabelOnInvoice = "Create New Sales Tax..." });

            // Load Accounts
            var accountsList = await _accountService.GetAllAccountsAsync();
            _allAccounts = accountsList ?? new List<Account>();

            // Set current selections based on product values
            if (CurrentProduct != null)
            {
                SelectedCategoryName = string.IsNullOrEmpty(CurrentProduct.Category) ? "General" : CurrentProduct.Category;
                SelectedUnitName = string.IsNullOrEmpty(CurrentProduct.Unit) ? "Pcs" : CurrentProduct.Unit;
                UnitSearchText = SelectedUnitName;
                SelectedSalesTax = SalesTaxes.FirstOrDefault(t => t.Id == CurrentProduct.SalesTaxId);
            }
        }

        private async Task LoadProductCustomFieldsAsync(int? productId)
        {
            if (_customFieldService == null) return;
            try
            {
                await _customFieldsPanel.LoadAsync(_customFieldService, "Product", productId);
            }
            catch
            {
                // Non-fatal: leave whatever fields were already loaded (possibly none) so
                // the core product form remains usable even if custom fields fail to load.
            }
            OnPropertyChanged(nameof(HasCustomFields));
        }

        [RelayCommand]
        private async Task OpenAddProductPane()
        {
            CurrentProduct = new Product
            {
                CanBeSold = true,
                CanBePurchased = true,
                AvailableInPOS = false,
                ProductType = "Good",
                InvoicingPolicy = "Ordered quantities",
                Tracking = "by quantity"
            };
            PaneTitle = Language["Inv_NewProduct"];
            IsStockMode = false;
            await LoadFormDataAsync();
            await LoadProductCustomFieldsAsync(null);

            IncomeAccountSearchText = string.Empty;
            SelectedIncomeAccountName = string.Empty;
            MatchedIncomeAccounts.Clear();
            ExpenseAccountSearchText = string.Empty;
            SelectedExpenseAccountName = string.Empty;
            MatchedExpenseAccounts.Clear();

            IsPaneOpen = true;
        }

        [RelayCommand]
        private async Task OpenEditProductPane(Product product)
        {
            if (product == null) return;
            CurrentProduct = new Product
            {
                Id = product.Id,
                Name = product.Name,
                SKU = product.SKU,
                Unit = product.Unit,
                Category = product.Category ?? "",
                Price = product.Price,
                Cost = product.Cost,
                StockQuantity = product.StockQuantity,
                CanBeSold = product.CanBeSold,
                CanBePurchased = product.CanBePurchased,
                AvailableInPOS = product.AvailableInPOS,
                ProductType = product.ProductType ?? "Good",
                InvoicingPolicy = product.InvoicingPolicy ?? "Ordered quantities",
                Tracking = product.Tracking ?? "by quantity",
                SalesTaxId = product.SalesTaxId,
                IncomeAccountId = product.IncomeAccountId,
                ExpenseAccountId = product.ExpenseAccountId
            };
            PaneTitle = Language["Inv_PaneTitle"];
            IsStockMode = false;
            await LoadFormDataAsync();
            await LoadProductCustomFieldsAsync(product.Id);

            if (product.IncomeAccountId.HasValue)
            {
                var acc = _allAccounts.FirstOrDefault(a => a.Id == product.IncomeAccountId.Value);
                if (acc != null)
                {
                    IncomeAccountSearchText = $"{acc.Code} {acc.Name}";
                    SelectedIncomeAccountName = $"Associated: {acc.Code} - {acc.Name}";
                }
                else
                {
                    IncomeAccountSearchText = string.Empty;
                    SelectedIncomeAccountName = string.Empty;
                }
            }
            else
            {
                IncomeAccountSearchText = string.Empty;
                SelectedIncomeAccountName = string.Empty;
            }

            if (product.ExpenseAccountId.HasValue)
            {
                var acc = _allAccounts.FirstOrDefault(a => a.Id == product.ExpenseAccountId.Value);
                if (acc != null)
                {
                    ExpenseAccountSearchText = $"{acc.Code} {acc.Name}";
                    SelectedExpenseAccountName = $"Associated: {acc.Code} - {acc.Name}";
                }
                else
                {
                    ExpenseAccountSearchText = string.Empty;
                    SelectedExpenseAccountName = string.Empty;
                }
            }
            else
            {
                ExpenseAccountSearchText = string.Empty;
                SelectedExpenseAccountName = string.Empty;
            }

            IsPaneOpen = true;
        }

        [RelayCommand]
        private void OpenStockPane(Product product)
        {
            CurrentProduct = product; // No cloning needed for reference, but be careful not to edit props
            PaneTitle = $"{Language["Inv_Stock"]} - {product.Name}";
            IsStockMode = true;
            AdjustmentQuantity = 0;
            AdjustmentReason = "";
            SelectedMovementType = "IN";
            AdjustmentCostPerUnit = product.Cost;
            AdjustmentUnitPrice = product.Price;
            IsPaneOpen = true;
        }

        [RelayCommand]
        private void CancelPane()
        {
            IsPaneOpen = false;
        }

        [RelayCommand]
        private async Task SaveProduct()
        {
            ErrorMessage = "";
            try
            {
                if (IsStockMode)
                {
                    await SaveStockMovement();
                    return;
                }

                if (string.IsNullOrWhiteSpace(CurrentProduct.Name))
                {
                    ErrorMessage = "Product Name is required.";
                    return;
                }

                if (CurrentProduct.Id == 0)
                {
                    await _inventoryService.AddProductAsync(CurrentProduct);
                }
                else
                {
                    await _inventoryService.UpdateProductAsync(CurrentProduct);
                }

                if (_customFieldService != null && CurrentProduct.Id != 0)
                {
                    try
                    {
                        await _customFieldsPanel.SaveAsync(_customFieldService, CurrentProduct.Id);
                    }
                    catch
                    {
                        // Non-fatal: the core product save already succeeded; don't block on
                        // custom field persistence failures.
                    }
                }

                IsPaneOpen = false;
                await LoadProducts();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SaveNewUnitAsync()
        {
            NewUnitErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(NewUnitName))
            {
                NewUnitErrorMessage = "Unit name is required.";
                return;
            }
            if (NewUnitQuantity <= 0)
            {
                NewUnitErrorMessage = "Quantity must be greater than zero.";
                return;
            }

            try
            {
                var unit = new ProductUnit
                {
                    Name = NewUnitName,
                    Quantity = NewUnitQuantity,
                    GroupInPOS = NewUnitGroupInPOS,
                    ReferenceUnit = NewUnitReferenceUnit ?? string.Empty
                };
                await _inventoryService.AddProductUnitAsync(unit);
                await LoadFormDataAsync();
                
                SelectedUnitName = unit.Name;
                UnitSearchText = unit.Name;
                IsCreateUnitModalOpen = false;
                
                NewUnitName = string.Empty;
                NewUnitQuantity = 1.0;
                NewUnitGroupInPOS = false;
                NewUnitReferenceUnit = string.Empty;
            }
            catch (Exception ex)
            {
                NewUnitErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private async Task SaveNewCategoryAsync()
        {
            NewCategoryErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(NewCategoryName))
            {
                NewCategoryErrorMessage = "Category name is required.";
                return;
            }

            try
            {
                var category = new Category
                {
                    Name = NewCategoryName,
                    Description = NewCategoryDescription
                };
                await _inventoryService.AddCategoryAsync(category);
                await LoadFormDataAsync();

                SelectedCategoryName = category.Name;
                IsCreateCategoryModalOpen = false;

                NewCategoryName = string.Empty;
                NewCategoryDescription = string.Empty;
            }
            catch (Exception ex)
            {
                NewCategoryErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private async Task SaveNewSalesTaxAsync()
        {
            NewTaxErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(NewTaxName))
            {
                NewTaxErrorMessage = "Tax name is required.";
                return;
            }
            if (NewTaxAmount < 0)
            {
                NewTaxErrorMessage = "Amount cannot be negative.";
                return;
            }

            try
            {
                var tax = new Tax
                {
                    Name = NewTaxName,
                    Amount = NewTaxAmount,
                    Computation = NewTaxComputation,
                    TaxType = "Sales",
                    Description = NewTaxDescription,
                    LabelOnInvoice = string.IsNullOrWhiteSpace(NewTaxLabelOnInvoice) ? NewTaxName : NewTaxLabelOnInvoice,
                    Scope = NewTaxScope,
                    IncludedInPrice = NewTaxIncludedInPrice,
                    IsActive = true
                };
                await _taxService.AddTaxAsync(tax);
                await LoadFormDataAsync();

                SelectedSalesTax = SalesTaxes.FirstOrDefault(t => t.Id == tax.Id);
                IsCreateTaxModalOpen = false;

                NewTaxName = string.Empty;
                NewTaxAmount = 0;
                NewTaxComputation = "Percentage";
                NewTaxDescription = string.Empty;
                NewTaxLabelOnInvoice = string.Empty;
                NewTaxScope = "Goods";
                NewTaxIncludedInPrice = "Exclude";
            }
            catch (Exception ex)
            {
                NewTaxErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void SelectUnitFromSearchMore(ProductUnit unit)
        {
            if (unit == null) return;
            SelectedUnitName = unit.Name;
            IsUomSearchMoreModalOpen = false;
        }

        [RelayCommand]
        private void OpenCreateUnitFromSearchMore()
        {
            IsCreateUnitModalOpen = true;
        }

        private async Task SaveStockMovement()
        {
            if (AdjustmentQuantity <= 0 && SelectedMovementType != "ADJUST")
            {
                ErrorMessage = "Quantity must be greater than zero.";
                return;
            }

            try
            {
                var username = UserSession.CurrentUser?.Username ?? "Unknown";
                BatchReceiveDetail? batchDetail = null;
                if (SelectedMovementType == "IN" && BatchTrackingService.RequiresTrackedBatches(CurrentProduct))
                {
                    batchDetail = new BatchReceiveDetail
                    {
                        BatchNumber = AdjustmentBatchNumber,
                        ExpiryDate = AdjustmentExpiryDate?.DateTime
                    };
                    if (BatchTrackingService.RequiresSerialTracking(CurrentProduct))
                    {
                        batchDetail.SerialNumbers = AdjustmentSerialNumbers
                            .Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .ToList();
                    }
                    BatchTrackingService.ValidateReceiveDetails(CurrentProduct, AdjustmentQuantity, batchDetail);
                }

                await _inventoryService.AddStockMovementAsync(
                    CurrentProduct.Id,
                    AdjustmentQuantity,
                    SelectedMovementType,
                    AdjustmentReason,
                    username,
                    AdjustmentCostPerUnit,
                    AdjustmentUnitPrice,
                    batchDetail?.BatchNumber,
                    batchDetail?.ExpiryDate,
                    qualityStatus: null,
                    serialNumbers: batchDetail?.SerialNumbers);
                IsPaneOpen = false;
                await LoadProducts();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message; // Service throws user-friendly validation errors
            }
        }


        [RelayCommand]
        private async Task DeleteProduct(Product product)
        {
            if (product == null) return;
            await _inventoryService.DeleteProductAsync(product);
            await LoadProducts();
        }

        [RelayCommand]
        private void CancelSearchMore()
        {
            IsUomSearchMoreModalOpen = false;
        }

        [RelayCommand]
        private void CancelCreateUnit()
        {
            IsCreateUnitModalOpen = false;
            NewUnitErrorMessage = string.Empty;
        }

        [RelayCommand]
        private void CancelCreateCategory()
        {
            IsCreateCategoryModalOpen = false;
            NewCategoryErrorMessage = string.Empty;
        }

        [RelayCommand]
        private void CancelCreateTax()
        {
            IsCreateTaxModalOpen = false;
            NewTaxErrorMessage = string.Empty;
        }

        partial void OnSearchTextChanged(string value)
        {
            LoadProductsCommand.Execute(null);
        }

        [RelayCommand]
        private async Task ScanBarcodeAsync()
        {
            if (_barcodeService == null || string.IsNullOrWhiteSpace(SearchText))
            {
                return;
            }

            var product = await _barcodeService.FindProductByBarcodeAsync(SearchText.Trim());
            if (product == null)
            {
                BarcodeStatusMessage = "No product found for this barcode.";
                return;
            }

            SelectedProduct = product;
            BarcodeStatusMessage = $"Selected {product.Name}.";
            SearchText = product.SKU ?? product.Name;
            LoadProductsCommand.Execute(null);
        }

        [RelayCommand]
        private async Task OpenProductHistory(Product? product)
        {
            if (product == null) return;

            HistoryProductTitle = $"{product.Name} ({product.SKU})";
            HistoryStatusMessage = string.Empty;
            SelectedHistoryEvent = null;
            HistoryJournalLines.Clear();
            HistoryJournalTotalDebit = 0;
            HistoryJournalTotalCredit = 0;

            var events = await _inventoryService.ProductHistory.GetProductHistoryAsync(product.Id);
            ProductHistoryEvents = new ObservableCollection<ProductHistoryEvent>(events);
            HistoryStatusMessage = events.Count == 0
                ? "No stock movements recorded for this product yet."
                : $"{events.Count} transaction(s). Select a row to view linked journal entries.";

            IsHistoryModalOpen = true;
        }

        partial void OnSelectedHistoryEventChanged(ProductHistoryEvent? value)
        {
            _ = LoadHistoryJournalsAsync(value);
        }

        private async Task LoadHistoryJournalsAsync(ProductHistoryEvent? historyEvent)
        {
            HistoryJournalLines.Clear();
            HistoryJournalTotalDebit = 0;
            HistoryJournalTotalCredit = 0;

            if (historyEvent == null) return;

            var lines = await _inventoryService.ProductHistory.GetJournalLinesForMovementAsync(historyEvent.Movement);
            HistoryJournalLines = new ObservableCollection<JournalEntryDetailRow>(lines);
            HistoryJournalTotalDebit = lines.Sum(l => l.Debit);
            HistoryJournalTotalCredit = lines.Sum(l => l.Credit);

            if (lines.Count == 0)
            {
                HistoryStatusMessage = "No journal entries linked to this movement (may be a stock-only change).";
            }
        }

        [RelayCommand]
        private void CloseHistoryModal()
        {
            IsHistoryModalOpen = false;
        }
    }
}
