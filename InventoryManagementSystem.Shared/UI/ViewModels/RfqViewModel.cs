using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class RfqViewModel : ViewModelBase
    {
        private readonly PurchaseOrderService _purchaseOrderService;
        private readonly SupplierService _supplierService;
        private readonly InventoryService _inventoryService;
        private readonly TaxService _taxService;
        private readonly SettingsService _settingsService;
        private readonly PurchaseOrderPdfService _pdfService;

        public LanguageService Language { get; }

        [ObservableProperty] private ObservableCollection<PurchaseOrderListItem> _rfqs = new();
        [ObservableProperty] private PurchaseOrderListItem? _selectedRfq;
        [ObservableProperty] private string _searchText = string.Empty;

        // Form fields
        [ObservableProperty] private bool _isFormOpen;
        [ObservableProperty] private bool _isNew;
        [ObservableProperty] private PurchaseOrder _currentRfq = new();
        [ObservableProperty] private string _vendorSearchText = string.Empty;
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private decimal _rfqTotalAmount;
        [ObservableProperty] private decimal _rfqSubtotalAmount;
        [ObservableProperty] private string _rfqTaxBreakdownText = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;

        public List<string> Currencies { get; } = new() { "USD", "EUR", "GBP", "KES", "RWF", "UGX" };
        public List<string> PaymentTermsOptions { get; } = new() { "Immediate Payment", "15 days", "21 days", "30 days" };
        
        [ObservableProperty] private ObservableCollection<Supplier> _matchedSuppliers = new();
        [ObservableProperty] private bool _isCreateSupplierButtonVisible;

        [ObservableProperty] private ObservableCollection<RfqItemRow> _rfqItems = new();
        [ObservableProperty] private ObservableCollection<Tax> _purchaseTaxes = new();

        public List<Supplier> AllSuppliers { get; private set; } = new();
        public List<Product> AllProducts { get; private set; } = new();

        // Sub-creation overlays
        [ObservableProperty] private bool _isCreateSupplierModalOpen;
        [ObservableProperty] private string _newSupplierName = string.Empty;
        [ObservableProperty] private string _newSupplierPhone = string.Empty;
        [ObservableProperty] private string _newSupplierEmail = string.Empty;
        [ObservableProperty] private string _newSupplierErrorMessage = string.Empty;

        [ObservableProperty] private bool _isCreateProductModalOpen;
        [ObservableProperty] private string _newProductName = string.Empty;
        [ObservableProperty] private decimal _newProductCost;
        [ObservableProperty] private decimal _newProductPrice;
        [ObservableProperty] private string _newProductErrorMessage = string.Empty;
        private int _triggerRowIndex = -1;

        public RfqViewModel(
            PurchaseOrderService purchaseOrderService,
            SupplierService supplierService,
            InventoryService inventoryService,
            TaxService taxService,
            SettingsService settingsService,
            LanguageService languageService)
        {
            _purchaseOrderService = purchaseOrderService;
            _supplierService = supplierService;
            _inventoryService = inventoryService;
            _taxService = taxService;
            _settingsService = settingsService;
            Language = languageService;
            _pdfService = new PurchaseOrderPdfService(_settingsService);

            LoadRfqsCommand.Execute(null);
        }

        [RelayCommand]
        public async Task LoadRfqs()
        {
            var list = await _purchaseOrderService.GetAllPurchaseOrdersAsync();
            
            // Filter to only display Draft (RFQ) or orders matching search text
            var filtered = list.Where(po => po.PurchaseOrder.Status == "Draft" || po.PurchaseOrder.Status == "Pending");
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var query = SearchText.ToLower();
                filtered = filtered.Where(po => 
                    po.PurchaseOrder.PONumber.ToLower().Contains(query) || 
                    po.SupplierName.ToLower().Contains(query)
                );
            }
            Rfqs = new ObservableCollection<PurchaseOrderListItem>(filtered);
        }

        public async Task LoadFormDataAsync()
        {
            AllSuppliers = await _supplierService.GetAllSuppliersAsync();
            AllProducts = await _inventoryService.GetAllProductsAsync();
            
            var taxes = await _taxService.GetSearchableTaxesAsync("Purchases");
            PurchaseTaxes.Clear();
            foreach (var t in taxes)
            {
                PurchaseTaxes.Add(t);
            }

            MatchedSuppliers.Clear();
            foreach (var s in AllSuppliers.Take(5))
            {
                MatchedSuppliers.Add(s);
            }
        }

        [RelayCommand]
        private async Task OpenAddRfq()
        {
            CurrentRfq = new PurchaseOrder
            {
                Status = "Draft",
                OrderDate = DateTime.Today,
                OrderDeadline = DateTime.Today.AddDays(7),
                ExpectedDeliveryDate = DateTime.Today.AddDays(14),
                Currency = "USD",
                PaymentTerms = "Immediate Payment",
                Company = "My Company",
                Buyer = UserSession.CurrentUser?.Username ?? "System",
                CreatedByUsername = UserSession.CurrentUser?.Username ?? "System"
            };

            VendorSearchText = string.Empty;
            SelectedSupplier = null;
            RfqItems.Clear();
            ErrorMessage = string.Empty;
            IsNew = true;

            await LoadFormDataAsync();
            AddItemRow(); // Start with one row
            IsFormOpen = true;
        }

        [RelayCommand]
        private async Task OpenEditRfq(PurchaseOrderListItem? item)
        {
            if (item == null) return;
            var po = item.PurchaseOrder;

            CurrentRfq = new PurchaseOrder
            {
                Id = po.Id,
                PONumber = po.PONumber,
                SupplierId = po.SupplierId,
                Status = po.Status,
                OrderDate = po.OrderDate,
                ExpectedDeliveryDate = po.ExpectedDeliveryDate,
                Notes = po.Notes,
                CreatedByUsername = po.CreatedByUsername,
                ApprovedByUsername = po.ApprovedByUsername,
                Currency = po.Currency,
                PaymentTerms = po.PaymentTerms,
                OrderDeadline = po.OrderDeadline,
                Buyer = po.Buyer,
                Company = po.Company
            };

            await LoadFormDataAsync();
            SelectedSupplier = AllSuppliers.FirstOrDefault(s => s.Id == po.SupplierId);
            VendorSearchText = SelectedSupplier?.Name ?? string.Empty;

            var itemsList = await _purchaseOrderService.GetItemsAsync(po.Id);
            RfqItems.Clear();
            foreach (var it in itemsList)
            {
                var row = new RfqItemRow(AllProducts)
                {
                    SelectedProduct = AllProducts.FirstOrDefault(p => p.Id == it.ProductId),
                    Quantity = it.QuantityOrdered,
                    UnitCost = it.UnitCost,
                    SelectedTax = PurchaseTaxes.FirstOrDefault(t => t.Id == it.TaxId)
                };
                row.OnChanged = UpdateRfqTotal;
                if (row.SelectedProduct != null)
                {
                    row.ProductSearchText = row.SelectedProduct.Name;
                }
                
                row.PropertyChanged += (s, e) => {
                    if (e.PropertyName == nameof(RfqItemRow.TotalCost))
                    {
                        UpdateRfqTotal();
                    }
                };
                RfqItems.Add(row);
            }

            IsNew = false;
            UpdateRfqTotal();
            IsFormOpen = true;
        }

        [RelayCommand]
        private void AddItemRow()
        {
            var row = new RfqItemRow(AllProducts);
            row.OnChanged = UpdateRfqTotal;
            row.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(RfqItemRow.TotalCost))
                {
                    UpdateRfqTotal();
                }
            };
            row.RequestCreateProduct = () => OpenCreateProductFromRow(RfqItems.IndexOf(row));
            RfqItems.Add(row);
            UpdateRfqTotal();
        }

        [RelayCommand]
        private void RemoveItemRow(RfqItemRow row)
        {
            if (row != null)
            {
                RfqItems.Remove(row);
                UpdateRfqTotal();
            }
        }

        private void UpdateRfqTotal()
        {
            RfqTotalAmount = RfqItems.Sum(item => item.TotalCost);
            RfqSubtotalAmount = RfqItems.Sum(item => item.BaseCost);
            
            // Calculate tax breakdown
            var taxBreakdown = RfqItems
                .Where(item => item.SelectedTax != null && item.TaxAmount > 0)
                .GroupBy(item => item.SelectedTax!)
                .Select(g => new { 
                    Tax = g.Key, 
                    Amount = g.Sum(item => item.TaxAmount) 
                })
                .ToList();
                
            if (taxBreakdown.Count > 0)
            {
                var inclusiveText = taxBreakdown.Any(t => t.Tax.IncludedInPrice == "Include") ? " (Tax Inclusive)" : "";
                var lines = taxBreakdown.Select(tb => $"{tb.Tax.Name} ({tb.Tax.Amount}%): {tb.Amount:N2}");
                RfqTaxBreakdownText = string.Join("\n", lines) + inclusiveText;
            }
            else
            {
                RfqTaxBreakdownText = "No Taxes";
            }
        }

        [RelayCommand]
        private async Task SaveRfq()
        {
            ErrorMessage = string.Empty;
            if (SelectedSupplier == null)
            {
                ErrorMessage = "Vendor/Supplier is required.";
                return;
            }
            if (string.IsNullOrWhiteSpace(CurrentRfq.Currency))
            {
                ErrorMessage = "Currency is required.";
                return;
            }
            if (CurrentRfq.OrderDeadline == null)
            {
                ErrorMessage = "Order Deadline is required.";
                return;
            }
            if (!RfqItems.Any() || RfqItems.Any(i => i.SelectedProduct == null))
            {
                ErrorMessage = "At least one valid product must be added.";
                return;
            }

            try
            {
                var items = RfqItems.Select(row => new PurchaseOrderItem
                {
                    ProductId = row.SelectedProduct!.Id,
                    QuantityOrdered = row.Quantity,
                    UnitCost = row.UnitCost,
                    TaxId = row.SelectedTax?.Id
                }).ToList();

                if (IsNew)
                {
                    await _purchaseOrderService.CreateRfqAsync(CurrentRfq, items);
                }
                else
                {
                    await _purchaseOrderService.UpdatePurchaseOrderAsync(CurrentRfq, items);
                }

                IsFormOpen = false;
                await LoadRfqs();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private async Task ConfirmAsPurchaseOrder()
        {
            if (CurrentRfq.Id == 0)
            {
                ErrorMessage = "Please save the RFQ as draft first before confirming.";
                return;
            }

            try
            {
                await _purchaseOrderService.ConvertRfqToPoAsync(CurrentRfq.Id, UserSession.CurrentUser?.Username ?? "System");
                IsFormOpen = false;
                await LoadRfqs();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void CancelForm()
        {
            IsFormOpen = false;
        }

        // Search supplier as user types
        partial void OnVendorSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || (SelectedSupplier != null && value == SelectedSupplier.Name))
            {
                MatchedSuppliers = new ObservableCollection<Supplier>(AllSuppliers.Take(5));
                IsCreateSupplierButtonVisible = false;
                return;
            }

            var query = value.ToLower();
            var matches = AllSuppliers.Where(s => s.Name.ToLower().Contains(query)).Take(5).ToList();
            MatchedSuppliers = new ObservableCollection<Supplier>(matches);
            IsCreateSupplierButtonVisible = matches.Count == 0;
        }

        [RelayCommand]
        private void SelectSupplier(Supplier supplier)
        {
            if (supplier == null) return;
            SelectedSupplier = supplier;
            VendorSearchText = supplier.Name;
            CurrentRfq.SupplierId = supplier.Id;
            MatchedSuppliers.Clear();
            IsCreateSupplierButtonVisible = false;
        }

        // Sub-creation overlay helpers: Create Supplier
        [RelayCommand]
        private void OpenCreateSupplier()
        {
            NewSupplierName = VendorSearchText;
            NewSupplierPhone = string.Empty;
            NewSupplierEmail = string.Empty;
            NewSupplierErrorMessage = string.Empty;
            IsCreateSupplierModalOpen = true;
        }

        [RelayCommand]
        private async Task SaveNewSupplier()
        {
            NewSupplierErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(NewSupplierName))
            {
                NewSupplierErrorMessage = "Name is required.";
                return;
            }

            try
            {
                var s = new Supplier
                {
                    Name = NewSupplierName,
                    Phone = NewSupplierPhone,
                    Email = NewSupplierEmail
                };
                await _supplierService.AddSupplierAsync(s);
                
                // Refresh suppliers list
                AllSuppliers = await _supplierService.GetAllSuppliersAsync();
                SelectSupplier(s);
                IsCreateSupplierModalOpen = false;
            }
            catch (Exception ex)
            {
                NewSupplierErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void CancelCreateSupplier()
        {
            IsCreateSupplierModalOpen = false;
        }

        // Sub-creation overlay helpers: Create Product
        private void OpenCreateProductFromRow(int rowIndex)
        {
            _triggerRowIndex = rowIndex;
            NewProductName = RfqItems[rowIndex].ProductSearchText;
            NewProductCost = 0;
            NewProductPrice = 0;
            NewProductErrorMessage = string.Empty;
            IsCreateProductModalOpen = true;
        }

        [RelayCommand]
        private async Task SaveNewProduct()
        {
            NewProductErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(NewProductName))
            {
                NewProductErrorMessage = "Product name is required.";
                return;
            }

            try
            {
                var p = new Product
                {
                    Name = NewProductName,
                    Cost = NewProductCost,
                    Price = NewProductPrice,
                    SKU = $"AUTO-{DateTime.Now.Ticks % 100000}",
                    Unit = "Pcs",
                    Category = "General",
                    CanBeSold = true,
                    CanBePurchased = true
                };
                await _inventoryService.AddProductAsync(p);

                // Refresh product list
                AllProducts = await _inventoryService.GetAllProductsAsync();

                if (_triggerRowIndex >= 0 && _triggerRowIndex < RfqItems.Count)
                {
                    RfqItems[_triggerRowIndex].ProductSearchText = p.Name;
                    RfqItems[_triggerRowIndex].SelectedProduct = p;
                }

                IsCreateProductModalOpen = false;
            }
            catch (Exception ex)
            {
                NewProductErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void CancelCreateProduct()
        {
            IsCreateProductModalOpen = false;
        }

        partial void OnSearchTextChanged(string value)
        {
            LoadRfqsCommand.Execute(null);
        }

        [RelayCommand]
        private async Task PrintRfq(PurchaseOrderListItem? item)
        {
            if (item == null) return;
            var po = item.PurchaseOrder;

            try
            {
                var itemsList = await _purchaseOrderService.GetItemsAsync(po.Id);
                var supplier = await _supplierService.GetSupplierByIdAsync(po.SupplierId);
                
                var products = await _inventoryService.GetAllProductsAsync();
                var taxes = await _taxService.GetAllTaxesAsync();

                var path = _pdfService.GeneratePurchaseOrderPdf(po, itemsList, products, taxes, supplier);
                
                if (File.Exists(path))
                {
                    new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo(path)
                        {
                            UseShellExecute = true
                        }
                    }.Start();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to generate or open PDF: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task PreviewCurrentRfqPdf()
        {
            if (SelectedSupplier == null)
            {
                ErrorMessage = "Please select a Supplier/Vendor first to generate a preview.";
                return;
            }

            try
            {
                var tempItems = RfqItems
                    .Where(row => row.SelectedProduct != null)
                    .Select(row => new PurchaseOrderItem
                    {
                        ProductId = row.SelectedProduct!.Id,
                        QuantityOrdered = row.Quantity,
                        UnitCost = row.UnitCost,
                        TaxId = row.SelectedTax?.Id
                    }).ToList();

                if (!tempItems.Any())
                {
                    ErrorMessage = "Please add at least one product to preview.";
                    return;
                }

                var products = await _inventoryService.GetAllProductsAsync();
                var taxes = await _taxService.GetAllTaxesAsync();

                var tempPo = new PurchaseOrder
                {
                    PONumber = string.IsNullOrWhiteSpace(CurrentRfq.PONumber) ? "RFQ-DRAFT" : CurrentRfq.PONumber,
                    SupplierId = SelectedSupplier.Id,
                    Status = CurrentRfq.Status ?? "Draft",
                    OrderDate = CurrentRfq.OrderDate,
                    OrderDeadline = CurrentRfq.OrderDeadline,
                    ExpectedDeliveryDate = CurrentRfq.ExpectedDeliveryDate,
                    Currency = CurrentRfq.Currency,
                    PaymentTerms = CurrentRfq.PaymentTerms,
                    Notes = CurrentRfq.Notes,
                    Company = CurrentRfq.Company,
                    Buyer = CurrentRfq.Buyer
                };

                var path = _pdfService.GeneratePurchaseOrderPdf(tempPo, tempItems, products, taxes, SelectedSupplier);

                if (File.Exists(path))
                {
                    new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo(path)
                        {
                            UseShellExecute = true
                        }
                    }.Start();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to preview PDF: {ex.Message}";
            }
        }
    }

    public partial class RfqItemRow : ViewModelBase
    {
        private string _productSearchText = string.Empty;
        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                SetProperty(ref _productSearchText, value);
                FilterProducts();
            }
        }

        private ObservableCollection<Product> _matchedProducts = new();
        public ObservableCollection<Product> MatchedProducts
        {
            get => _matchedProducts;
            set => SetProperty(ref _matchedProducts, value);
        }

        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                SetProperty(ref _selectedProduct, value);
                if (value != null)
                {
                    UnitCost = value.Cost;
                    ProductSearchText = value.Name;
                }
                OnChanged?.Invoke();
            }
        }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                SetProperty(ref _quantity, value);
                OnPropertyChanged(nameof(TotalCost));
                OnChanged?.Invoke();
            }
        }

        private decimal _unitCost;
        public decimal UnitCost
        {
            get => _unitCost;
            set
            {
                SetProperty(ref _unitCost, value);
                OnPropertyChanged(nameof(TotalCost));
                OnChanged?.Invoke();
            }
        }

        private Tax? _selectedTax;
        public Tax? SelectedTax
        {
            get => _selectedTax;
            set
            {
                SetProperty(ref _selectedTax, value);
                OnPropertyChanged(nameof(TotalCost));
                OnChanged?.Invoke();
            }
        }

        public decimal BaseCost
        {
            get
            {
                var subtotal = Quantity * UnitCost;
                if (SelectedTax != null && SelectedTax.IncludedInPrice == "Include")
                {
                    if (SelectedTax.Computation == "Percentage")
                    {
                        return subtotal / (1 + (SelectedTax.Amount / 100));
                    }
                    else
                    {
                        return Math.Max(0, subtotal - (Quantity * SelectedTax.Amount));
                    }
                }
                return subtotal;
            }
        }

        public decimal TaxAmount
        {
            get
            {
                if (SelectedTax == null) return 0;
                var baseCost = BaseCost;
                if (SelectedTax.IncludedInPrice == "Include")
                {
                    return (Quantity * UnitCost) - baseCost;
                }
                else
                {
                    if (SelectedTax.Computation == "Percentage")
                    {
                        return baseCost * (SelectedTax.Amount / 100);
                    }
                    else
                    {
                        return Quantity * SelectedTax.Amount;
                    }
                }
            }
        }

        public decimal TotalCost
        {
            get
            {
                if (SelectedTax != null && SelectedTax.IncludedInPrice == "Include")
                {
                    return Quantity * UnitCost;
                }
                return BaseCost + TaxAmount;
            }
        }

        private readonly List<Product> _allProducts;
        public Action? RequestCreateProduct { get; set; }
        public Action? OnChanged { get; set; }

        public RfqItemRow(List<Product> allProducts)
        {
            _allProducts = allProducts;
            MatchedProducts = new ObservableCollection<Product>(_allProducts.Take(5));
        }

        private void FilterProducts()
        {
            if (string.IsNullOrWhiteSpace(ProductSearchText) || (SelectedProduct != null && ProductSearchText == SelectedProduct.Name))
            {
                MatchedProducts = new ObservableCollection<Product>(_allProducts.Take(5));
                return;
            }
            var query = ProductSearchText.ToLower();
            var matches = _allProducts.Where(p => p.Name.ToLower().Contains(query)).Take(5).ToList();
            MatchedProducts = new ObservableCollection<Product>(matches);
        }

        [RelayCommand]
        private void SelectProduct(Product product)
        {
            SelectedProduct = product;
        }
    }
}
