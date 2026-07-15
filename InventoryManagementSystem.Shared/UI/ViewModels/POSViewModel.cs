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

        public Action? OnChanged { get; set; }

        public CartItem(Product product, int quantity, decimal unitPrice)
        {
            _product = product;
            _quantity = quantity;
            _unitPrice = unitPrice;
            _maxStock = product.StockQuantity;
        }

        partial void OnQuantityChanged(int value)
        {
            OnPropertyChanged(nameof(Subtotal));
            OnChanged?.Invoke();
        }

        partial void OnUnitPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(Subtotal));
            OnChanged?.Invoke();
        }

        public void Increment()
        {
            if (Product.ProductType == "Service" || Quantity < _maxStock)
            {
                Quantity++;
            }
        }

        public void Decrement()
        {
            if (Quantity > 1)
            {
                Quantity--;
            }
        }
    }

    public partial class POSViewModel : ViewModelBase
    {
        private readonly InventoryService _inventoryService;
        private readonly LicenseService _licenseService;
        private readonly ReceiptService _receiptService;
        private readonly SettingsService _settingsService;
        private readonly SalesOrderService _salesOrderService;
        private readonly CustomerService _customerService;
        private readonly JournalService _journalService;
        private readonly TaxService _taxService;
        private readonly BarcodeService _barcodeService;
        private readonly CurrencyService _currencyService;
        private readonly AuditService? _auditService;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private string _barcodeStatusMessage = string.Empty;
        [ObservableProperty] private ObservableCollection<CartItem> _cartItems = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private decimal _amountPaid;
        [ObservableProperty] private decimal _changeDue;
        [ObservableProperty] private string _posCheckoutCurrency = "RWF";
        [ObservableProperty] private decimal _checkoutTotalAmount;
        [ObservableProperty] private string _checkoutBaseEquivalent = string.Empty;

        public List<string> PosCurrencies { get; } = new() { "USD", "EUR", "GBP", "KES", "RWF", "UGX" };
        [ObservableProperty] private bool _isReceiptModalOpen;
        [ObservableProperty] private string _lastReceiptPath = string.Empty;
        [ObservableProperty] private string _lastReceiptText = string.Empty; // Keep for fallback/display

        // --- Screen Navigation ---
        [ObservableProperty] private string _activeTab = "Sales"; // "Sales", "Orders", "PaymentMethods"
        public bool IsSalesTabActive => ActiveTab == "Sales";
        public bool IsOrdersTabActive => ActiveTab == "Orders";
        public bool IsPaymentMethodsTabActive => ActiveTab == "PaymentMethods";

        // --- Cashier ---
        public string CashierName => UserSession.CurrentUser?.Username ?? "Cashier";

        // --- POS Payment Methods Screen ---
        [ObservableProperty] private ObservableCollection<PosPaymentMethod> _paymentMethods = new();
        [ObservableProperty] private ObservableCollection<Journal> _journals = new();
        [ObservableProperty] private string _newPaymentMethodName = string.Empty;
        [ObservableProperty] private Journal? _newPaymentMethodSelectedJournal;

        // --- POS Checkout Payment Panel ---
        [ObservableProperty] private bool _isPaymentPanelVisible;
        [ObservableProperty] private ObservableCollection<Customer> _matchedCustomers = new();
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private string _customerSearchText = string.Empty;
        [ObservableProperty] private PosPaymentMethod? _selectedPaymentMethod;
        [ObservableProperty] private bool _autoCreateInvoice = true;

        // --- inline Customer Creation Modal ---
        [ObservableProperty] private bool _isCreateCustomerModalOpen;
        [ObservableProperty] private string _newCustomerName = string.Empty;
        [ObservableProperty] private string _newCustomerPhone = string.Empty;
        [ObservableProperty] private string _newCustomerEmail = string.Empty;
        [ObservableProperty] private string _newCustomerAddress = string.Empty;
        [ObservableProperty] private string _newCustomerErrorMessage = string.Empty;
        [ObservableProperty] private bool _isCheckoutSuccess;

        // --- POS Order History ---
        [ObservableProperty] private ObservableCollection<SalesOrderListItem> _posOrders = new();

        // --- POS Order Details Modal ---
        [ObservableProperty] private bool _isOrderDetailsOpen;
        [ObservableProperty] private SalesOrder? _detailedOrder;
        [ObservableProperty] private string _detailedCustomerName = string.Empty;
        [ObservableProperty] private ObservableCollection<POSDetailedOrderItemRow> _detailedOrderItems = new();
        [ObservableProperty] private ObservableCollection<POSDetailedPaymentRow> _detailedPayments = new();
        [ObservableProperty] private decimal _detailedSubtotal;
        [ObservableProperty] private decimal _detailedTaxes;
        [ObservableProperty] private decimal _detailedTotal;
        [ObservableProperty] private string _detailedTaxBreakdownText = string.Empty;

        public string CurrencySymbol => _settingsService.CurrentSettings.CurrencySymbol;
        public string BaseCurrency => CurrencySymbol ?? "RWF";
        public string ActiveCheckoutCurrency => string.IsNullOrWhiteSpace(PosCheckoutCurrency) ? BaseCurrency : PosCheckoutCurrency;
        public LanguageService Language { get; }

        public POSViewModel(
            InventoryService inventoryService, 
            LicenseService licenseService, 
            ReceiptService receiptService, 
            SettingsService settingsService, 
            LanguageService languageService,
            SalesOrderService salesOrderService,
            CustomerService customerService,
            JournalService journalService,
            TaxService taxService,
            BarcodeService barcodeService,
            CurrencyService currencyService,
            AuditService? auditService = null)
        {
            _inventoryService = inventoryService;
            _licenseService = licenseService; // Future pro features
            _receiptService = receiptService;
            _settingsService = settingsService;
            Language = languageService;
            _salesOrderService = salesOrderService;
            _customerService = customerService;
            _auditService = auditService;
            _journalService = journalService;
            _taxService = taxService;
            _barcodeService = barcodeService;
            _currencyService = currencyService;
            PosCheckoutCurrency = BaseCurrency;
            CheckoutTotalAmount = 0;
            LoadProductsCommand.Execute(null);
            _ = EnsureWalkInCustomerExistsAsync();
        }

        async partial void OnSearchTextChanged(string value)
        {
            await LoadProducts();
        }

        [RelayCommand]
        private async Task ScanBarcodeAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return;
            }

            var product = await _barcodeService.FindProductByBarcodeAsync(SearchText.Trim());
            if (product == null)
            {
                BarcodeStatusMessage = "No product found for this barcode.";
                return;
            }

            if (!product.AvailableInPOS || !product.CanBeSold)
            {
                BarcodeStatusMessage = $"{product.Name} is not available at POS.";
                return;
            }

            AddToCart(product);
            SearchText = string.Empty;
            BarcodeStatusMessage = $"Added {product.Name} to cart.";
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
            if (product.ProductType != "Service" && product.StockQuantity <= 0) return; // Prevent adding if out of stock

            var existing = CartItems.FirstOrDefault(c => c.Product.Id == product.Id);
            if (existing != null)
            {
                if (existing.Quantity < product.StockQuantity || product.ProductType == "Service")
                {
                    existing.Increment();
                }
            }
            else
            {
                // Default selling price: use product.Price, but if it is 0 or less, fall back to product.Cost
                decimal sellingPrice = product.Price > 0 ? product.Price : product.Cost;
                var item = new CartItem(product, 1, sellingPrice);
                item.OnChanged = RecalculateTotal;
                CartItems.Add(item);
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
            _ = UpdateCheckoutAmountsAsync();
        }

        partial void OnPosCheckoutCurrencyChanged(string value)
        {
            _ = UpdateCheckoutAmountsAsync();
        }

        private async Task UpdateCheckoutAmountsAsync()
        {
            var checkoutCurrency = ActiveCheckoutCurrency;
            if (string.Equals(checkoutCurrency, BaseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                CheckoutTotalAmount = TotalAmount;
                CheckoutBaseEquivalent = string.Empty;
            }
            else
            {
                var (converted, _) = await _currencyService.TryFormatFromBaseAsync(TotalAmount, checkoutCurrency, BaseCurrency);
                CheckoutTotalAmount = converted ?? TotalAmount;
                CheckoutBaseEquivalent = $"Listed at {TotalAmount:N2} {BaseCurrency}";
            }

            OnPropertyChanged(nameof(ActiveCheckoutCurrency));
            CalculateChange();
        }

        partial void OnAmountPaidChanged(decimal value)
        {
            CalculateChange();
        }

        private void CalculateChange()
        {
            ChangeDue = AmountPaid - CheckoutTotalAmount;
        }

        [RelayCommand]
        private void SwitchTab(string tabName)
        {
            ActiveTab = tabName;
            IsPaymentPanelVisible = false;
        }

        [RelayCommand]
        private void TogglePaymentPanel()
        {
            if (CartItems.Count == 0) return;
            IsPaymentPanelVisible = !IsPaymentPanelVisible;
            if (IsPaymentPanelVisible)
            {
                _ = LoadCustomersAndPaymentMethodsAsync();
            }
        }

        private async Task LoadCustomersAndPaymentMethodsAsync()
        {
            try
            {
                var connection = _journalService.Database.Connection;
                
                // Load payment methods
                var list = await connection.Table<PosPaymentMethod>().ToListAsync();
                PaymentMethods = new ObservableCollection<PosPaymentMethod>(list);
                if (SelectedPaymentMethod == null && list.Count > 0)
                {
                    SelectedPaymentMethod = list[0];
                }

                // Initial customers list
                var allCusts = await _customerService.GetAllCustomersAsync();
                MatchedCustomers.Clear();
                foreach (var c in allCusts.Take(5))
                {
                    MatchedCustomers.Add(c);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load customer/payment data: {ex.Message}");
            }
        }

        async partial void OnCustomerSearchTextChanged(string value)
        {
            if (SelectedCustomer != null && value != SelectedCustomer.Name)
            {
                SelectedCustomer = null;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                MatchedCustomers.Clear();
                var allCusts = await _customerService.GetAllCustomersAsync();
                foreach (var c in allCusts.Take(5)) MatchedCustomers.Add(c);
                return;
            }

            var query = value.ToLower();
            var matched = await _customerService.GetAllCustomersAsync();
            var filtered = matched.Where(c => c.Name.ToLower().Contains(query)).Take(5).ToList();
            
            MatchedCustomers.Clear();
            foreach (var c in filtered) MatchedCustomers.Add(c);
        }

        [RelayCommand]
        private void SelectCustomer(Customer customer)
        {
            if (customer == null) return;
            SelectedCustomer = customer;
            CustomerSearchText = customer.Name;
            MatchedCustomers.Clear();
        }

        [RelayCommand]
        private void OpenCreateCustomerModal()
        {
            NewCustomerName = string.Empty;
            NewCustomerPhone = string.Empty;
            NewCustomerEmail = string.Empty;
            NewCustomerAddress = string.Empty;
            NewCustomerErrorMessage = string.Empty;
            IsCreateCustomerModalOpen = true;
        }

        [RelayCommand]
        private void CloseCreateCustomerModal()
        {
            IsCreateCustomerModalOpen = false;
        }

        [RelayCommand]
        private async Task CreateCustomerAsync()
        {
            if (string.IsNullOrWhiteSpace(NewCustomerName))
            {
                NewCustomerErrorMessage = "Name is required.";
                return;
            }

            try
            {
                var customer = new Customer
                {
                    Name = NewCustomerName.Trim(),
                    Phone = NewCustomerPhone.Trim(),
                    Email = NewCustomerEmail.Trim(),
                    Address = NewCustomerAddress.Trim(),
                    CreatedAt = DateTime.Now
                };
                await _customerService.AddCustomerAsync(customer);
                IsCreateCustomerModalOpen = false;

                // Select the newly created customer
                SelectCustomer(customer);
            }
            catch (Exception ex)
            {
                NewCustomerErrorMessage = $"Error: {ex.Message}";
            }
        }

        partial void OnActiveTabChanged(string value)
        {
            OnPropertyChanged(nameof(IsSalesTabActive));
            OnPropertyChanged(nameof(IsOrdersTabActive));
            OnPropertyChanged(nameof(IsPaymentMethodsTabActive));

            if (value == "Orders")
            {
                _ = LoadPosOrdersAsync();
            }
            else if (value == "PaymentMethods")
            {
                _ = LoadPaymentMethodsDataAsync();
            }
        }

        private async Task LoadPosOrdersAsync()
        {
            try
            {
                var list = await _salesOrderService.GetPosSalesOrdersAsync();
                PosOrders.Clear();
                foreach (var item in list)
                {
                    PosOrders.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load POS orders: {ex.Message}");
            }
        }

        private async Task LoadPaymentMethodsDataAsync()
        {
            try
            {
                var connection = _journalService.Database.Connection;
                
                var list = await connection.Table<PosPaymentMethod>().ToListAsync();
                PaymentMethods = new ObservableCollection<PosPaymentMethod>(list);

                var allJournals = await _journalService.GetAllJournalsAsync();
                var filteredJournals = allJournals.Where(j => j.Type == "Cash" || j.Type == "Bank" || j.Type == "Credit Card" || j.Type == "Miscellaneous").ToList();
                Journals = new ObservableCollection<Journal>(filteredJournals);
                if (NewPaymentMethodSelectedJournal == null && filteredJournals.Count > 0)
                {
                    NewPaymentMethodSelectedJournal = filteredJournals[0];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load Payment methods page: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CreatePaymentMethodAsync()
        {
            if (string.IsNullOrWhiteSpace(NewPaymentMethodName))
            {
                return;
            }

            if (NewPaymentMethodSelectedJournal == null)
            {
                return;
            }

            try
            {
                var method = new PosPaymentMethod
                {
                    Name = NewPaymentMethodName.Trim(),
                    JournalId = NewPaymentMethodSelectedJournal.Id
                };

                var connection = _journalService.Database.Connection;
                await connection.InsertAsync(method);

                NewPaymentMethodName = string.Empty;
                await LoadPaymentMethodsDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create payment method: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DeletePaymentMethodAsync(PosPaymentMethod method)
        {
            if (method == null) return;
            try
            {
                var connection = _journalService.Database.Connection;
                await connection.DeleteAsync(method);
                await LoadPaymentMethodsDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete payment method: {ex.Message}");
            }
        }

        private async Task EnsureWalkInCustomerExistsAsync()
        {
            try
            {
                await _customerService.EnsureWalkInCustomerAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to ensure Walk-in Customer: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task Checkout()
        {
            if (CartItems.Count == 0) return;

            if (SelectedPaymentMethod == null)
            {
                IsCheckoutSuccess = false;
                LastReceiptText = "Please select a payment method before completing the sale.";
                IsReceiptModalOpen = true;
                return;
            }

            try
            {
                var user = UserSession.CurrentUser?.Username ?? "Cashier";
                var connection = _journalService.Database.Connection;
                var postedTotal = CheckoutTotalAmount;
                var journalScale = TotalAmount > 0 ? postedTotal / TotalAmount : 1m;

                // Make sure we have a valid customer
                Customer? actualCustomer = SelectedCustomer;
                var customerId = actualCustomer?.Id ?? 0;
                if (customerId == 0)
                {
                    var walkIn = await _customerService.EnsureWalkInCustomerAsync();
                    customerId = walkIn.Id;
                    actualCustomer = walkIn;
                }

                // 1. Create SalesOrder record
                var orderNumber = await _salesOrderService.GeneratePosNumberAsync();

                var order = new SalesOrder
                {
                    SONumber = orderNumber,
                    CustomerId = customerId,
                    OrderDate = DateTime.Now,
                    QuotationDate = DateTime.Now,
                    TotalAmount = CheckoutTotalAmount,
                    Currency = ActiveCheckoutCurrency,
                    Status = "Delivered",
                    BillingStatus = AutoCreateInvoice ? "Invoiced" : "Waiting Invoice",
                    DeliveryStatus = "Delivered",
                    IsPosSale = true,
                    PosPaymentMethodId = SelectedPaymentMethod.Id,
                    CreatedByUsername = user
                };

                await connection.InsertAsync(order);

                if (_auditService != null)
                {
                    await _auditService.LogActionAsync(user, "Create", "SalesOrder", order.Id, order);
                }

                // 2. Process each item (insert SalesOrderItem, stock deduction, costing batch tracking)
                var itemsList = new List<SalesOrderItem>();
                foreach (var item in CartItems)
                {
                    var orderItem = new SalesOrderItem
                    {
                        SalesOrderId = order.Id,
                        ProductId = item.Product.Id,
                        QuantityOrdered = item.Quantity,
                        QuantityDelivered = item.Quantity,
                        QuantityInvoiced = AutoCreateInvoice ? item.Quantity : 0,
                        UnitPrice = item.UnitPrice,
                        TaxId = item.Product.SalesTaxId
                    };
                    await connection.InsertAsync(orderItem);
                    itemsList.Add(orderItem);

                    // Add Stock Movement OUT (reduces product stock quantity and decrements FIFO batch exactly once)
                    await _inventoryService.AddStockMovementAsync(
                        item.Product.Id, 
                        item.Quantity, 
                        "OUT", 
                        $"POS Sale: {order.SONumber}", 
                        user, 
                        customCost: null, 
                        unitPrice: item.UnitPrice,
                        postSalesRevenueJournal: !AutoCreateInvoice
                    );
                }

                // 3. Double Entry Accounting Entries
                if (AutoCreateInvoice)
                {
                    // a) Invoice Journal Entry
                    var salesJournal = await connection.Table<Journal>().Where(j => j.Type == "Sales").FirstOrDefaultAsync();
                    if (salesJournal != null)
                    {
                        var entryCount = await connection.Table<JournalEntry>().Where(e => e.JournalId == salesJournal.Id).CountAsync();
                        var entryNumber = $"{salesJournal.SequencePrefix}/{DateTime.Now.Year}/{(entryCount + 1):D5}";

                        var invoiceEntry = new JournalEntry
                        {
                            EntryNumber = entryNumber,
                            JournalId = salesJournal.Id,
                            Date = DateTime.Now,
                            Reference = $"POS Invoice: {order.SONumber}",
                            State = "Posted"
                        };
                        await connection.InsertAsync(invoiceEntry);

                        // Accounts
                        var arAccount = await connection.Table<Account>().Where(a => a.Code == "111000").FirstOrDefaultAsync();
                        int arAccountId = arAccount?.Id ?? 3; // Accounts Receivable

                        // Debit Accounts Receivable
                        await connection.InsertAsync(new JournalLine
                        {
                            JournalEntryId = invoiceEntry.Id,
                            AccountId = arAccountId,
                            Label = $"POS Invoice - {order.SONumber}",
                            Debit = postedTotal,
                            Credit = 0
                        });

                        // Credit Revenue for items
                        foreach (var item in CartItems)
                        {
                            int incomeAccountId = item.Product.IncomeAccountId ?? 0;
                            if (incomeAccountId == 0)
                            {
                                var revAccount = await connection.Table<Account>().Where(a => a.Code == "401000").FirstOrDefaultAsync();
                                incomeAccountId = revAccount?.Id ?? 13; // Product Sales Revenue
                            }

                            await connection.InsertAsync(new JournalLine
                            {
                                JournalEntryId = invoiceEntry.Id,
                                AccountId = incomeAccountId,
                                ProductId = item.Product.Id,
                                Label = $"POS Revenue - {item.Product.Name} (Qty: {item.Quantity})",
                                Debit = 0,
                                Credit = Math.Round(item.Subtotal * journalScale, 2)
                            });
                        }
                    }

                    // b) Payment Journal Entry (Debit cash/bank, Credit Accounts Receivable)
                    var paymentJournal = await connection.Table<Journal>().Where(j => j.Id == SelectedPaymentMethod.JournalId).FirstOrDefaultAsync();
                    if (paymentJournal != null)
                    {
                        var entryCount = await connection.Table<JournalEntry>().Where(e => e.JournalId == paymentJournal.Id).CountAsync();
                        var entryNumber = $"{paymentJournal.SequencePrefix}/{DateTime.Now.Year}/{(entryCount + 1):D5}";

                        var paymentEntry = new JournalEntry
                        {
                            EntryNumber = entryNumber,
                            JournalId = paymentJournal.Id,
                            Date = DateTime.Now,
                            Reference = $"POS Payment: {order.SONumber}",
                            State = "Posted"
                        };
                        await connection.InsertAsync(paymentEntry);

                        // Accounts
                        var arAccount = await connection.Table<Account>().Where(a => a.Code == "111000").FirstOrDefaultAsync();
                        int arAccountId = arAccount?.Id ?? 3; // Accounts Receivable

                        // Default/Bank Account for the payment journal
                        int cashAccountId = paymentJournal.DefaultAccountId ?? paymentJournal.BankAccountId ?? 0;
                        if (cashAccountId == 0)
                        {
                            var cashAccountObj = await connection.Table<Account>().Where(a => a.Code.StartsWith("101") || a.Code.StartsWith("102")).FirstOrDefaultAsync();
                            cashAccountId = cashAccountObj?.Id ?? 1; // Fallback to Cash/Bank
                        }

                        // Debit Cash/Bank
                        await connection.InsertAsync(new JournalLine
                        {
                            JournalEntryId = paymentEntry.Id,
                            AccountId = cashAccountId,
                            Label = $"POS Cash Inflow - {order.SONumber}",
                            Debit = postedTotal,
                            Credit = 0
                        });

                        // Credit Accounts Receivable (clears AR!)
                        await connection.InsertAsync(new JournalLine
                        {
                            JournalEntryId = paymentEntry.Id,
                            AccountId = arAccountId,
                            Label = $"POS Payment Receipt - {order.SONumber}",
                            Debit = 0,
                            Credit = postedTotal
                        });
                    }
                }

                // 4. Generate A4 Tax Invoice PDF or 80mm POS Receipt PDF
                if (AutoCreateInvoice)
                {
                    var allProducts = await _inventoryService.GetAllProductsAsync();
                    var allTaxes = await connection.Table<Tax>().ToListAsync();
                    var pdfService = new SalesOrderPdfService(_settingsService);
                    
                    LastReceiptPath = pdfService.GenerateSalesOrderPdf(
                        order, 
                        itemsList, 
                        allProducts, 
                        allTaxes, 
                        actualCustomer, 
                        asInvoice: true
                    );
                    LastReceiptText = $"Invoice Generated Successfully!\nSaved to: {LastReceiptPath}";
                }
                else
                {
                    LastReceiptPath = _receiptService.GenerateReceiptFromCart(user, CartItems, postedTotal, AmountPaid, ChangeDue);
                    LastReceiptText = $"Receipt Generated Successfully!\nSaved to: {LastReceiptPath}";
                }

                IsCheckoutSuccess = true;

                // Clear Cart
                CartItems.Clear();
                RecalculateTotal();
                AmountPaid = 0;
                IsPaymentPanelVisible = false;
                SelectedCustomer = null;
                CustomerSearchText = string.Empty;

                // Show Receipt Modal
                IsReceiptModalOpen = true;

                // Refresh Inventory List
                await LoadProducts(); 
            }
            catch (Exception ex)
            {
                IsCheckoutSuccess = false;
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

        [RelayCommand]
        private async Task OpenOrderDetails(SalesOrderListItem? item)
        {
            if (item == null) return;
            var so = item.SalesOrder;
            DetailedOrder = so;
            DetailedCustomerName = item.CustomerName;

            try
            {
                var dbItems = await _salesOrderService.GetItemsAsync(so.Id);
                var products = await _inventoryService.GetAllProductsAsync();
                var taxes = await _taxService.GetAllTaxesAsync();

                var rows = new List<POSDetailedOrderItemRow>();
                decimal subtotal = 0;
                decimal taxTotal = 0;
                var taxBreakdown = new Dictionary<int, (Tax Tax, decimal Amount)>();

                foreach (var it in dbItems)
                {
                    var product = products.FirstOrDefault(p => p.Id == it.ProductId);
                    var taxId = it.TaxId ?? product?.SalesTaxId;
                    var tax = taxId.HasValue ? taxes.FirstOrDefault(t => t.Id == taxId.Value) : null;

                    var rowSubtotal = it.QuantityOrdered * it.UnitPrice;
                    decimal rowTotal = rowSubtotal;
                    decimal taxAmount = 0;

                    if (tax != null)
                    {
                        if (tax.IncludedInPrice == "Include")
                        {
                            decimal basePrice;
                            if (tax.Computation == "Percentage")
                            {
                                basePrice = rowSubtotal / (1 + (tax.Amount / 100));
                            }
                            else
                            {
                                basePrice = Math.Max(0, rowSubtotal - (it.QuantityOrdered * tax.Amount));
                            }
                            taxAmount = rowSubtotal - basePrice;
                            subtotal += basePrice;
                            rowTotal = rowSubtotal;
                        }
                        else
                        {
                            subtotal += rowSubtotal;
                            if (tax.Computation == "Percentage")
                            {
                                taxAmount = rowSubtotal * (tax.Amount / 100);
                            }
                            else
                            {
                                taxAmount = it.QuantityOrdered * tax.Amount;
                            }
                            rowTotal = rowSubtotal + taxAmount;
                        }

                        if (taxAmount > 0)
                        {
                            if (taxBreakdown.ContainsKey(tax.Id))
                            {
                                var existing = taxBreakdown[tax.Id];
                                taxBreakdown[tax.Id] = (tax, existing.Amount + taxAmount);
                            }
                            else
                            {
                                taxBreakdown[tax.Id] = (tax, taxAmount);
                            }
                            taxTotal += taxAmount;
                        }
                    }
                    else
                    {
                        subtotal += rowSubtotal;
                    }

                    rows.Add(new POSDetailedOrderItemRow
                    {
                        ProductName = product?.Name ?? $"Product ID: {it.ProductId}",
                        Quantity = it.QuantityOrdered,
                        UnitPrice = it.UnitPrice,
                        Discount = 0,
                        TaxName = tax != null ? $"{tax.Name} ({tax.Amount}%)" : "None",
                        Total = rowTotal
                    });
                }

                DetailedOrderItems = new ObservableCollection<POSDetailedOrderItemRow>(rows);
                DetailedSubtotal = subtotal;
                DetailedTaxes = taxTotal;
                DetailedTotal = subtotal + taxTotal;

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

                var paymentRows = new List<POSDetailedPaymentRow>();
                if (so.PosPaymentMethodId.HasValue)
                {
                    var connection = _journalService.Database.Connection;
                    var pm = await connection.Table<PosPaymentMethod>().Where(p => p.Id == so.PosPaymentMethodId.Value).FirstOrDefaultAsync();
                    paymentRows.Add(new POSDetailedPaymentRow
                    {
                        Date = so.OrderDate,
                        PaymentMethod = pm?.Name ?? "Unknown POS Payment Method",
                        Amount = so.TotalAmount
                    });
                }
                else
                {
                    paymentRows.Add(new POSDetailedPaymentRow
                    {
                        Date = so.OrderDate,
                        PaymentMethod = "POS Cash/Bank",
                        Amount = so.TotalAmount
                    });
                }
                DetailedPayments = new ObservableCollection<POSDetailedPaymentRow>(paymentRows);

                IsOrderDetailsOpen = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading POS order details: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CloseOrderDetails()
        {
            IsOrderDetailsOpen = false;
        }
    }

    public class POSDetailedOrderItemRow
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; } = 0;
        public string TaxName { get; set; } = "None";
        public decimal Total { get; set; }
    }

    public class POSDetailedPaymentRow
    {
        public DateTime Date { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
