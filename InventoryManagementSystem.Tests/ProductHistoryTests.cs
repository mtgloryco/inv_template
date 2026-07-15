using System;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using InventoryManagementSystem.Services;
using Xunit;

namespace InventoryManagementSystem.Tests;

public class ProductHistoryTests : IAsyncLifetime
{
    private readonly string _dbPath = TempFile.CreateDbPath();
    private DatabaseService _db = null!;
    private InventoryService _inventoryService = null!;
    private ProductHistoryService _historyService = null!;

    public async Task InitializeAsync()
    {
        _db = new DatabaseService(_dbPath);
        await _db.InitializeAsync();
        var license = new LicenseService(_db, new HardwareIdService(), new LicenseCryptoService());
        await license.InitializeAsync();
        _inventoryService = new InventoryService(_db, license, new AuditService(_db));
        _historyService = _inventoryService.ProductHistory;
    }

    public async Task DisposeAsync()
    {
        await _db.CloseConnectionAsync();
        TempFile.DeleteDbFiles(_dbPath);
    }

    [Fact]
    public async Task ProductHistory_ReturnsMovementsForSingleProduct()
    {
        var productA = new Product { Name = "History A", SKU = "HA-1", Cost = 10, Price = 20, StockQuantity = 5, ProductType = "Good" };
        var productB = new Product { Name = "History B", SKU = "HB-1", Cost = 10, Price = 20, StockQuantity = 5, ProductType = "Good" };
        await _db.Connection.InsertAsync(productA);
        await _db.Connection.InsertAsync(productB);

        await _inventoryService.AddStockMovementAsync(productA.Id, 2, "OUT", "POS Sale: SO-TEST-001", "cashier", unitPrice: 20);
        await _inventoryService.AddStockMovementAsync(productB.Id, 1, "OUT", "POS Sale: SO-TEST-002", "cashier", unitPrice: 20);

        var history = await _historyService.GetProductHistoryAsync(productA.Id);

        Assert.Single(history);
        Assert.Equal("Sale", history[0].Category);
        Assert.Equal("SO-TEST-001", history[0].DocumentReference);
    }

    [Fact]
    public async Task ProductHistory_LinksAdjustmentJournalLines()
    {
        var product = new Product { Name = "Adj Product", SKU = "ADJ-1", Cost = 25, Price = 40, StockQuantity = 10, ProductType = "Good" };
        await _db.Connection.InsertAsync(product);

        await _inventoryService.AddStockMovementAsync(product.Id, 3, "IN", "Manual stock correction", "admin", customCost: 25);

        var history = await _historyService.GetProductHistoryAsync(product.Id);
        var evt = Assert.Single(history);
        var journals = await _historyService.GetJournalLinesForMovementAsync(evt.Movement);

        Assert.NotEmpty(journals);
        Assert.Contains(journals, j => j.Label.Contains("Stock Adj IN", StringComparison.OrdinalIgnoreCase));
    }
}
