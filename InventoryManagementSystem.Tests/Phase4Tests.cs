using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using InventoryManagementSystem.Services;
using Xunit;

namespace InventoryManagementSystem.Tests;

public class Phase4Tests : IAsyncLifetime
{
    private readonly string _dbPath = TempFile.CreateDbPath();
    private DatabaseService _db = null!;
    private AuditService _audit = null!;
    private InventoryService _inventory = null!;
    private SalesOrderService _salesOrderService = null!;
    private PurchaseOrderService _purchaseOrderService = null!;
    private PaymentService _paymentService = null!;
    private AccountingReportService _accountingReport = null!;
    private IntegrationWebhookService _webhookService = null!;
    private NotificationService _notificationService = null!;
    private MonthCloseService _monthCloseService = null!;
    private AdvancedAnalyticsService _advancedAnalytics = null!;
    private CloudSyncService _cloudSync = null!;

    public async Task InitializeAsync()
    {
        _db = new DatabaseService(_dbPath);
        await _db.InitializeAsync();
        _audit = new AuditService(_db);
        var license = new LicenseService(_db, new HardwareIdService(), new LicenseCryptoService());
        await license.InitializeAsync();
        _inventory = new InventoryService(_db, license, _audit);
        _salesOrderService = new SalesOrderService(_db, _inventory, _audit);
        _purchaseOrderService = new PurchaseOrderService(_db, _inventory, _audit);
        _paymentService = new PaymentService(_db, _audit);
        _accountingReport = new AccountingReportService(_db);
        _webhookService = new IntegrationWebhookService(_db, _audit);
        _notificationService = new NotificationService(_db, _paymentService, _audit);
        _monthCloseService = new MonthCloseService(_db, _accountingReport, _paymentService, _audit);
        _advancedAnalytics = new AdvancedAnalyticsService(_db);
        _cloudSync = new CloudSyncService(_db, auditService: _audit);
    }

    public async Task DisposeAsync()
    {
        await _db.CloseConnectionAsync();
        TempFile.DeleteDbFiles(_dbPath);
    }

    [Fact]
    public async Task PosSale_PostsToGeneralLedger()
    {
        var product = new Product
        {
            Name = "POS GL Item",
            SKU = "POS-GL-1",
            Price = 200,
            Cost = 80,
            StockQuantity = 10,
            AvailableInPOS = true,
            CanBeSold = true
        };
        await _db.Connection.InsertAsync(product);

        await _inventory.AddStockMovementAsync(
            product.Id, 2, "OUT", "POS Sale - TEST", "tester",
            unitPrice: 200, postSalesRevenueJournal: true);

        var entries = await _db.Connection.Table<JournalEntry>().ToListAsync();
        Assert.NotEmpty(entries);

        var lines = await _db.Connection.Table<JournalLine>().ToListAsync();
        Assert.Contains(lines, l => l.Debit > 0);
        Assert.Contains(lines, l => l.Credit > 0);
        Assert.True(await _monthCloseService.ValidateTrialBalanceAsync());
    }

    [Fact]
    public async Task PurchaseReceiveAndBill_PostsToAccountsPayable()
    {
        var product = new Product { Name = "AP Item", Cost = 50, Price = 90, StockQuantity = 0 };
        await _db.Connection.InsertAsync(product);

        var supplier = await _db.Connection.Table<Supplier>().FirstAsync();
        var po = new PurchaseOrder { SupplierId = supplier.Id, Status = "Approved", TotalAmount = 500 };
        var poItem = new PurchaseOrderItem { ProductId = product.Id, QuantityOrdered = 10, UnitCost = 50 };
        await _purchaseOrderService.CreatePurchaseOrderAsync(po, new List<PurchaseOrderItem> { poItem });

        var items = await _purchaseOrderService.GetItemsAsync(po.Id);
        await _purchaseOrderService.ReceivePurchaseOrderAsync(po.Id, new List<PurchaseReceiveLine>
        {
            new() { ItemId = items[0].Id, QuantityReceived = 10 }
        });

        var receiptEntries = await _db.Connection.Table<JournalEntry>()
            .Where(e => e.Reference.Contains(po.PONumber) || e.Reference.Contains("PO"))
            .ToListAsync();
        Assert.NotEmpty(receiptEntries);

        await _purchaseOrderService.CreateBillAsync(po.Id);
        po = await _db.Connection.FindAsync<PurchaseOrder>(po.Id);
        Assert.Equal("Billed", po!.BillingStatus);

        await _paymentService.RecordInvoicePaymentAsync("PurchaseOrder", po.Id, 250, "Bank", "tester");
        var open = await _paymentService.GetOpenBalanceAsync("PurchaseOrder", po.Id);
        Assert.True(open > 0);
    }

    [Fact]
    public async Task MonthClose_SummaryIsBalancedAfterSalesAndPurchases()
    {
        var product = new Product { Name = "Close Item", Cost = 20, Price = 40, StockQuantity = 5 };
        await _db.Connection.InsertAsync(product);

        await _inventory.AddStockMovementAsync(product.Id, 1, "OUT", "POS Sale - CLOSE", "tester", unitPrice: 40, postSalesRevenueJournal: true);

        var now = DateTime.Today;
        var summary = await _monthCloseService.GetMonthCloseSummaryAsync(now.Year, now.Month);
        Assert.True(summary.IsBalanced);
        Assert.True(summary.PostedEntryCount > 0);

        var closed = await _monthCloseService.RunMonthCloseAsync(now.Year, now.Month, "tester");
        Assert.True(closed.IsBalanced);

        var logs = await _audit.GetAuditReportAsync(DateTime.Today, DateTime.Today.AddDays(1));
        Assert.Contains(logs, l => l.Action == "MonthClose");
    }

    [Fact]
    public async Task WebhookService_RegistersAndDispatchesEvents()
    {
        var endpoint = await _webhookService.RegisterEndpointAsync(new WebhookEndpoint
        {
            Name = "Test Hook",
            TargetUrl = string.Empty,
            EventTypes = "sales.invoiced",
            IsActive = true
        }, "tester");

        var delivered = await _webhookService.DispatchEventAsync("sales.invoiced", new { SONumber = "SO-WH-1", Total = 100m });
        Assert.Equal(0, delivered);

        var logs = await _db.Connection.Table<WebhookDeliveryLog>().ToListAsync();
        Assert.Single(logs);
        Assert.Equal(endpoint.Id, logs[0].WebhookEndpointId);

        var auditLogs = await _audit.GetAuditTrailAsync("WebhookEndpoint", endpoint.Id);
        Assert.Contains(auditLogs, l => l.Action == "Create");
    }

    [Fact]
    public async Task NotificationService_QueuesAndProcessesInvoiceEmail()
    {
        var customer = new Customer { Name = "Notify Buyer", Email = "buyer@example.com" };
        await _db.Connection.InsertAsync(customer);

        var so = new SalesOrder
        {
            SONumber = "SO-NOTIFY-1",
            CustomerId = customer.Id,
            Status = "Delivered",
            BillingStatus = "Invoiced",
            OrderDate = DateTime.Today,
            TotalAmount = 300,
            Currency = "RWF"
        };
        await _db.Connection.InsertAsync(so);

        var notification = await _notificationService.QueueInvoiceDeliveryAsync(so.Id);
        Assert.Equal("Pending", notification.Status);
        Assert.Equal("buyer@example.com", notification.Recipient);

        var sent = await _notificationService.ProcessPendingNotificationsAsync();
        Assert.Equal(1, sent);

        notification = await _db.Connection.FindAsync<NotificationOutbox>(notification.Id);
        Assert.Equal("Sent", notification!.Status);
    }

    [Fact]
    public async Task AdvancedAnalytics_MarginByCategoryAndAbcClassification()
    {
        var pA = new Product { Name = "A Product", Category = "Electronics", Cost = 10, Price = 100, StockQuantity = 5 };
        var pB = new Product { Name = "B Product", Category = "Electronics", Cost = 5, Price = 20, StockQuantity = 5 };
        var pC = new Product { Name = "C Product", Category = "Food", Cost = 2, Price = 5, StockQuantity = 5 };
        await _db.Connection.InsertAllAsync(new[] { pA, pB, pC });

        await _inventory.AddStockMovementAsync(pA.Id, 5, "OUT", "Sale A", "tester", unitPrice: 100000, postSalesRevenueJournal: true);
        await _inventory.AddStockMovementAsync(pB.Id, 1, "OUT", "Sale B", "tester", unitPrice: 100, postSalesRevenueJournal: true);

        var margins = await _advancedAnalytics.GetMarginByCategoryAsync();
        var electronics = margins.First(m => m.CategoryName == "Electronics");
        Assert.True(electronics.Revenue > 0);
        Assert.True(electronics.GrossProfit > 0);

        var abc = await _advancedAnalytics.GetAbcAnalysisAsync();
        var aItem = abc.First(l => l.ProductName == "A Product");
        Assert.Equal("A", aItem.Classification);
        Assert.True(aItem.Revenue > abc.First(l => l.ProductName == "B Product").Revenue);
    }

    [Fact]
    public async Task IntegrationExport_ProducesQuickBooksAndXeroCsv()
    {
        var product = new Product { Name = "Export Item", Cost = 10, Price = 25, StockQuantity = 3 };
        await _db.Connection.InsertAsync(product);
        await _inventory.AddStockMovementAsync(product.Id, 1, "OUT", "Export sale", "tester", unitPrice: 25, postSalesRevenueJournal: true);

        var from = DateTime.Today.AddDays(-1);
        var to = DateTime.Today.AddDays(1);
        var qb = await _webhookService.ExportJournalEntriesForQuickBooksAsync(from, to);
        var xero = await _webhookService.ExportJournalEntriesForXeroAsync(from, to);

        Assert.Contains("Date,Account,Debit,Credit", qb);
        Assert.Contains("*JournalDate,*AccountCode", xero);
    }

    [Fact]
    public async Task SyncConflict_ResolvePendingConflicts_AppliesServerWins()
    {
        var syncId = Guid.NewGuid();
        await _db.Connection.InsertAsync(new SyncConflictLog
        {
            EntityType = "Product",
            SyncId = syncId,
            LocalPayloadJson = "{\"Name\":\"Local\"}",
            ServerPayloadJson = "{\"SyncId\":\"" + syncId + "\",\"Name\":\"Server Product\",\"SKU\":\"SP-1\",\"Unit\":\"Pcs\",\"Price\":12.5,\"Cost\":7,\"StockQuantity\":3,\"Category\":\"General\",\"UpdatedAt\":\"" + DateTime.UtcNow.ToString("O") + "\",\"IsDeleted\":false}",
            Resolution = "Pending"
        });

        var resolved = await _cloudSync.ResolvePendingConflictsAsync("ServerWins", "tester");
        Assert.Equal(1, resolved);

        var conflict = await _db.Connection.Table<SyncConflictLog>().FirstAsync();
        Assert.Equal("ServerWins", conflict.Resolution);
        Assert.NotNull(conflict.ResolvedAt);
    }
}
