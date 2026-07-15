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
    public partial class PurchaseOrdersViewModel : ViewModelBase
    {
        private readonly PurchaseOrderService _purchaseOrderService;
        private readonly SupplierService _supplierService;
        private readonly InventoryService _inventoryService;
        private readonly TaxService _taxService;
        private readonly SettingsService _settingsService;
        private readonly PurchaseOrderPdfService _pdfService;
        private readonly ReturnsService _returnsService;
        private readonly PaymentService _paymentService;
        private readonly CurrencyService _currencyService;
        private bool _isLoadingOrders;

        public LanguageService Language { get; }

        [ObservableProperty] private ObservableCollection<PurchaseOrderDisplayItem> _purchaseOrders = new();
        [ObservableProperty] private PurchaseOrderDisplayItem? _selectedPurchaseOrder;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        // Details Modal properties
        [ObservableProperty] private bool _isDetailsOpen;
        [ObservableProperty] private PurchaseOrder _detailedPo = new();
        [ObservableProperty] private Supplier? _detailedSupplier;
        [ObservableProperty] private ObservableCollection<PurchaseOrderItemDisplayRow> _detailedItems = new();
        [ObservableProperty] private string _detailedTaxBreakdownText = string.Empty;
        [ObservableProperty] private decimal _detailedSubtotal;
        [ObservableProperty] private decimal _detailedTotal;
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
        public string BaseCurrency => _settingsService.CurrentSettings.CurrencySymbol ?? "RWF";
        public bool CanRecordPayment => IsBilled && DetailedOpenBalance > 0.01m;

        // Return Modal properties
        [ObservableProperty] private bool _isReturnModalOpen;
        [ObservableProperty] private ObservableCollection<PurchaseOrderReturnRow> _returnRows = new();
        [ObservableProperty] private string _returnErrorMessage = string.Empty;

        // Receive Modal properties
        [ObservableProperty] private bool _isReceiveModalOpen;
        [ObservableProperty] private ObservableCollection<PurchaseReceiveRow> _receiveRows = new();
        [ObservableProperty] private decimal _landedCostFreight;
        [ObservableProperty] private decimal _landedCostDuties;
        [ObservableProperty] private string _receiveErrorMessage = string.Empty;

        public bool CanReturnDetailedPo => DetailedPo != null && DetailedPo.ReceiptStatus != "Pending" && DetailedPo.Status != "Cancelled";

        public bool IsBilled => DetailedPo?.BillingStatus == "Billed";
        public bool CanValidate => DetailedPo?.ReceiptStatus != "Received" && DetailedPo?.Status != "Draft";
        public bool CanCreateBill => DetailedPo?.ReceiptStatus != "Pending" && DetailedPo?.BillingStatus == "Waiting Bill";

        // Creation Modal properties
        [ObservableProperty] private bool _isCreateOpen;
        [ObservableProperty] private PurchaseOrder _newPo = new();
        [ObservableProperty] private string _vendorSearchText = string.Empty;

        partial void OnNewPoChanged(PurchaseOrder value)
        {
            OnPropertyChanged(nameof(NewPoOrderDeadline));
            OnPropertyChanged(nameof(NewPoExpectedDeliveryDate));
        }

        public DateTimeOffset? NewPoOrderDeadline
        {
            get => NewPo?.OrderDeadline != null ? new DateTimeOffset(NewPo.OrderDeadline.Value) : null;
            set
            {
                if (NewPo != null)
                {
                    NewPo.OrderDeadline = value?.DateTime;
                    OnPropertyChanged(nameof(NewPoOrderDeadline));
                }
            }
        }

        public DateTimeOffset? NewPoExpectedDeliveryDate
        {
            get => NewPo?.ExpectedDeliveryDate != null ? new DateTimeOffset(NewPo.ExpectedDeliveryDate.Value) : null;
            set
            {
                if (NewPo != null)
                {
                    NewPo.ExpectedDeliveryDate = value?.DateTime;
                    OnPropertyChanged(nameof(NewPoExpectedDeliveryDate));
                }
            }
        }
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private decimal _poTotalAmount;
        [ObservableProperty] private decimal _poSubtotalAmount;
        [ObservableProperty] private string _poTaxBreakdownText = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;

        private string _archiveFilter = "Active Orders";
        public string ArchiveFilter
        {
            get => _archiveFilter;
            set
            {
                if (SetProperty(ref _archiveFilter, value))
                {
                    LoadPurchaseOrdersCommand.Execute(null);
                }
            }
        }

        private string _companyFilter = "All Companies";
        public string CompanyFilter
        {
            get => _companyFilter;
            set
            {
                if (SetProperty(ref _companyFilter, value))
                {
                    LoadPurchaseOrdersCommand.Execute(null);
                }
            }
        }

        private DateTimeOffset? _filterStartDate;
        public DateTimeOffset? FilterStartDate
        {
            get => _filterStartDate;
            set
            {
                if (SetProperty(ref _filterStartDate, value))
                {
                    LoadPurchaseOrdersCommand.Execute(null);
                }
            }
        }

        private DateTimeOffset? _filterEndDate;
        public DateTimeOffset? FilterEndDate
        {
            get => _filterEndDate;
            set
            {
                if (SetProperty(ref _filterEndDate, value))
                {
                    LoadPurchaseOrdersCommand.Execute(null);
                }
            }
        }

        public List<Supplier> AllSuppliers { get; private set; } = new();
        public List<Product> AllProducts { get; private set; } = new();

        [ObservableProperty] private ObservableCollection<string> _availableCompanies = new() { "All Companies" };

        public List<string> ArchiveFilterOptions { get; } = new() { "Active Orders", "Archived Orders", "All Orders" };

        public List<string> Currencies { get; } = new() { "USD", "EUR", "GBP", "KES", "RWF", "UGX" };
        public List<string> PaymentTermsOptions { get; } = new() { "Immediate Payment", "15 days", "21 days", "30 days" };

        [ObservableProperty] private ObservableCollection<Supplier> _matchedSuppliers = new();
        [ObservableProperty] private bool _isCreateSupplierButtonVisible;

        [ObservableProperty] private ObservableCollection<PoItemRow> _poItems = new();
        [ObservableProperty] private ObservableCollection<Tax> _purchaseTaxes = new();

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

        public PurchaseOrdersViewModel(
            PurchaseOrderService purchaseOrderService,
            SupplierService supplierService,
            InventoryService inventoryService,
            TaxService taxService,
            SettingsService settingsService,
            ReturnsService returnsService,
            PaymentService paymentService,
            CurrencyService currencyService,
            LanguageService languageService)
        {
            _purchaseOrderService = purchaseOrderService;
            _supplierService = supplierService;
            _inventoryService = inventoryService;
            _taxService = taxService;
            _settingsService = settingsService;
            _returnsService = returnsService;
            _paymentService = paymentService;
            _currencyService = currencyService;
            Language = languageService;
            _pdfService = new PurchaseOrderPdfService(_settingsService);

            LoadPurchaseOrdersCommand.Execute(null);
        }

        [RelayCommand]
        public async Task LoadPurchaseOrders()
        {
            if (_isLoadingOrders) return;
            _isLoadingOrders = true;

            try
            {
                var list = await _purchaseOrderService.GetAllPurchaseOrdersAsync();
                
                // Exclude Draft and Sent statuses (RFQs) since they are handled in the RFQ tab
                var posOnly = list.Where(po => po.PurchaseOrder.Status != "Draft" && po.PurchaseOrder.Status != "Sent");

                // Update unique companies list
                var uniqueCompanies = posOnly
                    .Select(po => po.PurchaseOrder.Company)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                // Only update AvailableCompanies list if it has actually changed to avoid event storms
                bool listChanged = false;
                if (AvailableCompanies.Count != uniqueCompanies.Count + 1)
                {
                    listChanged = true;
                }
                else
                {
                    for (int i = 0; i < uniqueCompanies.Count; i++)
                    {
                        if (AvailableCompanies[i + 1] != uniqueCompanies[i])
                        {
                            listChanged = true;
                            break;
                        }
                    }
                }

                if (listChanged)
                {
                    var currentSelection = CompanyFilter;
                    AvailableCompanies.Clear();
                    AvailableCompanies.Add("All Companies");
                    foreach (var c in uniqueCompanies)
                    {
                        AvailableCompanies.Add(c);
                    }
                    if (AvailableCompanies.Contains(currentSelection))
                    {
                        _companyFilter = currentSelection;
                    }
                    else
                    {
                        _companyFilter = "All Companies";
                    }
                    OnPropertyChanged(nameof(CompanyFilter));
                }

                // 1. Archive Filtering
                if (ArchiveFilter == "Active Orders")
                {
                    posOnly = posOnly.Where(po => !po.PurchaseOrder.IsArchived);
                }
                else if (ArchiveFilter == "Archived Orders")
                {
                    posOnly = posOnly.Where(po => po.PurchaseOrder.IsArchived);
                }

                // 2. Company Filtering
                if (CompanyFilter != "All Companies" && !string.IsNullOrEmpty(CompanyFilter))
                {
                    posOnly = posOnly.Where(po => po.PurchaseOrder.Company == CompanyFilter);
                }

                // 3. Date Filtering
                if (FilterStartDate.HasValue)
                {
                    posOnly = posOnly.Where(po => po.PurchaseOrder.OrderDate >= FilterStartDate.Value.Date);
                }
                if (FilterEndDate.HasValue)
                {
                    posOnly = posOnly.Where(po => po.PurchaseOrder.OrderDate <= FilterEndDate.Value.Date.AddDays(1).AddTicks(-1));
                }

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var query = SearchText.ToLower();
                    posOnly = posOnly.Where(po =>
                        po.PurchaseOrder.PONumber.ToLower().Contains(query) ||
                        po.SupplierName.ToLower().Contains(query) ||
                        po.PurchaseOrder.Buyer.ToLower().Contains(query) ||
                        po.PurchaseOrder.Company.ToLower().Contains(query)
                    );
                }

                var displayItems = new List<PurchaseOrderDisplayItem>();
                foreach (var item in posOnly)
                {
                    displayItems.Add(new PurchaseOrderDisplayItem(item.PurchaseOrder, item.SupplierName));
                }

                PurchaseOrders = new ObservableCollection<PurchaseOrderDisplayItem>(displayItems);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading POs: {ex.Message}";
            }
            finally
            {
                _isLoadingOrders = false;
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            LoadPurchaseOrdersCommand.Execute(null);
        }

        [RelayCommand]
        private async Task OpenDetails(PurchaseOrderDisplayItem? displayItem)
        {
            if (displayItem == null) return;
            var po = displayItem.PurchaseOrder;

            try
            {
                DetailedPo = po;
                DetailedSupplier = await _supplierService.GetSupplierByIdAsync(po.SupplierId);
                
                var dbItems = await _purchaseOrderService.GetItemsAsync(po.Id);
                var products = await _inventoryService.GetAllProductsAsync();
                var taxes = await _taxService.GetAllTaxesAsync();

                var rows = new List<PurchaseOrderItemDisplayRow>();
                foreach (var it in dbItems)
                {
                    var product = products.FirstOrDefault(p => p.Id == it.ProductId);
                    var tax = taxes.FirstOrDefault(t => t.Id == it.TaxId);
                    rows.Add(new PurchaseOrderItemDisplayRow(it, product, tax));
                }

                DetailedItems = new ObservableCollection<PurchaseOrderItemDisplayRow>(rows);
                UpdateDetailedTotals(rows, taxes);

                OnPropertyChanged(nameof(IsBilled));
                OnPropertyChanged(nameof(CanValidate));
                OnPropertyChanged(nameof(CanCreateBill));
                OnPropertyChanged(nameof(CanReturnDetailedPo));
                OnPropertyChanged(nameof(CanRecordPayment));

                await LoadPaymentDetailsAsync();
                await UpdateDetailedBaseCurrencyEquivalentAsync();
                IsDetailsOpen = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening details: {ex.Message}";
            }
        }

        private void UpdateDetailedTotals(List<PurchaseOrderItemDisplayRow> rows, List<Tax> allTaxes)
        {
            decimal subtotal = 0;
            decimal total = 0;
            var taxBreakdown = new Dictionary<int, (Tax Tax, decimal Amount)>();

            foreach (var r in rows)
            {
                var rowSubtotal = r.QuantityOrdered * r.UnitCost;
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

        private async Task UpdateDetailedBaseCurrencyEquivalentAsync()
        {
            if (DetailedPo == null)
            {
                DetailedBaseCurrencyEquivalent = string.Empty;
                return;
            }

            var (_, label) = await _currencyService.TryFormatBaseEquivalentAsync(
                DetailedTotal,
                DetailedPo.Currency,
                BaseCurrency,
                DetailedPo.OrderDate);

            DetailedBaseCurrencyEquivalent = label;
        }

        private async Task LoadPaymentDetailsAsync()
        {
            if (DetailedPo == null || DetailedPo.BillingStatus != "Billed")
            {
                DetailedOpenBalance = 0;
                DetailedAmountPaid = 0;
                DetailedPayments = new ObservableCollection<InvoicePayment>();
                OnPropertyChanged(nameof(CanRecordPayment));
                return;
            }

            DetailedAmountPaid = await _paymentService.GetAmountPaidAsync("PurchaseOrder", DetailedPo.Id);
            DetailedOpenBalance = await _paymentService.GetOpenBalanceAsync("PurchaseOrder", DetailedPo.Id);
            var payments = await _paymentService.GetPaymentsForDocumentAsync("PurchaseOrder", DetailedPo.Id);
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
            if (DetailedPo == null) return;
            if (PaymentAmount <= 0)
            {
                PaymentErrorMessage = "Payment amount must be greater than zero.";
                return;
            }

            try
            {
                await _paymentService.RecordInvoicePaymentAsync(
                    "PurchaseOrder",
                    DetailedPo.Id,
                    PaymentAmount,
                    PaymentMethod,
                    UserSession.CurrentUser?.Username ?? "System",
                    reference: PaymentReference);

                IsPaymentModalOpen = false;
                await LoadPaymentDetailsAsync();
                StatusMessage = $"Recorded payment of {PaymentAmount:N2} {DetailedPo.Currency} for {DetailedPo.PONumber}";
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

        [RelayCommand]
        private void CloseDetails()
        {
            IsDetailsOpen = false;
        }

        [RelayCommand]
        private async Task CancelDetailedPo()
        {
            if (DetailedPo == null) return;
            try
            {
                if (DetailedPo.Status == "Received")
                {
                    StatusMessage = "Cannot cancel an order that has already been fully received.";
                    return;
                }

                await _purchaseOrderService.CancelPurchaseOrderAsync(DetailedPo.Id);
                StatusMessage = $"Cancelled purchase order {DetailedPo.PONumber}";

                // Refresh details
                var updatedPo = (await _purchaseOrderService.GetAllPurchaseOrdersAsync())
                    .FirstOrDefault(x => x.PurchaseOrder.Id == DetailedPo.Id);

                if (updatedPo != null)
                {
                    DetailedPo = updatedPo.PurchaseOrder;
                    await OpenDetails(new PurchaseOrderDisplayItem(updatedPo.PurchaseOrder, updatedPo.SupplierName));
                }

                OnPropertyChanged(nameof(IsBilled));
                OnPropertyChanged(nameof(CanValidate));
                OnPropertyChanged(nameof(CanCreateBill));
                OnPropertyChanged(nameof(CanCancelDetailedPo));
                OnPropertyChanged(nameof(DetailedPoIsArchived));
                OnPropertyChanged(nameof(ArchiveButtonText));

                await LoadPurchaseOrders();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Cancellation failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ToggleArchiveDetailedPo()
        {
            if (DetailedPo == null) return;
            try
            {
                bool targetArchive = !DetailedPo.IsArchived;
                await _purchaseOrderService.ArchivePurchaseOrderAsync(DetailedPo.Id, targetArchive);
                StatusMessage = targetArchive 
                    ? $"Archived purchase order {DetailedPo.PONumber}" 
                    : $"Unarchived purchase order {DetailedPo.PONumber}";

                // Refresh details
                var updatedPo = (await _purchaseOrderService.GetAllPurchaseOrdersAsync())
                    .FirstOrDefault(x => x.PurchaseOrder.Id == DetailedPo.Id);

                if (updatedPo != null)
                {
                    DetailedPo = updatedPo.PurchaseOrder;
                    await OpenDetails(new PurchaseOrderDisplayItem(updatedPo.PurchaseOrder, updatedPo.SupplierName));
                }

                OnPropertyChanged(nameof(DetailedPoIsArchived));
                OnPropertyChanged(nameof(ArchiveButtonText));
                await LoadPurchaseOrders();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Archiving failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteDetailedPo()
        {
            if (DetailedPo == null) return;
            try
            {
                bool deleted = await _purchaseOrderService.DeletePurchaseOrderAsync(DetailedPo.Id);
                if (deleted)
                {
                    StatusMessage = $"Deleted purchase order {DetailedPo.PONumber}";
                    IsDetailsOpen = false;
                    await LoadPurchaseOrders();
                }
                else
                {
                    StatusMessage = "Cannot delete this order because some quantities have been received or billed. Try archiving it instead.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
        }

        public bool DetailedPoIsArchived => DetailedPo?.IsArchived ?? false;
        public bool CanCancelDetailedPo => DetailedPo != null && DetailedPo.Status != "Cancelled" && DetailedPo.Status != "Received";
        public string ArchiveButtonText => DetailedPoIsArchived ? "Unarchive Order" : "Archive Order";

        [RelayCommand]
        private async Task ValidateReceipt()
        {
            if (DetailedPo == null) return;
            try
            {
                var dbItems = await _purchaseOrderService.GetItemsAsync(DetailedPo.Id);
                var products = await _inventoryService.GetAllProductsAsync();
                ReceiveRows.Clear();

                foreach (var item in dbItems)
                {
                    int remaining = item.QuantityOrdered - item.QuantityReceived;
                    if (remaining <= 0) continue;

                    var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                    ReceiveRows.Add(new PurchaseReceiveRow(item, product, remaining));
                }

                if (!ReceiveRows.Any())
                {
                    StatusMessage = "All goods already received.";
                    return;
                }

                LandedCostFreight = 0;
                LandedCostDuties = 0;
                ReceiveErrorMessage = string.Empty;
                IsReceiveModalOpen = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Receipt preparation failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ConfirmReceive()
        {
            if (DetailedPo == null) return;
            try
            {
                var receivedItems = new List<PurchaseReceiveLine>();
                foreach (var row in ReceiveRows)
                {
                    if (row.QuantityToReceive <= 0) continue;

                    BatchReceiveDetail? detail = null;
                    if (row.RequiresLotTracking || row.RequiresSerialTracking)
                    {
                        detail = new BatchReceiveDetail
                        {
                            BatchNumber = row.BatchNumber,
                            ExpiryDate = row.ExpiryDate?.DateTime
                        };
                        if (row.RequiresSerialTracking)
                        {
                            detail.SerialNumbers = row.SerialNumbers
                                .Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .ToList();
                        }
                    }

                    receivedItems.Add(new PurchaseReceiveLine
                    {
                        ItemId = row.ItemId,
                        QuantityReceived = row.QuantityToReceive,
                        BatchDetail = detail
                    });
                }

                var landedCosts = new List<LandedCostInput>();
                if (LandedCostFreight > 0) landedCosts.Add(new LandedCostInput { CostType = "Freight", Amount = LandedCostFreight });
                if (LandedCostDuties > 0) landedCosts.Add(new LandedCostInput { CostType = "Duties", Amount = LandedCostDuties });

                await _purchaseOrderService.ReceivePurchaseOrderAsync(DetailedPo.Id, receivedItems, landedCosts);
                StatusMessage = $"Validated and received goods for {DetailedPo.PONumber}";
                IsReceiveModalOpen = false;

                var updatedPo = (await _purchaseOrderService.GetAllPurchaseOrdersAsync())
                    .FirstOrDefault(x => x.PurchaseOrder.Id == DetailedPo.Id);

                if (updatedPo != null)
                {
                    DetailedPo = updatedPo.PurchaseOrder;
                    await OpenDetails(new PurchaseOrderDisplayItem(updatedPo.PurchaseOrder, updatedPo.SupplierName));
                }

                OnPropertyChanged(nameof(IsBilled));
                OnPropertyChanged(nameof(CanValidate));
                OnPropertyChanged(nameof(CanCreateBill));
                await LoadPurchaseOrders();
            }
            catch (Exception ex)
            {
                ReceiveErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void CancelReceive()
        {
            IsReceiveModalOpen = false;
            ReceiveErrorMessage = string.Empty;
        }

        [RelayCommand]
        private async Task CreateBill()
        {
            if (DetailedPo == null) return;
            try
            {
                await _purchaseOrderService.CreateBillAsync(DetailedPo.Id);
                StatusMessage = $"Created bill for {DetailedPo.PONumber}";

                var updatedPo = (await _purchaseOrderService.GetAllPurchaseOrdersAsync())
                    .FirstOrDefault(x => x.PurchaseOrder.Id == DetailedPo.Id);

                if (updatedPo != null)
                {
                    DetailedPo = updatedPo.PurchaseOrder;
                    await OpenDetails(new PurchaseOrderDisplayItem(updatedPo.PurchaseOrder, updatedPo.SupplierName));
                }

                OnPropertyChanged(nameof(IsBilled));
                OnPropertyChanged(nameof(CanValidate));
                OnPropertyChanged(nameof(CanCreateBill));
                OnPropertyChanged(nameof(CanRecordPayment));

                await LoadPaymentDetailsAsync();
                await UpdateDetailedBaseCurrencyEquivalentAsync();

                await LoadPurchaseOrders();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Billing failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task PrintDetailedPoPdf()
        {
            if (DetailedPo == null) return;
            try
            {
                var items = await _purchaseOrderService.GetItemsAsync(DetailedPo.Id);
                var products = await _inventoryService.GetAllProductsAsync();
                var taxes = await _taxService.GetAllTaxesAsync();

                var path = _pdfService.GeneratePurchaseOrderPdf(DetailedPo, items, products, taxes, DetailedSupplier, asBill: false);

                if (File.Exists(path))
                {
                    new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo(path)
                        {
                            UseShellExecute = true
                        }
                    }.Start();
                    StatusMessage = $"Opened PO PDF: {Path.GetFileName(path)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"PDF print failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task PrintDetailedBillPdf()
        {
            if (DetailedPo == null) return;
            try
            {
                var items = await _purchaseOrderService.GetItemsAsync(DetailedPo.Id);
                var products = await _inventoryService.GetAllProductsAsync();
                var taxes = await _taxService.GetAllTaxesAsync();

                var path = _pdfService.GeneratePurchaseOrderPdf(DetailedPo, items, products, taxes, DetailedSupplier, asBill: true);

                if (File.Exists(path))
                {
                    new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo(path)
                        {
                            UseShellExecute = true
                        }
                    }.Start();
                    StatusMessage = $"Opened Vendor Bill PDF: {Path.GetFileName(path)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"PDF print failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task OpenCreateForm()
        {
            ErrorMessage = string.Empty;
            NewPo = new PurchaseOrder
            {
                OrderDate = DateTime.Now,
                OrderDeadline = DateTime.Now.AddDays(7),
                ExpectedDeliveryDate = DateTime.Now.AddDays(14),
                Currency = _settingsService.CurrentSettings.CurrencySymbol ?? "USD",
                PaymentTerms = "Immediate Payment",
                Company = _settingsService.CurrentSettings.StoreName ?? "My Company",
                Buyer = UserSession.CurrentUser?.Username ?? "System",
                Status = "Approved", // Directly confirm it
                BillingStatus = "Waiting Bill",
                ReceiptStatus = "Pending"
            };

            SelectedSupplier = null;
            VendorSearchText = string.Empty;
            PoItems.Clear();

            try
            {
                AllSuppliers = await _supplierService.GetAllSuppliersAsync();
                AllProducts = await _inventoryService.GetAllProductsAsync();
                var taxes = await _taxService.GetAllTaxesAsync();
                PurchaseTaxes = new ObservableCollection<Tax>(taxes);

                MatchedSuppliers = new ObservableCollection<Supplier>(AllSuppliers.Take(5));
                IsCreateSupplierButtonVisible = false;

                // Add one default item row
                AddPoItemRow();

                IsCreateOpen = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading setup lists: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CancelCreateForm()
        {
            IsCreateOpen = false;
        }

        [RelayCommand]
        private void AddPoItemRow()
        {
            var row = new PoItemRow(AllProducts);
            row.OnChanged = UpdatePoTotal;
            row.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PoItemRow.TotalCost))
                {
                    UpdatePoTotal();
                }
            };
            row.RequestCreateProduct = () => OpenCreateProductFromRow(PoItems.IndexOf(row));
            PoItems.Add(row);
            UpdatePoTotal();
        }

        [RelayCommand]
        private void RemovePoItemRow(PoItemRow? row)
        {
            if (row == null) return;
            PoItems.Remove(row);
            UpdatePoTotal();
        }

        private void UpdatePoTotal()
        {
            decimal subtotal = 0;
            decimal total = 0;
            var taxBreakdown = new Dictionary<int, (Tax Tax, decimal Amount)>();

            foreach (var r in PoItems)
            {
                var rowSubtotal = r.Quantity * r.UnitCost;
                if (r.SelectedTax != null)
                {
                    decimal taxAmount = 0;
                    if (r.SelectedTax.IncludedInPrice == "Include")
                    {
                        decimal basePrice;
                        if (r.SelectedTax.Computation == "Percentage")
                        {
                            basePrice = rowSubtotal / (1 + (r.SelectedTax.Amount / 100));
                        }
                        else
                        {
                            basePrice = Math.Max(0, rowSubtotal - (r.Quantity * r.SelectedTax.Amount));
                        }
                        taxAmount = rowSubtotal - basePrice;
                        subtotal += basePrice;
                        total += rowSubtotal;
                    }
                    else
                    {
                        subtotal += rowSubtotal;
                        if (r.SelectedTax.Computation == "Percentage")
                        {
                            taxAmount = rowSubtotal * (r.SelectedTax.Amount / 100);
                        }
                        else
                        {
                            taxAmount = r.Quantity * r.SelectedTax.Amount;
                        }
                        total += rowSubtotal + taxAmount;
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
                else
                {
                    subtotal += rowSubtotal;
                    total += rowSubtotal;
                }
            }

            PoSubtotalAmount = subtotal;
            PoTotalAmount = total;

            if (taxBreakdown.Count == 0)
            {
                PoTaxBreakdownText = "Taxes: None";
            }
            else
            {
                var parts = taxBreakdown.Values.Select(tb => 
                    $"{tb.Tax.Name} ({tb.Tax.Amount}%): {tb.Amount:N2} {(tb.Tax.IncludedInPrice == "Include" ? "Incl." : "")}");
                PoTaxBreakdownText = "Taxes breakdown:\n" + string.Join("\n", parts);
            }
        }

        [RelayCommand]
        private async Task ConfirmSaveOrder()
        {
            ErrorMessage = string.Empty;

            if (SelectedSupplier == null)
            {
                ErrorMessage = "Vendor/Supplier is required.";
                return;
            }

            var itemsToSave = PoItems
                .Where(r => r.SelectedProduct != null)
                .Select(r => new PurchaseOrderItem
                {
                    ProductId = r.SelectedProduct!.Id,
                    QuantityOrdered = r.Quantity,
                    QuantityReceived = 0,
                    QuantityBilled = 0,
                    UnitCost = r.UnitCost,
                    TaxId = r.SelectedTax?.Id
                }).ToList();

            if (!itemsToSave.Any())
            {
                ErrorMessage = "Please add at least one product line.";
                return;
            }

            try
            {
                NewPo.SupplierId = SelectedSupplier.Id;
                NewPo.Status = "Approved"; // Directly approved/confirmed
                NewPo.BillingStatus = "Waiting Bill";
                NewPo.ReceiptStatus = "Pending";
                NewPo.CreatedByUsername = UserSession.CurrentUser?.Username ?? "System";

                await _purchaseOrderService.CreatePurchaseOrderAsync(NewPo, itemsToSave);

                IsCreateOpen = false;
                StatusMessage = $"Purchase Order {NewPo.PONumber} saved and confirmed.";
                await LoadPurchaseOrders();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save failed: {ex.Message}";
            }
        }

        partial void OnVendorSearchTextChanged(string value)
        {
            FilterSuppliers();
        }

        private void FilterSuppliers()
        {
            if (string.IsNullOrWhiteSpace(VendorSearchText))
            {
                MatchedSuppliers = new ObservableCollection<Supplier>(AllSuppliers.Take(5));
                IsCreateSupplierButtonVisible = false;
                return;
            }

            if (SelectedSupplier != null && VendorSearchText == SelectedSupplier.Name)
            {
                IsCreateSupplierButtonVisible = false;
                return;
            }

            var query = VendorSearchText.ToLower();
            var matches = AllSuppliers.Where(s => s.Name.ToLower().Contains(query)).Take(5).ToList();
            MatchedSuppliers = new ObservableCollection<Supplier>(matches);

            IsCreateSupplierButtonVisible = !AllSuppliers.Any(s => s.Name.Equals(VendorSearchText, StringComparison.OrdinalIgnoreCase));
        }

        [RelayCommand]
        private void SelectSupplier(Supplier supplier)
        {
            SelectedSupplier = supplier;
            VendorSearchText = supplier.Name;
        }

        [RelayCommand]
        private void RequestCreateSupplier()
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
                NewSupplierErrorMessage = "Supplier name is required.";
                return;
            }

            try
            {
                var s = new Supplier
                {
                    Name = NewSupplierName,
                    Phone = NewSupplierPhone,
                    Email = NewSupplierEmail,
                    IsActive = true
                };
                await _supplierService.AddSupplierAsync(s);

                AllSuppliers = await _supplierService.GetAllSuppliersAsync();
                SelectedSupplier = s;
                VendorSearchText = s.Name;
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

        private void OpenCreateProductFromRow(int rowIndex)
        {
            _triggerRowIndex = rowIndex;
            NewProductName = PoItems[rowIndex].ProductSearchText;
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

                AllProducts = await _inventoryService.GetAllProductsAsync();

                if (_triggerRowIndex >= 0 && _triggerRowIndex < PoItems.Count)
                {
                    PoItems[_triggerRowIndex].ProductSearchText = p.Name;
                    PoItems[_triggerRowIndex].SelectedProduct = p;
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

        [RelayCommand]
        private async Task OpenReturnOrder(PurchaseOrderDisplayItem? item)
        {
            var target = item ?? SelectedPurchaseOrder;
            if (target == null) return;

            ReturnErrorMessage = string.Empty;
            var orderItems = await _purchaseOrderService.GetItemsAsync(target.PurchaseOrder.Id);
            
            ReturnRows.Clear();
            foreach (var it in orderItems)
            {
                if (it.QuantityReceived <= 0) continue;

                var prod = AllProducts.FirstOrDefault(p => p.Id == it.ProductId);
                
                ReturnRows.Add(new PurchaseOrderReturnRow
                {
                    ItemId = it.Id,
                    ProductId = it.ProductId,
                    ProductName = prod?.Name ?? "Unknown Product",
                    QuantityReceived = it.QuantityReceived,
                    QuantityToReturn = it.QuantityReceived, // Default to full return
                    CreditAmount = it.QuantityReceived * it.UnitCost, // Default full refund cost
                    Reason = "Supplier Return"
                });
            }

            if (ReturnRows.Count == 0)
            {
                StatusMessage = "No received items found on this order to return.";
                return;
            }

            DetailedPo = target.PurchaseOrder;
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
            if (ReturnRows.Any(r => r.QuantityToReturn > r.QuantityReceived))
            {
                ReturnErrorMessage = "Return quantity cannot exceed received quantity.";
                return;
            }

            try
            {
                var payload = ReturnRows
                    .Where(r => r.QuantityToReturn > 0)
                    .Select(r => (r.ItemId, r.QuantityToReturn, r.Reason, r.CreditAmount))
                    .ToList();

                if (payload.Count == 0)
                {
                    ReturnErrorMessage = "Please specify at least one item and quantity to return.";
                    return;
                }

                await _returnsService.ProcessPurchaseOrderReturnAsync(DetailedPo.Id, payload, UserSession.CurrentUser?.Username ?? "System");
                IsReturnModalOpen = false;
                
                // Refresh Order Details
                var updatedPo = (await _purchaseOrderService.GetAllPurchaseOrdersAsync())
                    .FirstOrDefault(x => x.PurchaseOrder.Id == DetailedPo.Id);
                if (updatedPo != null)
                {
                    DetailedPo = updatedPo.PurchaseOrder;
                    await OpenDetails(new PurchaseOrderDisplayItem(updatedPo.PurchaseOrder, updatedPo.SupplierName));
                }

                await LoadPurchaseOrders();
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

    public class PurchaseOrderReturnRow : ObservableObject
    {
        public int ItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int QuantityReceived { get; set; }
        
        private int _quantityToReturn;
        public int QuantityToReturn
        {
            get => _quantityToReturn;
            set => SetProperty(ref _quantityToReturn, value);
        }

        private string _reason = "Supplier Return";
        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        private decimal _creditAmount;
        public decimal CreditAmount
        {
            get => _creditAmount;
            set => SetProperty(ref _creditAmount, value);
        }
    }

    public class PurchaseOrderDisplayItem
    {
        public PurchaseOrder PurchaseOrder { get; }
        public string SupplierName { get; }

        public string BillingStatusText => PurchaseOrder.BillingStatus;
        public string ExpectedArrivalText => PurchaseOrder.ExpectedDeliveryDate?.ToString("yyyy-MM-dd") ?? "N/A";
        
        public bool IsArrivalPassed => 
            PurchaseOrder.ExpectedDeliveryDate.HasValue && 
            PurchaseOrder.ExpectedDeliveryDate.Value.Date < DateTime.Now.Date && 
            PurchaseOrder.Status != "Received";

        public PurchaseOrderDisplayItem(PurchaseOrder po, string supplierName)
        {
            PurchaseOrder = po;
            SupplierName = supplierName;
        }
    }

    public class PurchaseOrderItemDisplayRow
    {
        public PurchaseOrderItem Item { get; }
        public Product? Product { get; }
        public Tax? Tax { get; }

        public string ProductName => Product?.Name ?? $"Product ID: {Item.ProductId}";
        public int QuantityOrdered => Item.QuantityOrdered;
        public int QuantityReceived => Item.QuantityReceived;
        public int QuantityBilled => Item.QuantityBilled;
        public decimal UnitCost => Item.UnitCost;
        public string TaxName => Tax != null ? $"{Tax.Name} ({Tax.Amount}%)" : "None";
        public decimal Amount => Item.TotalCost;

        public PurchaseOrderItemDisplayRow(PurchaseOrderItem item, Product? product, Tax? tax)
        {
            Item = item;
            Product = product;
            Tax = tax;
        }
    }

    public partial class PoItemRow : ObservableObject
    {
        [ObservableProperty] private string _productSearchText = string.Empty;
        [ObservableProperty] private ObservableCollection<Product> _matchedProducts = new();
        
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

        public PoItemRow(List<Product> allProducts)
        {
            _allProducts = allProducts;
            MatchedProducts = new ObservableCollection<Product>(_allProducts.Take(5));
        }

        partial void OnProductSearchTextChanged(string value)
        {
            FilterProducts();
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

    public partial class PurchaseReceiveRow : ObservableObject
    {
        public int ItemId { get; }
        public string ProductName { get; }
        public string Tracking { get; }
        public bool RequiresLotTracking { get; }
        public bool RequiresSerialTracking { get; }

        [ObservableProperty] private int _quantityToReceive;
        [ObservableProperty] private string _batchNumber = string.Empty;
        [ObservableProperty] private string _serialNumbers = string.Empty;
        [ObservableProperty] private DateTimeOffset? _expiryDate;

        public PurchaseReceiveRow(PurchaseOrderItem item, Product? product, int remainingQty)
        {
            ItemId = item.Id;
            ProductName = product?.Name ?? $"Product #{item.ProductId}";
            Tracking = product?.Tracking ?? ProductTrackingModes.ByQuantity;
            RequiresLotTracking = product != null && BatchTrackingService.RequiresLotTracking(product);
            RequiresSerialTracking = product != null && BatchTrackingService.RequiresSerialTracking(product);
            QuantityToReceive = remainingQty;
        }
    }
}
