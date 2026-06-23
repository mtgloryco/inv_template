using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class CartItem : ObservableObject
    {
        [ObservableProperty] private Product _product;
        [ObservableProperty] private int _quantity;
        [ObservableProperty] private decimal _unitPrice; // Selling Price

        private readonly int _maxStock;

        public decimal Subtotal => Quantity * UnitPrice;

        public CartItem(Product product, int quantity, decimal unitPrice)
        {
            _product = product;
            _quantity = quantity;
            _unitPrice = unitPrice;
            _maxStock = product.StockQuantity;
        }

        public void Increment()
        {
            if (Quantity < _maxStock)
            {
                Quantity++;
                OnPropertyChanged(nameof(Subtotal));
            }
        }

        public void Decrement()
        {
            if (Quantity > 1)
            {
                Quantity--;
                OnPropertyChanged(nameof(Subtotal));
            }
        }
    }

    public partial class POSViewModel : ViewModelBase
    {
        private readonly InventoryService _inventoryService;
        private readonly LicenseService _licenseService;
        private readonly ReceiptService _receiptService;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<CartItem> _cartItems = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private decimal _amountPaid;
        [ObservableProperty] private decimal _changeDue;
        
        [ObservableProperty] private bool _isReceiptModalOpen;
        [ObservableProperty] private string _lastReceiptPath = string.Empty;
        [ObservableProperty] private string _lastReceiptText = string.Empty; // Keep for fallback/display

        public string CurrencySymbol => _settingsService.CurrentSettings.CurrencySymbol;
        public LanguageService Language { get; }

        private readonly SettingsService _settingsService;

        public POSViewModel(InventoryService inventoryService, LicenseService licenseService, ReceiptService receiptService, SettingsService settingsService, LanguageService languageService)
        {
            _inventoryService = inventoryService;
            _licenseService = licenseService; // Future pro features
            _receiptService = receiptService;
            _settingsService = settingsService;
            Language = languageService;
            LoadProductsCommand.Execute(null);
        }

        async partial void OnSearchTextChanged(string value)
        {
            await LoadProducts();
        }

        [RelayCommand]
        private async Task LoadProducts()
        {
            var all = await _inventoryService.GetAllProductsAsync();
            var posProducts = all.Where(p => p.AvailableInPOS && p.CanBeSold).ToList();
            
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                AvailableProducts = new ObservableCollection<Product>(posProducts);
            }
            else
            {
                var filtered = posProducts.Where(p => 
                    p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                    (p.SKU != null && p.SKU.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                ).ToList();
                AvailableProducts = new ObservableCollection<Product>(filtered);
            }
        }

        [RelayCommand]
        private void AddToCart(Product product)
        {
            if (product.StockQuantity <= 0) return; // Prevent adding if out of stock

            var existing = CartItems.FirstOrDefault(c => c.Product.Id == product.Id);
            if (existing != null)
            {
                if (existing.Quantity < product.StockQuantity)
                {
                    existing.Increment();
                }
            }
            else
            {
                // Default selling price could be Cost * Margin, or just Price property if we had one for sales.
                // For now, let's assume Product.Price is the default Selling Price.
                CartItems.Add(new CartItem(product, 1, product.Price));
            }
            RecalculateTotal();
        }

        [RelayCommand]
        private void RemoveFromCart(CartItem item)
        {
            CartItems.Remove(item);
            RecalculateTotal();
        }

        private void RecalculateTotal()
        {
            TotalAmount = CartItems.Sum(x => x.Subtotal);
            CalculateChange();
        }

        partial void OnAmountPaidChanged(decimal value)
        {
            CalculateChange();
        }

        private void CalculateChange()
        {
            ChangeDue = AmountPaid - TotalAmount;
        }

        [RelayCommand]
        private async Task Checkout()
        {
            if (CartItems.Count == 0) return;

            try
            {
                var user = UserSession.CurrentUser?.Username ?? "Cashier";

                // Generate Receipt PDF BEFORE clearing cart
                LastReceiptPath = _receiptService.GenerateReceiptFromCart(user, CartItems, TotalAmount, AmountPaid, ChangeDue);
                LastReceiptText = $"Receipt Generated Successfully!\nSaved to: {LastReceiptPath}";

                // Process each item as a Stock OUT movement
                foreach (var item in CartItems)
                {
                    await _inventoryService.AddStockMovementAsync(
                        item.Product.Id, 
                        item.Quantity, 
                        "OUT", 
                        "POS Sale", 
                        user, 
                        customCost: null, 
                        unitPrice: item.UnitPrice // Capture the selling price at checkout
                    );
                }
                
                // Clear Cart
                CartItems.Clear();
                RecalculateTotal();
                AmountPaid = 0;
                
                // Show Receipt Modal
                IsReceiptModalOpen = true;
                
                // Refresh Inventory List in background if needed (LoadProducts will handle it next time)
                await LoadProducts(); 
            }
            catch (Exception ex)
            {
                LastReceiptText = $"Error during checkout: {ex.Message}";
                IsReceiptModalOpen = true;
            }
        }

        [RelayCommand]
        private void CloseReceipt()
        {
            IsReceiptModalOpen = false;
        }

        [RelayCommand]
        private void PrintReceipt()
        {
             // Open the generated PDF
             if (!string.IsNullOrEmpty(LastReceiptPath) && System.IO.File.Exists(LastReceiptPath))
             {
                 try
                 {
                     new System.Diagnostics.Process
                     {
                         StartInfo = new System.Diagnostics.ProcessStartInfo(LastReceiptPath)
                         {
                             UseShellExecute = true
                         }
                     }.Start();
                 }
                 catch (Exception ex)
                 {
                     LastReceiptText += $"\nCould not open PDF automatically: {ex.Message}";
                 }
             }
        }
    }
}
