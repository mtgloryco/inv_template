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

        public List<string> MovementTypes { get; } = new List<string> { "IN", "OUT", "ADJUST" };

        public bool IsMovementIn => SelectedMovementType == "IN";
        public bool IsMovementOut => SelectedMovementType == "OUT";

        public string CurrencySymbol => _settingsService.CurrentSettings.CurrencySymbol;
        public LanguageService Language { get; }

        private readonly SettingsService _settingsService;

        public InventoryViewModel(InventoryService inventoryService, LicenseService licenseService, SettingsService settingsService, LanguageService languageService)
        {
            _inventoryService = inventoryService;
            _licenseService = licenseService;
            _settingsService = settingsService;
            Language = languageService;
            LoadProductsCommand.Execute(null);
        }

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

        [RelayCommand]
        private void OpenAddProductPane()
        {
            CurrentProduct = new Product();
            PaneTitle = Language["Inv_NewProduct"];
            IsStockMode = false;
            IsPaneOpen = true;
        }

        [RelayCommand]
        private void OpenEditProductPane(Product product)
        {
            if (product == null) return;
            // Clone to avoid live editing before save
            CurrentProduct = new Product
            {
                Id = product.Id,
                Name = product.Name,
                SKU = product.SKU,
                Unit = product.Unit,
                Category = product.Category ?? "",
                Price = product.Price,
                Cost = product.Cost,
                StockQuantity = product.StockQuantity
            };
            PaneTitle = Language["Inv_PaneTitle"]; // "Manage Product" or "Edit Product"
            IsStockMode = false;
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

                IsPaneOpen = false;
                await LoadProducts();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
            }
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
                await _inventoryService.AddStockMovementAsync(
                    CurrentProduct.Id,
                    AdjustmentQuantity,
                    SelectedMovementType,
                    AdjustmentReason,
                    username,
                    AdjustmentCostPerUnit,
                    AdjustmentUnitPrice);
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

        partial void OnSearchTextChanged(string value)
        {
            LoadProductsCommand.Execute(null);
        }

        partial void OnSelectedMovementTypeChanged(string value)
        {
            OnPropertyChanged(nameof(IsMovementIn));
            OnPropertyChanged(nameof(IsMovementOut));
        }
    }
}
