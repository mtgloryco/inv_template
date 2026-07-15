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
    public partial class SalesViewModel : ViewModelBase
    {
        private readonly SalesOrderService _salesOrderService;
        private readonly CustomerService _customerService;
        private readonly InventoryService _inventoryService;
        private readonly TaxService _taxService;
        private readonly SettingsService _settingsService;
        private readonly SalesOrderPdfService _pdfService;
        private readonly ReturnsService _returnsService;
        private readonly PaymentService _paymentService;
        private readonly CurrencyService _currencyService;

        public LanguageService Language { get; }
        public string BaseCurrency => _settingsService.CurrentSettings.CurrencySymbol ?? "RWF";

        [ObservableProperty] private int _selectedTabIndex; // 0 = Quotations, 1 = Sales Orders

        public bool IsQuotationsTabSelected => SelectedTabIndex == 0;
        public bool IsSalesOrdersTabSelected => SelectedTabIndex == 1;

        partial void OnSelectedTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsQuotationsTabSelected));
            OnPropertyChanged(nameof(IsSalesOrdersTabSelected));
        }
        [ObservableProperty] private ObservableCollection<SalesOrderListItem> _quotations = new();
        [ObservableProperty] private ObservableCollection<SalesOrderListItem> _salesOrders = new();
        [ObservableProperty] private SalesOrderListItem? _selectedOrder;
        [ObservableProperty] private string _searchText = string.Empty;

        // Form fields
        [ObservableProperty] private bool _isFormOpen;
        [ObservableProperty] private bool _isNew;
        [ObservableProperty] private SalesOrder _currentOrder = new();
        [ObservableProperty] private string _customerSearchText = string.Empty;
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private decimal _orderTotalAmount;
        [ObservableProperty] private decimal _orderSubtotalAmount;
        [ObservableProperty] private string _orderTaxBreakdownText = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _isTaxInclusive;

        partial void OnIsTaxInclusiveChanged(bool value)
        {
            if (CurrentOrder != null)
            {
                CurrentOrder.IsTaxInclusive = value;
                foreach (var r in OrderItems)
                {
                    r.IsTaxInclusive = value;
                }
                UpdateTotals();
            }
        }

        // Payment Terms Search / Creation
        [ObservableProperty] private string _paymentTermsSearchText = string.Empty;
        [ObservableProperty] private ObservableCollection<PaymentTerm> _matchedPaymentTerms = new();
        [ObservableProperty] private bool _isCreatePaymentTermButtonVisible;
        [ObservableProperty] private bool _isPaymentTermsListVisible;

        // Customer Search / Creation
        [ObservableProperty] private ObservableCollection<Customer> _matchedCustomers = new();
        [ObservableProperty] private bool _isCreateCustomerButtonVisible;
        [ObservableProperty] private bool _isCustomerListVisible;

        // Sub-creation overlay flags
        [ObservableProperty] private bool _isCreateCustomerModalOpen;
        [ObservableProperty] private string _newCustomerName = string.Empty;
        [ObservableProperty] private string _newCustomerPhone = string.Empty;
        [ObservableProperty] private string _newCustomerEmail = string.Empty;
        [ObservableProperty] private string _newCustomerAddress = string.Empty;
        [ObservableProperty] private string _newCustomerErrorMessage = string.Empty;

        [ObservableProperty] private bool _isCreatePaymentTermModalOpen;
        [ObservableProperty] private string _newPaymentTermName = string.Empty;
        [ObservableProperty] private string _newPaymentTermDescription = string.Empty;
        [ObservableProperty] private string _newPaymentTermErrorMessage = string.Empty;

        // Delivery Modal
        [ObservableProperty] private bool _isDeliveryModalOpen;
        [ObservableProperty] private ObservableCollection<SalesOrderDeliveryRow> _deliveryRows = new();
        [ObservableProperty] private string _deliveryErrorMessage = string.Empty;

        // Return Modal
        [ObservableProperty] private bool _isReturnModalOpen;
        [ObservableProperty] private ObservableCollection<SalesOrderReturnRow> _returnRows = new();
        [ObservableProperty] private string _returnErrorMessage = string.Empty;

        [ObservableProperty] private ObservableCollection<SalesItemRow> _orderItems = new();
        [ObservableProperty] private ObservableCollection<Tax> _salesTaxes = new();

        // Details Overlay Properties
        [ObservableProperty] private bool _isDetailsOpen;
        [ObservableProperty] private SalesOrder? _detailedSo;
        [ObservableProperty] private Customer? _detailedCustomer;
        [ObservableProperty] private ObservableCollection<SalesOrderItemDisplayRow> _detailedItems = new();
        [ObservableProperty] private decimal _detailedSubtotal;
        [ObservableProperty] private decimal _detailedTotal;
        [ObservableProperty] private string _detailedTaxBreakdownText = string.Empty;
        [ObservableProperty] private string _detailedBaseCurrencyEquivalent = string.Empty;

        [ObservableProperty] private decimal _detailedOpenBalance;
        [ObservableProperty] private decimal _detailedAmountPaid;
        [ObservableProperty] private ObservableCollection<InvoicePayment> _detailedPayments = new();
        [ObservableProperty] private bool _isPaymentModalOpen;
        [ObservableProperty] private decimal _paymentAmount;
        [ObservableProperty] private string _paymentMethod = "Bank";
        [ObservableProperty] private string _paymentReference = string.Empty;
        [ObservableProperty] private string _paymentErrorMessage = string.Empty;

        public List<string> PaymentMethods { get; } = new() { "Bank", "Cash", "Mobile Money" };
        public bool CanRecordPayment => IsDetailedSoInvoiced && DetailedOpenBalance > 0.01m;

        public bool DetailedSoIsArchived => DetailedSo?.IsArchived ?? false;
        public bool CanCancelDetailedSo => DetailedSo != null && DetailedSo.Status != "Cancelled" && DetailedSo.Status != "Delivered";
        public string ArchiveDetailedSoButtonText => DetailedSoIsArchived ? "Unarchive Order" : "Archive Order";
        public bool CanDeliverDetailedSo => DetailedSo != null && DetailedSo.DeliveryStatus != "Delivered" && DetailedSo.Status != "Cancelled" && DetailedSo.Status != "Draft";
        public bool CanReturnDetailedSo => DetailedSo != null && DetailedSo.DeliveryStatus != "Pending" && DetailedSo.Status != "Cancelled";
        public bool CanInvoiceDetailedSo => DetailedSo != null && DetailedSo.BillingStatus != "Invoiced" && DetailedSo.Status != "Cancelled" && DetailedSo.Status != "Draft";
        public bool IsDetailedSoInvoiced => DetailedSo?.BillingStatus == "Invoiced";

        public List<Customer> AllCustomers { get; private set; } = new();
        public List<Product> AllProducts { get; private set; } = new();
        public List<PaymentTerm> AllPaymentTerms { get; private set; } = new();

        public List<string> Currencies { get; } = new() { "USD", "EUR", "GBP", "KES", "RWF", "UGX" };

        public SalesViewModel(
            SalesOrderService salesOrderService,
            CustomerService customerService,
            InventoryService inventoryService,
            TaxService taxService,
            SettingsService settingsService,
            ReturnsService returnsService,
            PaymentService paymentService,
            CurrencyService currencyService,
            LanguageService languageService,
            int initialTab = 0)
        {
            _salesOrderService = salesOrderService;
            _customerService = customerService;
            _inventoryService = inventoryService;
            _taxService = taxService;
            _settingsService = settingsService;
            _returnsService = returnsService;
            _paymentService = paymentService;
            _currencyService = currencyService;
            Language = languageService;
            _pdfService = new SalesOrderPdfService(_settingsService);
            SelectedTabIndex = initialTab;

            LoadSalesDataCommand.Execute(null);
        }

        [RelayCommand]
        public async Task LoadSalesData()
        {
            var list = await _salesOrderService.GetAllSalesOrdersAsync();
            
            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var query = SearchText.ToLower();
                list = list.Where(o => 
                    o.SalesOrder.SONumber.ToLower().Contains(query) || 
                    o.CustomerName.ToLower().Contains(query)
                ).ToList();
            }

            Quotations = new ObservableCollection<SalesOrderListItem>(list.Where(o => o.SalesOrder.Status == "Draft"));
            SalesOrders = new ObservableCollection<SalesOrderListItem>(list.Where(o => o.SalesOrder.Status != "Draft"));
        }

        public async Task LoadFormDataAsync()
        {
            AllCustomers = await _customerService.GetAllCustomersAsync();
            AllProducts = await _inventoryService.GetAllProductsAsync();
            AllPaymentTerms = await _salesOrderService.GetAllPaymentTermsAsync();
            
            var taxes = await _taxService.GetSearchableTaxesAsync("Sales");
            SalesTaxes.Clear();
            foreach (var t in taxes)
            {
                SalesTaxes.Add(t);
            }

            MatchedCustomers.Clear();
            foreach (var c in AllCustomers.Take(5))
            {
                MatchedCustomers.Add(c);
            }

            MatchedPaymentTerms.Clear();
            foreach (var t in AllPaymentTerms.Take(5))
            {
                MatchedPaymentTerms.Add(t);
            }
        }

        [RelayCommand]
        private async Task OpenAddSale()
        {
            CurrentOrder = new SalesOrder
            {
                Status = "Draft",
                OrderDate = DateTime.Today,
                QuotationDate = DateTime.Today,
                ExpirationDate = DateTime.Today.AddDays(7),
                DeliveryDate = DateTime.Today.AddDays(14),
                Currency = "RWF",
                PaymentTerms = "Immediate Payment",
                Company = "My Company",
                CreatedByUsername = UserSession.CurrentUser?.Username ?? "System"
            };

            CustomerSearchText = string.Empty;
            PaymentTermsSearchText = "Immediate Payment";
            SelectedCustomer = null;
            OrderItems.Clear();
            ErrorMessage = string.Empty;
            IsTaxInclusive = false;
            IsNew = true;

            await LoadFormDataAsync();
            AddItemRow(); // Start with one row
            IsFormOpen = true;
        }

        [RelayCommand]
        private async Task OpenEditSale(SalesOrderListItem? item)
        {
            if (item == null) return;
            IsNew = false;
            ErrorMessage = string.Empty;
            
            await LoadFormDataAsync();

            CurrentOrder = new SalesOrder
            {
                Id = item.SalesOrder.Id,
                SONumber = item.SalesOrder.SONumber,
                CustomerId = item.SalesOrder.CustomerId,
                Status = item.SalesOrder.Status,
                OrderDate = item.SalesOrder.OrderDate,
                QuotationDate = item.SalesOrder.QuotationDate,
                ExpirationDate = item.SalesOrder.ExpirationDate,
                DeliveryDate = item.SalesOrder.DeliveryDate,
                PaymentTerms = item.SalesOrder.PaymentTerms,
                Notes = item.SalesOrder.Notes,
                CreatedByUsername = item.SalesOrder.CreatedByUsername,
                TotalAmount = item.SalesOrder.TotalAmount,
                IsTaxInclusive = item.SalesOrder.IsTaxInclusive,
                BillingStatus = item.SalesOrder.BillingStatus,
                DeliveryStatus = item.SalesOrder.DeliveryStatus,
                IsArchived = item.SalesOrder.IsArchived,
                Company = item.SalesOrder.Company,
                Currency = item.SalesOrder.Currency
            };

            SelectedCustomer = AllCustomers.FirstOrDefault(c => c.Id == CurrentOrder.CustomerId);
            CustomerSearchText = SelectedCustomer?.Name ?? string.Empty;
            PaymentTermsSearchText = CurrentOrder.PaymentTerms;
            IsTaxInclusive = CurrentOrder.IsTaxInclusive;

            // Load Items
            var items = await _salesOrderService.GetItemsAsync(CurrentOrder.Id);
            OrderItems.Clear();
            foreach (var it in items)
            {
                var row = new SalesItemRow(AllProducts)
                {
                    Quantity = it.QuantityOrdered,
                    UnitPrice = it.UnitPrice,
                    IsTaxInclusive = CurrentOrder.IsTaxInclusive,
                    SelectedProduct = AllProducts.FirstOrDefault(p => p.Id == it.ProductId),
                    SelectedTax = SalesTaxes.FirstOrDefault(t => t.Id == it.TaxId)
                };
                row.OnChanged = UpdateTotals;
                OrderItems.Add(row);
            }

            UpdateTotals();
            IsFormOpen = true;
        }

        [RelayCommand]
        private void AddItemRow()
        {
            var row = new SalesItemRow(AllProducts);
            row.OnChanged = UpdateTotals;
            row.IsTaxInclusive = CurrentOrder.IsTaxInclusive;
            OrderItems.Add(row);
            UpdateTotals();
        }

        [RelayCommand]
        private void RemoveItemRow(SalesItemRow row)
        {
            if (row != null && OrderItems.Contains(row))
            {
                OrderItems.Remove(row);
                UpdateTotals();
            }
        }

        [RelayCommand]
        private void CloseForm()
        {
            IsFormOpen = false;
        }

        [RelayCommand]
        private async Task SaveSale()
        {
            ErrorMessage = string.Empty;
            if (SelectedCustomer == null)
            {
                ErrorMessage = "Customer is required.";
                return;
            }

            if (!OrderItems.Any(row => row.SelectedProduct != null))
            {
                ErrorMessage = "Please add at least one valid product.";
                return;
            }

            CurrentOrder.CustomerId = SelectedCustomer.Id;
            CurrentOrder.PaymentTerms = PaymentTermsSearchText;
            CurrentOrder.TotalAmount = OrderTotalAmount;

            var items = OrderItems
                .Where(row => row.SelectedProduct != null)
                .Select(row => new SalesOrderItem
                {
                    ProductId = row.SelectedProduct!.Id,
                    QuantityOrdered = row.Quantity,
                    UnitPrice = row.UnitPrice,
                    TaxId = row.SelectedTax?.Id
                }).ToList();

            try
            {
                if (IsNew)
                {
                    if (SelectedTabIndex == 0)
                    {
                        await _salesOrderService.CreateSalesQuotationAsync(CurrentOrder, items);
                    }
                    else
                    {
                        await _salesOrderService.CreateSalesOrderAsync(CurrentOrder, items);
                    }
                }
                else
                {
                    await _salesOrderService.UpdateSalesOrderAsync(CurrentOrder, items);
                }

                IsFormOpen = false;
                await LoadSalesData();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ConfirmQuotation(SalesOrderListItem? item)
        {
            var target = item ?? SelectedOrder;
            if (target == null) return;
            await _salesOrderService.ConfirmQuotationAsync(target.SalesOrder.Id);
            await LoadSalesData();
            SelectedTabIndex = 1; // Go to Sales Orders tab
        }

        [RelayCommand]
        private async Task OpenDeliverOrder(SalesOrderListItem? item)
        {
            var target = item ?? SelectedOrder;
            if (target == null) return;

            DeliveryErrorMessage = string.Empty;
            var orderItems = await _salesOrderService.GetItemsAsync(target.SalesOrder.Id);
            
            DeliveryRows.Clear();
            foreach (var it in orderItems)
            {
                var prod = AllProducts.FirstOrDefault(p => p.Id == it.ProductId);
                int pending = it.QuantityOrdered - it.QuantityDelivered;
                
                DeliveryRows.Add(new SalesOrderDeliveryRow
                {
                    ItemId = it.Id,
                    ProductName = prod?.Name ?? "Unknown Product",
                    QuantityOrdered = it.QuantityOrdered,
                    QuantityDeliveredSoFar = it.QuantityDelivered,
                    QuantityToDeliver = pending,
                    AvailableStock = prod?.StockQuantity ?? 0
                });
            }

            CurrentOrder = target.SalesOrder;
            IsDeliveryModalOpen = true;
        }

        [RelayCommand]
        private async Task SubmitDelivery()
        {
            DeliveryErrorMessage = string.Empty;
            if (DeliveryRows.Any(r => r.QuantityToDeliver < 0))
            {
                DeliveryErrorMessage = "Delivery quantity cannot be negative.";
                return;
            }

            var payload = DeliveryRows
                .Select(r => (itemId: r.ItemId, quantityDelivered: r.QuantityToDeliver))
                .ToList();

            try
            {
                await _salesOrderService.DeliverSalesOrderAsync(CurrentOrder.Id, payload);
                IsDeliveryModalOpen = false;
                await LoadSalesData();
            }
            catch (Exception ex)
            {
                DeliveryErrorMessage = $"Delivery failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CloseDeliveryModal()
        {
            IsDeliveryModalOpen = false;
        }

        [RelayCommand]
        private async Task InvoiceOrder(SalesOrderListItem? item)
        {
            var target = item ?? SelectedOrder;
            if (target == null) return;
            await _salesOrderService.InvoiceSalesOrderAsync(target.SalesOrder.Id);
            await LoadSalesData();
        }

        [RelayCommand]
        private async Task DeleteOrder(SalesOrderListItem? item)
        {
            var target = item ?? SelectedOrder;
            if (target == null) return;
            
            bool deleted = await _salesOrderService.DeleteSalesOrderAsync(target.SalesOrder.Id);
            if (!deleted)
            {
                ErrorMessage = "Cannot delete delivered or invoiced order. You can only delete drafts or un-delivered sales.";
            }
            else
            {
                await LoadSalesData();
            }
        }

        [RelayCommand]
        private async Task OpenDetails(SalesOrderListItem? displayItem)
        {
            if (displayItem == null) return;
            var so = displayItem.SalesOrder;

            try
            {
                DetailedSo = so;
                DetailedCustomer = AllCustomers.FirstOrDefault(c => c.Id == so.CustomerId);
                
                var dbItems = await _salesOrderService.GetItemsAsync(so.Id);
                var products = await _inventoryService.GetAllProductsAsync();
                var taxes = await _taxService.GetAllTaxesAsync();

                var rows = new List<SalesOrderItemDisplayRow>();
                foreach (var it in dbItems)
                {
                    var product = products.FirstOrDefault(p => p.Id == it.ProductId);
                    var tax = taxes.FirstOrDefault(t => t.Id == it.TaxId);
                    rows.Add(new SalesOrderItemDisplayRow(it, product, tax));
                }

                DetailedItems = new ObservableCollection<SalesOrderItemDisplayRow>(rows);
                UpdateDetailedTotals(rows);

                OnPropertyChanged(nameof(DetailedSoIsArchived));
                OnPropertyChanged(nameof(CanCancelDetailedSo));
                OnPropertyChanged(nameof(ArchiveDetailedSoButtonText));
                OnPropertyChanged(nameof(CanDeliverDetailedSo));
                OnPropertyChanged(nameof(CanReturnDetailedSo));
                OnPropertyChanged(nameof(CanInvoiceDetailedSo));
                OnPropertyChanged(nameof(IsDetailedSoInvoiced));
                OnPropertyChanged(nameof(CanRecordPayment));

                await LoadPaymentDetailsAsync();
                await UpdateDetailedBaseCurrencyEquivalentAsync();
                IsDetailsOpen = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error opening details: {ex.Message}";
            }
        }

        private async Task LoadPaymentDetailsAsync()
        {
            if (DetailedSo == null || DetailedSo.BillingStatus != "Invoiced")
            {
                DetailedOpenBalance = 0;
                DetailedAmountPaid = 0;
                DetailedPayments = new ObservableCollection<InvoicePayment>();
                OnPropertyChanged(nameof(CanRecordPayment));
                return;
            }

            DetailedAmountPaid = await _paymentService.GetAmountPaidAsync("SalesOrder", DetailedSo.Id);
            DetailedOpenBalance = await _paymentService.GetOpenBalanceAsync("SalesOrder", DetailedSo.Id);
            var payments = await _paymentService.GetPaymentsForDocumentAsync("SalesOrder", DetailedSo.Id);
            DetailedPayments = new ObservableCollection<InvoicePayment>(payments);
            OnPropertyChanged(nameof(CanRecordPayment));
        }

        [RelayCommand]
        private void OpenRecordPayment()
        {
            if (!CanRecordPayment) return;
            PaymentAmount = DetailedOpenBalance;
            PaymentMethod = "Bank";
            PaymentReference = string.Empty;
            PaymentErrorMessage = string.Empty;
            IsPaymentModalOpen = true;
        }

        [RelayCommand]
        private async Task SubmitPayment()
        {
            PaymentErrorMessage = string.Empty;
            if (DetailedSo == null) return;
            if (PaymentAmount <= 0)
            {
                PaymentErrorMessage = "Payment amount must be greater than zero.";
                return;
            }

            try
            {
                await _paymentService.RecordInvoicePaymentAsync(
                    "SalesOrder",
                    DetailedSo.Id,
                    PaymentAmount,
                    PaymentMethod,
                    UserSession.CurrentUser?.Username ?? "System",
                    reference: PaymentReference);

                IsPaymentModalOpen = false;
                await LoadPaymentDetailsAsync();
                ErrorMessage = $"Recorded payment of {PaymentAmount:N2} {DetailedSo.Currency} for {DetailedSo.SONumber}";
            }
            catch (Exception ex)
            {
                PaymentErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void ClosePaymentModal()
        {
            IsPaymentModalOpen = false;
        }

        private async Task UpdateDetailedBaseCurrencyEquivalentAsync()
        {
            if (DetailedSo == null)
            {
                DetailedBaseCurrencyEquivalent = string.Empty;
                return;
            }

            var (_, label) = await _currencyService.TryFormatBaseEquivalentAsync(
                DetailedTotal,
                DetailedSo.Currency,
                BaseCurrency,
                DetailedSo.OrderDate);

            DetailedBaseCurrencyEquivalent = label;
        }

        private void UpdateDetailedTotals(List<SalesOrderItemDisplayRow> rows)
        {
            decimal subtotal = 0;
            decimal total = 0;
            var taxBreakdown = new Dictionary<int, (Tax Tax, decimal Amount)>();

            foreach (var r in rows)
            {
                var rowSubtotal = r.QuantityOrdered * r.UnitPrice;
                if (r.Tax != null)
                {
                    decimal taxAmount = 0;
                    if (r.Tax.IncludedInPrice == "Include")
                    {
                        decimal basePrice;
                        if (r.Tax.Computation == "Percentage")
                        {
                            basePrice = rowSubtotal / (1 + (r.Tax.Amount / 100));
                        }
                        else
                        {
                            basePrice = Math.Max(0, rowSubtotal - (r.QuantityOrdered * r.Tax.Amount));
                        }
                        taxAmount = rowSubtotal - basePrice;
                        subtotal += basePrice;
                        total += rowSubtotal;
                    }
                    else
                    {
                        subtotal += rowSubtotal;
                        if (r.Tax.Computation == "Percentage")
                        {
                            taxAmount = rowSubtotal * (r.Tax.Amount / 100);
                        }
                        else
                        {
                            taxAmount = r.QuantityOrdered * r.Tax.Amount;
                        }
                        total += rowSubtotal + taxAmount;
                    }

                    if (taxAmount > 0)
                    {
                        if (taxBreakdown.ContainsKey(r.Tax.Id))
                        {
                            var existing = taxBreakdown[r.Tax.Id];
                            taxBreakdown[r.Tax.Id] = (r.Tax, existing.Amount + taxAmount);
                        }
                        else
                        {
                            taxBreakdown[r.Tax.Id] = (r.Tax, taxAmount);
                        }
                    }
                }
                else
                {
                    subtotal += rowSubtotal;
                    total += rowSubtotal;
                }
            }

            DetailedSubtotal = subtotal;
            DetailedTotal = total;

            _ = UpdateDetailedBaseCurrencyEquivalentAsync();

            if (taxBreakdown.Count == 0)
            {
                DetailedTaxBreakdownText = "Taxes: None";
            }
            else
            {
                var parts = taxBreakdown.Values.Select(tb => 
                    $"{tb.Tax.Name} ({tb.Tax.Amount}%): {tb.Amount:N2} {(tb.Tax.IncludedInPrice == "Include" ? "Incl." : "")}");
                DetailedTaxBreakdownText = "Taxes breakdown:\n" + string.Join("\n", parts);
            }
        }

        [RelayCommand]
        private void CloseDetails()
        {
            IsDetailsOpen = false;
        }

        [RelayCommand]
        private async Task ValidateDelivery()
        {
            if (DetailedSo == null) return;
            try
            {
                var dbItems = await _salesOrderService.GetItemsAsync(DetailedSo.Id);
                var deliveryLines = new List<(int itemId, int quantityDelivered)>();

                foreach (var item in dbItems)
                {
                    int remaining = item.QuantityOrdered - item.QuantityDelivered;
                    if (remaining > 0)
                    {
                        deliveryLines.Add((item.Id, remaining));
                    }
                }

                if (deliveryLines.Any())
                {
                    await _salesOrderService.DeliverSalesOrderAsync(DetailedSo.Id, deliveryLines);
                    ErrorMessage = $"Validated and delivered goods for {DetailedSo.SONumber}";
                }
                else
                {
                    ErrorMessage = "All goods already delivered.";
                }

                // Refresh details
                var updatedList = await _salesOrderService.GetAllSalesOrdersAsync();
                var updatedSo = updatedList.FirstOrDefault(x => x.SalesOrder.Id == DetailedSo.Id);

                if (updatedSo != null)
                {
                    DetailedSo = updatedSo.SalesOrder;
                    await OpenDetails(updatedSo);
                }

                OnPropertyChanged(nameof(CanDeliverDetailedSo));
                OnPropertyChanged(nameof(CanInvoiceDetailedSo));
                OnPropertyChanged(nameof(IsDetailedSoInvoiced));
                OnPropertyChanged(nameof(CanRecordPayment));

                await LoadPaymentDetailsAsync();
                await LoadSalesData();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Delivery validation failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task InvoiceDetailedOrder()
        {
            if (DetailedSo == null) return;
            try
            {
                await _salesOrderService.InvoiceSalesOrderAsync(DetailedSo.Id);
                ErrorMessage = $"Created invoice for {DetailedSo.SONumber}";

                var updatedList = await _salesOrderService.GetAllSalesOrdersAsync();
                var updatedSo = updatedList.FirstOrDefault(x => x.SalesOrder.Id == DetailedSo.Id);

                if (updatedSo != null)
                {
                    DetailedSo = updatedSo.SalesOrder;
                    await OpenDetails(updatedSo);
                }

                OnPropertyChanged(nameof(CanDeliverDetailedSo));
                OnPropertyChanged(nameof(CanInvoiceDetailedSo));
                OnPropertyChanged(nameof(IsDetailedSoInvoiced));
                OnPropertyChanged(nameof(CanRecordPayment));

                await LoadPaymentDetailsAsync();
                await LoadSalesData();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Invoicing failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task PrintDetailedSoPdf()
        {
            if (DetailedSo == null) return;
            try
            {
                var items = await _salesOrderService.GetItemsAsync(DetailedSo.Id);
                var products = await _inventoryService.GetAllProductsAsync();
                var taxes = await _taxService.GetAllTaxesAsync();

                var path = _pdfService.GenerateSalesOrderPdf(DetailedSo, items, products, taxes, DetailedCustomer, asInvoice: false);

                if (File.Exists(path))
                {
                    new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo(path)
                        {
                            UseShellExecute = true
                        }
                    }.Start();
                    ErrorMessage = $"Opened SO PDF: {Path.GetFileName(path)}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"PDF print failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task PrintDetailedInvoicePdf()
        {
            if (DetailedSo == null) return;
            try
            {
                var items = await _salesOrderService.GetItemsAsync(DetailedSo.Id);
                var products = await _inventoryService.GetAllProductsAsync();
                var taxes = await _taxService.GetAllTaxesAsync();

                var path = _pdfService.GenerateSalesOrderPdf(DetailedSo, items, products, taxes, DetailedCustomer, asInvoice: true);

                if (File.Exists(path))
                {
                    new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo(path)
                        {
                            UseShellExecute = true
                        }
                    }.Start();
                    ErrorMessage = $"Opened Invoice PDF: {Path.GetFileName(path)}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"PDF print failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task CancelDetailedSo()
        {
            if (DetailedSo == null) return;
            try
            {
                await _salesOrderService.CancelSalesOrderAsync(DetailedSo.Id);
                ErrorMessage = $"Cancelled order {DetailedSo.SONumber}";

                var updatedList = await _salesOrderService.GetAllSalesOrdersAsync();
                var updatedSo = updatedList.FirstOrDefault(x => x.SalesOrder.Id == DetailedSo.Id);

                if (updatedSo != null)
                {
                    DetailedSo = updatedSo.SalesOrder;
                    await OpenDetails(updatedSo);
                }

                await LoadSalesData();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Cancel failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ToggleArchiveDetailedSo()
        {
            if (DetailedSo == null) return;
            try
            {
                DetailedSo.IsArchived = !DetailedSo.IsArchived;
                await _salesOrderService.UpdateSalesOrderAsync(DetailedSo, await _salesOrderService.GetItemsAsync(DetailedSo.Id));
                ErrorMessage = DetailedSo.IsArchived ? "Archived order" : "Unarchived order";

                var updatedList = await _salesOrderService.GetAllSalesOrdersAsync();
                var updatedSo = updatedList.FirstOrDefault(x => x.SalesOrder.Id == DetailedSo.Id);

                if (updatedSo != null)
                {
                    DetailedSo = updatedSo.SalesOrder;
                    await OpenDetails(updatedSo);
                }

                await LoadSalesData();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Archive toggle failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteDetailedSo()
        {
            if (DetailedSo == null) return;
            try
            {
                bool deleted = await _salesOrderService.DeleteSalesOrderAsync(DetailedSo.Id);
                if (!deleted)
                {
                    ErrorMessage = "Cannot delete delivered or invoiced order. You can only delete drafts or un-delivered sales.";
                }
                else
                {
                    IsDetailsOpen = false;
                    await LoadSalesData();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Delete failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task PrintPdf(SalesOrderListItem? item)
        {
            var target = item ?? SelectedOrder;
            if (target == null) return;

            try
            {
                var dbItems = await _salesOrderService.GetItemsAsync(target.SalesOrder.Id);
                var products = await _inventoryService.GetAllProductsAsync();
                var taxes = await _taxService.GetAllTaxesAsync();
                var customer = AllCustomers.FirstOrDefault(c => c.Id == target.SalesOrder.CustomerId);

                var path = _pdfService.GenerateSalesOrderPdf(
                    target.SalesOrder, 
                    dbItems, 
                    products, 
                    taxes, 
                    customer,
                    asInvoice: target.SalesOrder.BillingStatus == "Invoiced");

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
                ErrorMessage = $"Failed to print PDF: {ex.Message}";
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            _ = LoadSalesData();
        }

        // --- CUSTOMER SEARCH / INLINE CREATION ---
        partial void OnCustomerSearchTextChanged(string value)
        {
            if (SelectedCustomer != null && value != SelectedCustomer.Name)
            {
                SelectedCustomer = null;
            }

            if (string.IsNullOrWhiteSpace(value) || (SelectedCustomer != null && value == SelectedCustomer.Name))
            {
                MatchedCustomers = new ObservableCollection<Customer>(AllCustomers.Take(5));
                IsCreateCustomerButtonVisible = false;
                IsCustomerListVisible = false;
                return;
            }

            var query = value.ToLower();
            var matches = AllCustomers.Where(c => c.Name.ToLower().Contains(query)).Take(5).ToList();
            MatchedCustomers = new ObservableCollection<Customer>(matches);
            IsCreateCustomerButtonVisible = matches.Count == 0;
            IsCustomerListVisible = true;
        }

        [RelayCommand]
        private void SelectCustomer(Customer customer)
        {
            if (customer == null) return;
            SelectedCustomer = customer;
            CustomerSearchText = customer.Name;
            CurrentOrder.CustomerId = customer.Id;
            IsCustomerListVisible = false;
            IsCreateCustomerButtonVisible = false;
        }

        [RelayCommand]
        private void OpenCreateCustomer()
        {
            NewCustomerName = CustomerSearchText;
            NewCustomerPhone = string.Empty;
            NewCustomerEmail = string.Empty;
            NewCustomerAddress = string.Empty;
            NewCustomerErrorMessage = string.Empty;
            IsCreateCustomerModalOpen = true;
        }

        [RelayCommand]
        private async Task SaveNewCustomer()
        {
            NewCustomerErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(NewCustomerName))
            {
                NewCustomerErrorMessage = "Name is required.";
                return;
            }

            try
            {
                var s = new Customer
                {
                    Name = NewCustomerName,
                    Phone = NewCustomerPhone,
                    Email = NewCustomerEmail,
                    Address = NewCustomerAddress,
                    IsActive = true
                };

                await _customerService.AddCustomerAsync(s);
                
                // Refresh customer list
                AllCustomers = await _customerService.GetAllCustomersAsync();
                var created = AllCustomers.FirstOrDefault(x => x.Name == s.Name);
                if (created != null)
                {
                    SelectCustomer(created);
                }

                IsCreateCustomerModalOpen = false;
            }
            catch (Exception ex)
            {
                NewCustomerErrorMessage = $"Failed to create customer: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CloseCreateCustomerModal()
        {
            IsCreateCustomerModalOpen = false;
        }

        // --- PAYMENT TERMS SEARCH / INLINE CREATION ---
        partial void OnPaymentTermsSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || AllPaymentTerms.Any(t => t.Name == value))
            {
                MatchedPaymentTerms = new ObservableCollection<PaymentTerm>(AllPaymentTerms.Take(5));
                IsCreatePaymentTermButtonVisible = false;
                IsPaymentTermsListVisible = false;
                return;
            }

            var query = value.ToLower();
            var matches = AllPaymentTerms.Where(t => t.Name.ToLower().Contains(query)).Take(5).ToList();
            MatchedPaymentTerms = new ObservableCollection<PaymentTerm>(matches);
            IsCreatePaymentTermButtonVisible = matches.Count == 0;
            IsPaymentTermsListVisible = true;
        }

        [RelayCommand]
        private void SelectPaymentTerm(PaymentTerm term)
        {
            if (term == null) return;
            PaymentTermsSearchText = term.Name;
            CurrentOrder.PaymentTerms = term.Name;
            IsPaymentTermsListVisible = false;
            IsCreatePaymentTermButtonVisible = false;
        }

        [RelayCommand]
        private void OpenCreatePaymentTerm()
        {
            NewPaymentTermName = PaymentTermsSearchText;
            NewPaymentTermDescription = string.Empty;
            NewPaymentTermErrorMessage = string.Empty;
            IsCreatePaymentTermModalOpen = true;
        }

        [RelayCommand]
        private async Task SaveNewPaymentTerm()
        {
            NewPaymentTermErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(NewPaymentTermName))
            {
                NewPaymentTermErrorMessage = "Term name is required.";
                return;
            }

            try
            {
                var term = new PaymentTerm
                {
                    Name = NewPaymentTermName,
                    Description = NewPaymentTermDescription
                };

                await _salesOrderService.CreatePaymentTermAsync(term);

                AllPaymentTerms = await _salesOrderService.GetAllPaymentTermsAsync();
                var created = AllPaymentTerms.FirstOrDefault(t => t.Name == term.Name);
                if (created != null)
                {
                    SelectPaymentTerm(created);
                }

                IsCreatePaymentTermModalOpen = false;
            }
            catch (Exception ex)
            {
                NewPaymentTermErrorMessage = $"Failed to create payment term: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CloseCreatePaymentTermModal()
        {
            IsCreatePaymentTermModalOpen = false;
        }

        public void ToggleTaxInclusive(bool isInclusive)
        {
            CurrentOrder.IsTaxInclusive = isInclusive;
            foreach (var r in OrderItems)
            {
                r.IsTaxInclusive = isInclusive;
            }
            UpdateTotals();
        }

        public void UpdateTotals()
        {
            decimal subtotal = 0;
            var taxBreakdown = new Dictionary<int, (Tax Tax, decimal Amount)>();

            foreach (var r in OrderItems)
            {
                if (r.SelectedProduct == null) continue;

                var rowSubtotal = r.Quantity * r.UnitPrice;
                decimal basePrice = rowSubtotal;

                if (r.SelectedTax != null)
                {
                    decimal taxAmount = 0;
                    if (r.IsTaxInclusive || r.SelectedTax.IncludedInPrice == "Include")
                    {
                        if (r.SelectedTax.Computation == "Percentage")
                        {
                            basePrice = rowSubtotal / (1 + (r.SelectedTax.Amount / 100));
                        }
                        else
                        {
                            basePrice = Math.Max(0, rowSubtotal - (r.Quantity * r.SelectedTax.Amount));
                        }
                        taxAmount = rowSubtotal - basePrice;
                    }
                    else
                    {
                        if (r.SelectedTax.Computation == "Percentage")
                        {
                            taxAmount = rowSubtotal * (r.SelectedTax.Amount / 100);
                        }
                        else
                        {
                            taxAmount = r.Quantity * r.SelectedTax.Amount;
                        }
                    }

                    if (taxAmount > 0)
                    {
                        if (taxBreakdown.ContainsKey(r.SelectedTax.Id))
                        {
                            var existing = taxBreakdown[r.SelectedTax.Id];
                            taxBreakdown[r.SelectedTax.Id] = (r.SelectedTax, existing.Amount + taxAmount);
                        }
                        else
                        {
                            taxBreakdown[r.SelectedTax.Id] = (r.SelectedTax, taxAmount);
                        }
                    }
                }

                subtotal += basePrice;
            }

            OrderSubtotalAmount = subtotal;
            decimal totalTaxes = taxBreakdown.Values.Sum(tb => tb.Amount);
            OrderTotalAmount = subtotal + totalTaxes;

            if (taxBreakdown.Count == 0)
            {
                OrderTaxBreakdownText = "Taxes: None";
            }
            else
            {
                var parts = taxBreakdown.Values.Select(tb => 
                    $"{tb.Tax.Name} ({tb.Tax.Amount}%): {tb.Amount:N2} RWF");
                OrderTaxBreakdownText = "Taxes breakdown:\n" + string.Join("\n", parts);
            }
        }

        [RelayCommand]
        private async Task OpenReturnOrder(SalesOrderListItem? item)
        {
            var target = item ?? SelectedOrder;
            if (target == null) return;

            ReturnErrorMessage = string.Empty;
            var orderItems = await _salesOrderService.GetItemsAsync(target.SalesOrder.Id);
            
            ReturnRows.Clear();
            foreach (var it in orderItems)
            {
                if (it.QuantityDelivered <= 0) continue;

                var prod = AllProducts.FirstOrDefault(p => p.Id == it.ProductId);
                
                ReturnRows.Add(new SalesOrderReturnRow
                {
                    ItemId = it.Id,
                    ProductId = it.ProductId,
                    ProductName = prod?.Name ?? "Unknown Product",
                    QuantityDelivered = it.QuantityDelivered,
                    QuantityToReturn = it.QuantityDelivered, // Default to full return
                    RefundAmount = it.QuantityDelivered * it.UnitPrice, // Default full refund
                    Condition = "Resaleable",
                    Reason = "Customer Return"
                });
            }

            if (ReturnRows.Count == 0)
            {
                ErrorMessage = "No delivered items found on this order to return.";
                return;
            }

            CurrentOrder = target.SalesOrder;
            IsReturnModalOpen = true;
        }

        [RelayCommand]
        private async Task SubmitReturn()
        {
            ReturnErrorMessage = string.Empty;
            if (ReturnRows.Any(r => r.QuantityToReturn < 0))
            {
                ReturnErrorMessage = "Return quantity cannot be negative.";
                return;
            }
            if (ReturnRows.Any(r => r.QuantityToReturn > r.QuantityDelivered))
            {
                ReturnErrorMessage = "Return quantity cannot exceed delivered quantity.";
                return;
            }

            try
            {
                var payload = ReturnRows
                    .Where(r => r.QuantityToReturn > 0)
                    .Select(r => (r.ItemId, r.QuantityToReturn, r.Condition, r.Reason, r.RefundAmount))
                    .ToList();

                if (payload.Count == 0)
                {
                    ReturnErrorMessage = "Please specify at least one item and quantity to return.";
                    return;
                }

                await _returnsService.ProcessSalesOrderReturnAsync(CurrentOrder.Id, payload, UserSession.CurrentUser?.Username ?? "System");
                IsReturnModalOpen = false;
                
                // Refresh Order Details
                var updatedOrder = (await _salesOrderService.GetAllSalesOrdersAsync())
                    .FirstOrDefault(o => o.SalesOrder.Id == CurrentOrder.Id);
                if (updatedOrder != null)
                {
                    await OpenDetails(updatedOrder);
                }
                
                await LoadSalesData();
            }
            catch (Exception ex)
            {
                ReturnErrorMessage = $"Return failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CloseReturnModal()
        {
            IsReturnModalOpen = false;
        }
    }

    public partial class SalesItemRow : ViewModelBase
    {
        private string _productSearchText = string.Empty;
        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                if (SetProperty(ref _productSearchText, value))
                {
                    if (SelectedProduct != null && value != SelectedProduct.Name)
                    {
                        SelectedProduct = null;
                    }
                    FilterProducts();
                    OnPropertyChanged(nameof(IsDropdownVisible));
                }
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
                    UnitPrice = value.Price; // Selling price
                    ProductSearchText = value.Name;
                }
                OnChanged?.Invoke();
                OnPropertyChanged(nameof(IsDropdownVisible));
            }
        }

        public bool IsDropdownVisible => SelectedProduct == null && !string.IsNullOrWhiteSpace(ProductSearchText) && MatchedProducts.Count > 0;

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                SetProperty(ref _quantity, value);
                OnPropertyChanged(nameof(TotalPrice));
                OnChanged?.Invoke();
            }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                SetProperty(ref _unitPrice, value);
                OnPropertyChanged(nameof(TotalPrice));
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
                OnPropertyChanged(nameof(TotalPrice));
                OnChanged?.Invoke();
            }
        }

        private bool _isTaxInclusive;
        public bool IsTaxInclusive
        {
            get => _isTaxInclusive;
            set
            {
                SetProperty(ref _isTaxInclusive, value);
                OnPropertyChanged(nameof(TotalPrice));
                OnChanged?.Invoke();
            }
        }

        public decimal BasePrice
        {
            get
            {
                var subtotal = Quantity * UnitPrice;
                if (SelectedTax != null && (IsTaxInclusive || SelectedTax.IncludedInPrice == "Include"))
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
                var basePrice = BasePrice;
                if (IsTaxInclusive || SelectedTax.IncludedInPrice == "Include")
                {
                    return (Quantity * UnitPrice) - basePrice;
                }
                else
                {
                    if (SelectedTax.Computation == "Percentage")
                    {
                        return basePrice * (SelectedTax.Amount / 100);
                    }
                    else
                    {
                        return Quantity * SelectedTax.Amount;
                    }
                }
            }
        }

        public decimal TotalPrice
        {
            get
            {
                if (SelectedTax != null && (IsTaxInclusive || SelectedTax.IncludedInPrice == "Include"))
                {
                    return Quantity * UnitPrice;
                }
                return BasePrice + TaxAmount;
            }
        }

        private readonly List<Product> _allProducts;
        public Action? OnChanged { get; set; }

        public SalesItemRow(List<Product> allProducts)
        {
            _allProducts = allProducts;
            MatchedProducts = new ObservableCollection<Product>();
        }

        private void FilterProducts()
        {
            if (string.IsNullOrWhiteSpace(ProductSearchText) || (SelectedProduct != null && ProductSearchText == SelectedProduct.Name))
            {
                MatchedProducts.Clear();
                OnPropertyChanged(nameof(IsDropdownVisible));
                return;
            }
            var query = ProductSearchText.ToLower();
            var matches = _allProducts.Where(p => p.Name.ToLower().Contains(query)).Take(5).ToList();
            MatchedProducts.Clear();
            foreach (var m in matches)
            {
                MatchedProducts.Add(m);
            }
            OnPropertyChanged(nameof(IsDropdownVisible));
        }

        [RelayCommand]
        private void SelectProduct(Product product)
        {
            SelectedProduct = product;
        }    }

    public class SalesOrderReturnRow : ObservableObject
    {
        public int ItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int QuantityDelivered { get; set; }
        
        private int _quantityToReturn;
        public int QuantityToReturn
        {
            get => _quantityToReturn;
            set => SetProperty(ref _quantityToReturn, value);
        }

        private string _condition = "Resaleable"; // Resaleable, Damaged, Destroyed
        public string Condition
        {
            get => _condition;
            set => SetProperty(ref _condition, value);
        }

        private string _reason = "Customer Return";
        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        private decimal _refundAmount;
        public decimal RefundAmount
        {
            get => _refundAmount;
            set => SetProperty(ref _refundAmount, value);
        }
    }

    public class SalesOrderDeliveryRow
    {
        public int ItemId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int QuantityOrdered { get; set; }
        public int QuantityDeliveredSoFar { get; set; }
        public int QuantityToDeliver { get; set; }
        public int AvailableStock { get; set; }
    }

    public class SalesOrderItemDisplayRow
    {
        public SalesOrderItem Item { get; }
        public Product? Product { get; }
        public Tax? Tax { get; }

        public string ProductName => Product?.Name ?? $"Product ID: {Item.ProductId}";
        public int QuantityOrdered => Item.QuantityOrdered;
        public int QuantityDelivered => Item.QuantityDelivered;
        public int QuantityInvoiced => Item.QuantityInvoiced;
        public decimal UnitPrice => Item.UnitPrice;
        public string TaxName => Tax != null ? $"{Tax.Name} ({Tax.Amount}%)" : "None";
        public decimal Amount
        {
            get
            {
                decimal baseAmount = Item.QuantityOrdered * Item.UnitPrice;
                if (Tax != null)
                {
                    if (Tax.IncludedInPrice != "Include" && Tax.Computation == "Percentage")
                    {
                        return baseAmount * (1 + (Tax.Amount / 100));
                    }
                    else if (Tax.IncludedInPrice != "Include")
                    {
                        return baseAmount + (Item.QuantityOrdered * Tax.Amount);
                    }
                }
                return baseAmount;
            }
        }

        public SalesOrderItemDisplayRow(SalesOrderItem item, Product? product, Tax? tax)
        {
            Item = item;
            Product = product;
            Tax = tax;
        }
    }
}
