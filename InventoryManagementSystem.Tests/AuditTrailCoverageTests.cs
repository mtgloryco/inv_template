using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using InventoryManagementSystem.Services;
using Xunit;

namespace InventoryManagementSystem.Tests;

public class AuditTrailCoverageTests : IAsyncLifetime
{
    private readonly string _dbPath = TempFile.CreateDbPath();
    private DatabaseService _db = null!;
    private AuditService _audit = null!;
    private InventoryService _inventory = null!;
    private SalesOrderService _salesOrderService = null!;
    private UserService _userService = null!;
    private CurrencyService _currencyService = null!;
    private PurchaseOrderService _purchaseOrderService = null!;

    public async Task InitializeAsync()
    {
        _db = new DatabaseService(_dbPath);
        await _db.InitializeAsync();
        _audit = new AuditService(_db);
        var license = new LicenseService(_db, new HardwareIdService(), new LicenseCryptoService());
        await license.InitializeAsync();
        _inventory = new InventoryService(_db, license, _audit);
        _salesOrderService = new SalesOrderService(_db, _inventory, _audit);
        _userService = new UserService(_db, _audit);
        _currencyService = new CurrencyService(_db, _audit);
        _purchaseOrderService = new PurchaseOrderService(_db, _inventory, _audit);
        await _userService.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.CloseConnectionAsync();
        TempFile.DeleteDbFiles(_dbPath);
    }

    [Fact]
    public async Task InvoiceSalesOrderAsync_WritesInvoiceAuditLog()
    {
        var so = new SalesOrder
        {
            SONumber = "SO-AUDIT-1",
            CustomerId = 1,
            Status = "Delivered",
            BillingStatus = "Waiting Invoice",
            OrderDate = DateTime.Today,
            TotalAmount = 500
        };
        await _db.Connection.InsertAsync(so);

        await _salesOrderService.InvoiceSalesOrderAsync(so.Id);

        var logs = await _audit.GetAuditTrailAsync("SalesOrder", so.Id);
        var log = Assert.Single(logs, l => l.Action == "Invoice");
        Assert.Equal("SalesOrder", log.EntityType);
        Assert.Equal("System", log.ChangedByUsername);
    }

    [Fact]
    public async Task UpdateUserAccessAsync_WritesUserUpdateAuditLog()
    {
        var user = await _db.Connection.Table<User>().FirstAsync(u => u.Username == "admin");

        await _userService.UpdateUserAccessAsync(user, new[] { RolePermissions.ViewReports, RolePermissions.ManageInventory });

        var logs = await _audit.GetAuditTrailAsync("User", user.Id);
        var log = Assert.Single(logs, l => l.Action == "Update");
        Assert.Equal("User", log.EntityType);
        Assert.Contains("ManageInventory", log.NewValues);
    }

    [Fact]
    public async Task SaveRateAsync_WritesExchangeRateCreateAuditLog()
    {
        var rate = new ExchangeRate
        {
            FromCurrency = "USD",
            ToCurrency = "RWF",
            Rate = 1300m,
            EffectiveDate = DateTime.Today
        };

        await _currencyService.SaveRateAsync(rate);

        var logs = await _audit.GetAuditTrailAsync("ExchangeRate", rate.Id);
        var log = Assert.Single(logs, l => l.Action == "Create");
        Assert.Equal("ExchangeRate", log.EntityType);
    }

    [Fact]
    public async Task ReceivePurchaseOrderAsync_WritesReceiveAuditLogWithLandedCost()
    {
        var supplier = await _db.Connection.Table<Supplier>().FirstAsync();
        var product = new Product { Name = "PO Audit Item", SKU = "PO-AUD-1", Cost = 50, Price = 80, StockQuantity = 0 };
        await _db.Connection.InsertAsync(product);

        var po = new PurchaseOrder { SupplierId = supplier.Id, Status = "Approved", PONumber = "PO-AUDIT-1" };
        var poItem = new PurchaseOrderItem { ProductId = product.Id, QuantityOrdered = 5, UnitCost = 50 };
        await _purchaseOrderService.CreatePurchaseOrderAsync(po, new List<PurchaseOrderItem> { poItem });

        var items = await _purchaseOrderService.GetItemsAsync(po.Id);
        var item = items.First();

        await _purchaseOrderService.ReceivePurchaseOrderAsync(
            po.Id,
            new List<PurchaseReceiveLine> { new() { ItemId = item.Id, QuantityReceived = 5 } },
            new List<LandedCostInput> { new() { CostType = "Freight", Amount = 25m } });

        var logs = await _audit.GetAuditTrailAsync("PurchaseOrder", po.Id);
        var log = Assert.Single(logs, l => l.Action == "Receive");
        Assert.Equal("PurchaseOrder", log.EntityType);
        Assert.Contains(po.PONumber, log.NewValues);
        Assert.Contains("TotalLandedCost", log.NewValues);
    }
}
