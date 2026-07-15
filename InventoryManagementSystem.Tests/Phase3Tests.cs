using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using InventoryManagementSystem.Services;
using Xunit;

namespace InventoryManagementSystem.Tests;

public class Phase3Tests : IAsyncLifetime
{
    private readonly string _dbPath = TempFile.CreateDbPath();
    private DatabaseService _db = null!;
    private InventoryService _inventoryService = null!;
    private PurchaseOrderService _purchaseOrderService = null!;
    private SalesOrderService _salesOrderService = null!;
    private ReturnsService _returnsService = null!;
    private ForecastingService _forecastingService = null!;
    private CycleCountService _cycleCountService = null!;
    private ManufacturingService _manufacturingService = null!;

    public async Task InitializeAsync()
    {
        _db = new DatabaseService(_dbPath);
        await _db.InitializeAsync();
        var license = new LicenseService(_db, new HardwareIdService(), new LicenseCryptoService());
        await license.InitializeAsync();
        var audit = new AuditService(_db);
        _inventoryService = new InventoryService(_db, license, audit);
        _purchaseOrderService = new PurchaseOrderService(_db, _inventoryService);
        _salesOrderService = new SalesOrderService(_db, _inventoryService);
        _returnsService = new ReturnsService(_db, audit);
        _forecastingService = new ForecastingService(_db);
        _cycleCountService = new CycleCountService(_db);
        _manufacturingService = new ManufacturingService(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.CloseConnectionAsync();
        TempFile.DeleteDbFiles(_dbPath);
    }

    [Fact]
    public async Task BatchTracking_SerialReceiveSellAndReturn_RoundTrip()
    {
        var product = new Product
        {
            Name = "Serial Widget",
            SKU = "SER-1",
            Price = 100,
            Cost = 40,
            StockQuantity = 0,
            Tracking = ProductTrackingModes.Serial
        };
        await _db.Connection.InsertAsync(product);

        var supplier = await _db.Connection.Table<Supplier>().FirstAsync();
        var po = new PurchaseOrder { SupplierId = supplier.Id, Status = "Approved", PONumber = "PO-SER-1" };
        var poItem = new PurchaseOrderItem { ProductId = product.Id, QuantityOrdered = 2, UnitCost = 40 };
        await _purchaseOrderService.CreatePurchaseOrderAsync(po, new List<PurchaseOrderItem> { poItem });

        var items = await _purchaseOrderService.GetItemsAsync(po.Id);
        var poItemDb = items.First(i => i.ProductId == product.Id);

        await _purchaseOrderService.ReceivePurchaseOrderAsync(po.Id, new List<PurchaseReceiveLine>
        {
            new()
            {
                ItemId = poItemDb.Id,
                QuantityReceived = 2,
                BatchDetail = new BatchReceiveDetail { SerialNumbers = new List<string> { "SN-A", "SN-B" } }
            }
        });

        var batches = await _db.Connection.Table<PurchaseBatch>().Where(b => b.ProductId == product.Id).ToListAsync();
        Assert.Equal(2, batches.Count);
        Assert.All(batches, b => Assert.Equal(1, b.QuantityRemaining));

        var customer = new Customer { Name = "Serial Buyer" };
        await _db.Connection.InsertAsync(customer);
        var so = new SalesOrder { CustomerId = customer.Id, Status = "Confirmed", SONumber = "SO-SER-1" };
        var soItem = new SalesOrderItem { ProductId = product.Id, QuantityOrdered = 1, UnitPrice = 100 };
        await _salesOrderService.CreateSalesOrderAsync(so, new List<SalesOrderItem> { soItem });
        var soItems = await _salesOrderService.GetItemsAsync(so.Id);
        var soItemDb = soItems.First();
        await _salesOrderService.DeliverSalesOrderAsync(so.Id, new List<(int, int)> { (soItemDb.Id, 1) });

        product = await _db.Connection.FindAsync<Product>(product.Id);
        Assert.Equal(1, product!.StockQuantity);

        await _returnsService.ProcessCustomerReturnAsync(new CustomerReturn
        {
            ReturnNumber = "RET-SER-1",
            ProductId = product.Id,
            Quantity = 1,
            Condition = "Resaleable",
            ProcessedByUsername = "tester"
        });

        product = await _db.Connection.FindAsync<Product>(product.Id);
        Assert.Equal(2, product!.StockQuantity);
    }

    [Fact]
    public async Task LandedCost_AllocatesFreightAcrossReceivedItems()
    {
        var p1 = new Product { Name = "Heavy", Cost = 100, Price = 150, StockQuantity = 0 };
        var p2 = new Product { Name = "Light", Cost = 50, Price = 80, StockQuantity = 0 };
        await _db.Connection.InsertAllAsync(new[] { p1, p2 });

        var supplier = await _db.Connection.Table<Supplier>().FirstAsync();
        var po = new PurchaseOrder { SupplierId = supplier.Id, Status = "Approved" };
        var items = new List<PurchaseOrderItem>
        {
            new() { ProductId = p1.Id, QuantityOrdered = 10, UnitCost = 100 },
            new() { ProductId = p2.Id, QuantityOrdered = 10, UnitCost = 50 }
        };
        await _purchaseOrderService.CreatePurchaseOrderAsync(po, items);
        var savedItems = await _purchaseOrderService.GetItemsAsync(po.Id);

        await _purchaseOrderService.ReceivePurchaseOrderAsync(po.Id, new List<PurchaseReceiveLine>
        {
            new() { ItemId = savedItems[0].Id, QuantityReceived = 10 },
            new() { ItemId = savedItems[1].Id, QuantityReceived = 10 }
        }, new List<LandedCostInput> { new() { CostType = "Freight", Amount = 150 } });

        var batchHeavy = await _db.Connection.Table<PurchaseBatch>().Where(b => b.ProductId == p1.Id).FirstAsync();
        var batchLight = await _db.Connection.Table<PurchaseBatch>().Where(b => b.ProductId == p2.Id).FirstAsync();

        Assert.Equal(110, batchHeavy.CostPerUnit);
        Assert.Equal(55, batchLight.CostPerUnit);

        var charges = await _db.Connection.Table<LandedCostCharge>().Where(c => c.PurchaseOrderId == po.Id).ToListAsync();
        Assert.Single(charges);
        Assert.Equal(150, charges[0].Amount);
    }

    [Fact]
    public async Task ReorderRules_UseLocationStockNotGlobalQuantity()
    {
        var location = await _db.Connection.Table<Location>().FirstAsync();
        var product = new Product { Name = "Loc Stock Item", Cost = 10, Price = 20, StockQuantity = 100 };
        await _db.Connection.InsertAsync(product);

        await _db.Connection.InsertAsync(new LocationStock
        {
            LocationId = location.Id,
            ProductId = product.Id,
            Quantity = 3,
            ReorderPoint = 5
        });

        var supplier = await _db.Connection.Table<Supplier>().FirstAsync();
        await _db.Connection.InsertAsync(new ReorderRule
        {
            ProductId = product.Id,
            LocationId = location.Id,
            PreferredSupplierId = supplier.Id,
            ReorderPoint = 5,
            ReorderQuantity = 20,
            LeadTimeDays = 3,
            SafetyStockDays = 2
        });

        var recs = await _forecastingService.GetReorderRecommendationsAsync();
        var rec = Assert.Single(recs, r => r.Product.Id == product.Id);
        Assert.Equal(location.Id, rec.LocationId);
        Assert.Equal(3, rec.LocationStockQuantity);
    }

    [Fact]
    public async Task CycleCount_CreateDraft_IncludesAllGoodProducts()
    {
        var location = await _db.Connection.Table<Location>().FirstAsync();
        var onCount = new Product { Name = "On Count", Cost = 10, Price = 20, StockQuantity = 5, ProductType = "Good" };
        var zeroStock = new Product { Name = "Zero At Location", Cost = 15, Price = 30, StockQuantity = 0, ProductType = "Good" };
        await _db.Connection.InsertAsync(onCount);
        await _db.Connection.InsertAsync(zeroStock);

        await _db.Connection.InsertAsync(new LocationStock
        {
            LocationId = location.Id,
            ProductId = onCount.Id,
            Quantity = 5
        });

        var count = await _cycleCountService.CreateDraftAsync(location.Id, "tester");
        var lines = await _cycleCountService.GetLinesAsync(count.Id);

        Assert.Contains(lines, l => l.ProductId == onCount.Id && l.SystemQuantity == 5);
        Assert.Contains(lines, l => l.ProductId == zeroStock.Id && l.SystemQuantity == 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cycleCountService.AddProductLineAsync(count.Id, onCount.Id));
    }

    [Fact]
    public async Task CycleCount_PostsVarianceAndJournal()
    {
        var location = await _db.Connection.Table<Location>().FirstAsync();
        var product = new Product { Name = "Count Item", Cost = 25, Price = 40, StockQuantity = 10 };
        await _db.Connection.InsertAsync(product);

        await _db.Connection.InsertAsync(new LocationStock
        {
            LocationId = location.Id,
            ProductId = product.Id,
            Quantity = 10
        });

        var count = await _cycleCountService.CreateDraftAsync(location.Id, "tester");
        var lines = await _cycleCountService.GetLinesAsync(count.Id);
        var line = Assert.Single(lines, l => l.ProductId == product.Id);
        await _cycleCountService.UpdateCountedQuantityAsync(line.Id, 8);
        await _cycleCountService.PostVariancesAsync(count.Id, "tester");

        var locStock = await _db.Connection.Table<LocationStock>()
            .FirstAsync(ls => ls.LocationId == location.Id && ls.ProductId == product.Id);
        Assert.Equal(8, locStock.Quantity);

        product = await _db.Connection.FindAsync<Product>(product.Id);
        Assert.Equal(8, product!.StockQuantity);

        var posted = await _db.Connection.FindAsync<CycleCount>(count.Id);
        Assert.Equal("Posted", posted!.Status);

        var journalLines = await _db.Connection.Table<JournalLine>()
            .Where(jl => jl.Label.Contains("Cycle count"))
            .ToListAsync();
        Assert.NotEmpty(journalLines);
    }

    [Fact]
    public void ManufacturingService_CalculateComponentQuantity_AppliesScrapAndYield()
    {
        var qty = ManufacturingService.CalculateComponentQuantity(
            bomOutputQty: 1,
            bomLineQty: 2,
            moTargetQty: 10,
            bomYieldPercent: 80,
            lineScrapPercent: 10,
            bomScrapPercent: 5);

        Assert.Equal(28.75, qty, 2);
    }

    [Fact]
    public async Task ManufacturingService_BuildExpectedLinesFromBom_UsesScrapYield()
    {
        var finished = new Product { Name = "Finished Good", Cost = 0, Price = 200, StockQuantity = 0 };
        var component = new Product { Name = "Component", Cost = 5, Price = 10, StockQuantity = 100 };
        await _db.Connection.InsertAllAsync(new[] { finished, component });

        var bom = new BillOfMaterial
        {
            ProductId = finished.Id,
            Quantity = 1,
            YieldPercent = 90,
            ScrapPercent = 0,
            Reference = "BOM-TEST"
        };
        await _db.Connection.InsertAsync(bom);
        await _db.Connection.InsertAsync(new BillOfMaterialLine
        {
            BillOfMaterialId = bom.Id,
            ProductId = component.Id,
            Quantity = 2,
            ScrapPercent = 5
        });

        var lines = await _manufacturingService.BuildExpectedLinesFromBomAsync(bom.Id, 9);
        var line = Assert.Single(lines);
        Assert.True(line.ExpectedQuantity > 20);
    }
}
