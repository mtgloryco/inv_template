using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class ExpiryService
    {
        private readonly DatabaseService _databaseService;

        public ExpiryService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public class BatchExpiryInfo
        {
            public Product Product { get; set; } = new();
            public PurchaseBatch Batch { get; set; } = new();

            public int DaysRemaining { get; set; }
            public decimal ValueAtCost { get; set; }

            // Red/Orange/Green
            public string TrafficLight { get; set; } = "Green";
            public string ColorHex { get; set; } = "#10B981";

            public bool CanRecall => !string.IsNullOrWhiteSpace(Batch.BatchNumber);
        }

        public class WasteReportRow
        {
            public DateTime Date { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string BatchNumber { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal CostPerUnit { get; set; }
            public decimal TotalCost { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        public class WasteReportResult
        {
            public decimal TotalWasteCost { get; set; }
            public List<WasteReportRow> Rows { get; set; } = new();
        }

        public async Task<List<BatchExpiryInfo>> GetExpiringProductsAsync(int daysAhead = 30)
        {
            if (daysAhead < 0) daysAhead = 0;

            var today = DateTime.Now.Date;
            var expiryTo = today.AddDays(daysAhead);

            var batches = await _databaseService.Connection.Table<PurchaseBatch>()
                .Where(b => b.QuantityRemaining > 0 && b.ExpiryDate != null)
                .ToListAsync();

            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var productById = products.ToDictionary(p => p.Id, p => p);

            var rows = new List<BatchExpiryInfo>();
            foreach (var batch in batches)
            {
                if (!batch.ExpiryDate.HasValue) continue;

                var daysRemaining = (batch.ExpiryDate.Value.Date - today).Days;
                if (daysRemaining < 0) continue; // expired is handled separately
                if (batch.ExpiryDate.Value.Date > expiryTo) continue;

                if (!productById.TryGetValue(batch.ProductId, out var product)) continue;

                rows.Add(new BatchExpiryInfo
                {
                    Product = product,
                    Batch = batch,
                    DaysRemaining = daysRemaining,
                    ValueAtCost = batch.QuantityRemaining * batch.CostPerUnit,
                    TrafficLight = "Orange",
                    ColorHex = "#F59E0B"
                });
            }

            // closest first
            rows.Sort((a, b) => a.DaysRemaining.CompareTo(b.DaysRemaining));
            return rows;
        }

        public async Task<List<BatchExpiryInfo>> GetExpiredProductsAsync()
        {
            var today = DateTime.Now.Date;

            var batches = await _databaseService.Connection.Table<PurchaseBatch>()
                .Where(b => b.QuantityRemaining > 0 && b.ExpiryDate != null)
                .ToListAsync();

            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var productById = products.ToDictionary(p => p.Id, p => p);

            var rows = new List<BatchExpiryInfo>();
            foreach (var batch in batches)
            {
                if (!batch.ExpiryDate.HasValue) continue;

                var daysRemaining = (batch.ExpiryDate.Value.Date - today).Days;
                if (daysRemaining >= 0) continue; // expiring handled separately

                if (!productById.TryGetValue(batch.ProductId, out var product)) continue;

                rows.Add(new BatchExpiryInfo
                {
                    Product = product,
                    Batch = batch,
                    DaysRemaining = daysRemaining,
                    ValueAtCost = batch.QuantityRemaining * batch.CostPerUnit,
                    TrafficLight = "Red",
                    ColorHex = "#FF5252"
                });
            }

            // most overdue first (more negative first)
            rows.Sort((a, b) => a.DaysRemaining.CompareTo(b.DaysRemaining));
            return rows;
        }

        public async Task<(int RedCount, int OrangeCount, int GreenCount)> GetExpiryTrafficCountsAsync(int daysAhead = 30)
        {
            if (daysAhead < 0) daysAhead = 0;

            var today = DateTime.Now.Date;
            var expiryTo = today.AddDays(daysAhead);

            var batches = await _databaseService.Connection.Table<PurchaseBatch>()
                .Where(b => b.QuantityRemaining > 0 && b.ExpiryDate != null)
                .ToListAsync();

            int red = 0;
            int orange = 0;
            int green = 0;

            foreach (var batch in batches)
            {
                if (!batch.ExpiryDate.HasValue) continue;

                var daysRemaining = (batch.ExpiryDate.Value.Date - today).Days;
                if (daysRemaining < 0) red++;
                else if (batch.ExpiryDate.Value.Date <= expiryTo) orange++;
                else green++;
            }

            return (red, orange, green);
        }

        public async Task RecallBatchAsync(string batchNumber, string reason)
        {
            if (string.IsNullOrWhiteSpace(batchNumber)) return;

            // Recall consumes remaining quantity from that exact batch.
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var batch = conn.Table<PurchaseBatch>()
                    .FirstOrDefault(b => b.BatchNumber == batchNumber);
                if (batch == null) return;

                if (batch.QuantityRemaining <= 0) return;

                var product = conn.Find<Product>(batch.ProductId);
                if (product == null) return;

                // Mark first to reflect state even if failure happens later.
                batch.QualityStatus = "Recalled";
                conn.Update(batch);

                var username = UserSession.CurrentUser?.Username ?? "System";
                var recallReason = string.IsNullOrWhiteSpace(reason) ? "Batch recalled" : reason;

                var movement = new StockMovement
                {
                    ProductId = product.Id,
                    QuantityChanged = batch.QuantityRemaining,
                    MovementType = "OUT",
                    Reason = $"Batch Recalled: {recallReason}",
                    Date = DateTime.Now,
                    Username = username,
                    UnitPrice = product.Price
                };

                conn.Insert(movement);

                // Link this OUT movement to the recalled purchase batch for proper COGS tracking.
                var usage = new SaleBatchUsage
                {
                    StockMovementId = movement.Id,
                    PurchaseBatchId = batch.Id,
                    QuantityUsed = batch.QuantityRemaining,
                    CostPerUnit = batch.CostPerUnit
                };
                conn.Insert(usage);

                batch.QuantityRemaining = 0;
                conn.Update(batch);
            });
        }

        public async Task<List<string>> GetBatchTraceabilityAsync(string batchNumber)
        {
            // Minimal traceability output: received batch info + list of OUT movements that consumed it.
            if (string.IsNullOrWhiteSpace(batchNumber)) return new List<string>();

            var batch = await _databaseService.Connection.Table<PurchaseBatch>()
                .FirstOrDefaultAsync(b => b.BatchNumber == batchNumber);

            if (batch == null) return new List<string>();

            var product = await _databaseService.Connection.FindAsync<Product>(batch.ProductId);

            var events = new List<string>();
            events.Add($"Received: {batch.PurchaseDate:yyyy-MM-dd} | Supplier: Unknown | Qty: {batch.QuantityPurchased}");
            if (product != null)
            {
                events[0] = $"Received: {batch.PurchaseDate:yyyy-MM-dd} | Supplier: Unknown | Qty: {batch.QuantityPurchased} | Product: {product.Name}";
            }

            var usages = await _databaseService.Connection.Table<SaleBatchUsage>()
                .Where(u => u.PurchaseBatchId == batch.Id)
                .ToListAsync();

            foreach (var usage in usages.OrderByDescending(u => u.StockMovementId))
            {
                var movement = await _databaseService.Connection.FindAsync<StockMovement>(usage.StockMovementId);
                if (movement == null) continue;

                events.Add(
                    $"Used: {movement.Date:yyyy-MM-dd HH:mm} | Qty: {usage.QuantityUsed} | By: {movement.Username} | Reason: {movement.Reason}");
            }

            return events;
        }

        public async Task<WasteReportResult> GetWasteReportAsync(DateTime from, DateTime to)
        {
            if (to < from)
            {
                var tmp = from;
                from = to;
                to = tmp;
            }

            var fromDate = from.Date;
            var toDate = to.Date.AddDays(1); // inclusive

            var recalledMovements = await _databaseService.Connection.Table<StockMovement>()
                .Where(m => m.MovementType == "OUT")
                .ToListAsync();

            // Filter by date in-memory (safer for SQLite expression support)
            recalledMovements = recalledMovements
                .Where(m => m.Date >= fromDate && m.Date < toDate && m.Reason.StartsWith("Batch Recalled:"))
                .ToList();

            var usageByMovementId = new Dictionary<int, List<SaleBatchUsage>>();
            var allMovementIds = recalledMovements.Select(m => m.Id).ToList();

            if (allMovementIds.Any())
            {
                var allUsages = await _databaseService.Connection.Table<SaleBatchUsage>()
                    .Where(u => allMovementIds.Contains(u.StockMovementId))
                    .ToListAsync();

                usageByMovementId = allUsages
                    .GroupBy(u => u.StockMovementId)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }

            var productIds = recalledMovements.Select(m => m.ProductId).Distinct().ToList();
            var products = await _databaseService.Connection.Table<Product>()
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();
            var productById = products.ToDictionary(p => p.Id, p => p);

            // Batch number mapping for the usages (by PurchaseBatchId)
            var batchIds = recalledMovements
                .SelectMany(m => usageByMovementId.TryGetValue(m.Id, out var list) ? list : Enumerable.Empty<SaleBatchUsage>())
                .Select(u => u.PurchaseBatchId)
                .Distinct()
                .ToList();

            var batches = await _databaseService.Connection.Table<PurchaseBatch>()
                .Where(b => batchIds.Contains(b.Id))
                .ToListAsync();
            var batchById = batches.ToDictionary(b => b.Id, b => b);

            var rows = new List<WasteReportRow>();
            decimal totalCost = 0;

            foreach (var movement in recalledMovements.OrderByDescending(m => m.Date))
            {
                var usages = usageByMovementId.TryGetValue(movement.Id, out var list) ? list : new List<SaleBatchUsage>();
                if (usages.Count == 0) continue;

                foreach (var usage in usages)
                {
                    if (!batchById.TryGetValue(usage.PurchaseBatchId, out var batch)) continue;
                    if (!productById.TryGetValue(movement.ProductId, out var product)) continue;

                    var total = usage.QuantityUsed * usage.CostPerUnit;
                    rows.Add(new WasteReportRow
                    {
                        Date = movement.Date,
                        ProductName = product.Name,
                        BatchNumber = batch.BatchNumber,
                        Quantity = usage.QuantityUsed,
                        CostPerUnit = usage.CostPerUnit,
                        TotalCost = total,
                        Reason = movement.Reason
                    });
                    totalCost += total;
                }
            }

            return new WasteReportResult
            {
                TotalWasteCost = totalCost,
                Rows = rows
            };
        }
    }
}

