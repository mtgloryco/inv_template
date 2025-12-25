using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class InventoryService
    {
        private readonly DatabaseService _databaseService;

        public InventoryService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        // Product CRUD
        public async Task<List<Product>> GetAllProductsAsync()
        {
            return await _databaseService.Connection.Table<Product>().ToListAsync();
        }

        public async Task<Product> GetProductByIdAsync(int id)
        {
            return await _databaseService.Connection.Table<Product>().Where(p => p.Id == id).FirstOrDefaultAsync();
        }

        public async Task AddProductAsync(Product product)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Insert(product);
                if (product.StockQuantity > 0)
                {
                    var batch = new PurchaseBatch
                    {
                        ProductId = product.Id,
                        QuantityPurchased = product.StockQuantity,
                        QuantityRemaining = product.StockQuantity,
                        CostPerUnit = product.Cost,
                        PurchaseDate = DateTime.Now
                    };
                    conn.Insert(batch);

                    var movement = new StockMovement
                    {
                        ProductId = product.Id,
                        QuantityChanged = product.StockQuantity,
                        MovementType = "IN",
                        Reason = "Initial Stock",
                        Date = DateTime.Now,
                        Username = "System"
                    };
                    conn.Insert(movement);
                }
            });
        }

        public async Task AddProductsAsync(IEnumerable<Product> products)
        {
            await _databaseService.Connection.InsertAllAsync(products);
        }

        public async Task BulkAddProductsWithMovementsAsync(IEnumerable<Product> products, string user, string reasonPrefix)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                foreach (var product in products)
                {
                    // Insert product
                    conn.Insert(product);

                    // If it has initial stock, log a movement and create a batch
                    if (product.StockQuantity != 0)
                    {
                        var batch = new PurchaseBatch
                        {
                            ProductId = product.Id,
                            QuantityPurchased = product.StockQuantity,
                            QuantityRemaining = product.StockQuantity,
                            CostPerUnit = product.Cost,
                            PurchaseDate = DateTime.Now
                        };
                        conn.Insert(batch);

                        var movement = new StockMovement
                        {
                            ProductId = product.Id,
                            QuantityChanged = product.StockQuantity,
                            MovementType = "IN",
                            Reason = $"{reasonPrefix}: Initial Import",
                            Date = DateTime.Now,
                            Username = user
                        };
                        conn.Insert(movement);
                    }
                }
            });
        }

        public async Task UpdateProductAsync(Product product)
        {
            await _databaseService.Connection.UpdateAsync(product);
        }

        public async Task DeleteProductAsync(Product product)
        {
            await _databaseService.Connection.DeleteAsync(product);
        }

        // Stock Movement
        public async Task AddStockMovementAsync(int productId, int quantity, string type, string reason, string user, decimal? customCost = null, decimal? unitPrice = null)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var product = conn.Find<Product>(productId);
                if (product == null) throw new Exception($"Product with ID {productId} not found.");

                if (quantity <= 0 && type != "ADJUST") throw new Exception("Quantity must be positive.");

                int newStock = product.StockQuantity;

                if (type == "IN")
                {
                    newStock += quantity;

                    // UPDATE MASTER COST if a new cost is provided
                    // This ensures future profitability calculations use the latest replacement cost
                    if (customCost.HasValue && customCost.Value > 0)
                    {
                        product.Cost = customCost.Value;
                    }

                    // UPDATE MASTER PRICE if provided
                    if (unitPrice.HasValue && unitPrice.Value > 0)
                    {
                        product.Price = unitPrice.Value;
                    }
                    // FALLBACK: If Price is still 0, but we have a Cost, set Price = Cost
                    // This prevents items from appearing as $0.00 in POS if the user forgot to set a Selling Price
                    else if (product.Price == 0 && customCost.HasValue && customCost.Value > 0)
                    {
                        product.Price = customCost.Value;
                    }
                    
                    // We must update the product record now to save the Cost/Price changes
                    conn.Update(product);

                    var batch = new PurchaseBatch
                    {
                        ProductId = productId,
                        QuantityPurchased = quantity,
                        QuantityRemaining = quantity,
                        CostPerUnit = customCost ?? product.Cost,
                        PurchaseDate = DateTime.Now
                    };
                    conn.Insert(batch);
                }
                else if (type == "OUT")
                {
                    if (product.StockQuantity < quantity)
                        throw new InvalidOperationException($"Insufficient stock for {product.Name}. Available: {product.StockQuantity}, Requested: {quantity}");

                    newStock -= quantity;

                    int remainingToDeduct = quantity;
                    var batches = conn.Table<PurchaseBatch>()
                                      .Where(b => b.ProductId == productId && b.QuantityRemaining > 0)
                                      .OrderBy(b => b.PurchaseDate)
                                      .ToList();

                    if (batches.Count == 0 && quantity > 0)
                    {
                        throw new InvalidOperationException("No purchase batches found. Batch tracking is required.");
                    }

                    var movement = new StockMovement
                    {
                        ProductId = productId,
                        QuantityChanged = quantity,
                        MovementType = type,
                        Reason = reason,
                        Date = DateTime.Now,
                        Username = user,
                        UnitPrice = unitPrice ?? product.Price
                    };
                    conn.Insert(movement);

                    foreach (var batch in batches)
                    {
                        if (remainingToDeduct <= 0) break;

                        int deductFromThisBatch = Math.Min(batch.QuantityRemaining, remainingToDeduct);

                        var usage = new SaleBatchUsage
                        {
                            StockMovementId = movement.Id,
                            PurchaseBatchId = batch.Id,
                            QuantityUsed = deductFromThisBatch,
                            CostPerUnit = batch.CostPerUnit
                        };
                        conn.Insert(usage);

                        batch.QuantityRemaining -= deductFromThisBatch;
                        conn.Update(batch);

                        remainingToDeduct -= deductFromThisBatch;
                    }

                    if (remainingToDeduct > 0)
                    {
                        throw new InvalidOperationException("Insufficient batch stock.");
                    }
                }
                else if (type == "ADJUST")
                {
                    newStock += quantity;
                }

                if (newStock < 0) throw new InvalidOperationException("Negative stock detected.");

                product.StockQuantity = newStock;
                conn.Update(product);

                if (type != "OUT")
                {
                    var movement = new StockMovement
                    {
                        ProductId = productId,
                        QuantityChanged = quantity,
                        MovementType = type,
                        Reason = reason,
                        Date = DateTime.Now,
                        Username = user
                    };
                    conn.Insert(movement);
                }
            });
        }

        // --- Reporting & Dashboard ---

        public async Task<int> GetTotalProductCountAsync()
        {
            return await _databaseService.Connection.Table<Product>().CountAsync();
        }

        public async Task<List<Product>> GetLowStockProductsAsync(int threshold = 5)
        {
            return await _databaseService.Connection.Table<Product>().Where(p => p.StockQuantity <= threshold).ToListAsync();
        }

        public async Task<List<StockMovement>> GetRecentStockMovementsAsync(int limit = 10)
        {
            var movements = await _databaseService.Connection.Table<StockMovement>()
                .OrderByDescending(m => m.Date)
                .Take(limit)
                .ToListAsync();

            // Populate Batch Trace Info for user transparency
            foreach (var m in movements)
            {
                if (m.MovementType == "OUT")
                {
                    var usages = await _databaseService.Connection.Table<SaleBatchUsage>()
                                       .Where(u => u.StockMovementId == m.Id)
                                       .ToListAsync();

                    if (usages.Count > 0)
                    {
                        var totalCost = usages.Sum(u => u.QuantityUsed * u.CostPerUnit);
                        var avgCost = totalCost / usages.Sum(u => u.QuantityUsed);
                        var profit = (m.UnitPrice * m.QuantityChanged) - totalCost;

                        // "Qty: 5 | Cost: $50.00 | Profit: $20.00"
                        m.BatchTraceInfo = $"Qty: {m.QuantityChanged} | Avg Cost: {avgCost:C} | Profit: {profit:C}";
                    }
                }
            }

            return movements;
        }

        public async Task<decimal> GetTotalInventoryValueAsync()
        {
            var batches = await _databaseService.Connection.Table<PurchaseBatch>()
                                .Where(b => b.QuantityRemaining > 0)
                                .ToListAsync();

            decimal totalValue = 0;
            foreach (var b in batches) totalValue += b.QuantityRemaining * b.CostPerUnit;
            return totalValue;
        }

        public async Task<List<MonthlyProfitReport>> GetMonthlyProfitSummaryAsync()
        {
            var movements = await _databaseService.Connection.Table<StockMovement>()
                                  .Where(m => m.MovementType == "OUT")
                                  .ToListAsync();

            var usages = await _databaseService.Connection.Table<SaleBatchUsage>().ToListAsync();
            var reports = new Dictionary<string, MonthlyProfitReport>();

            foreach (var m in movements)
            {
                var monthKey = m.Date.ToString("yyyy-MM");
                if (!reports.ContainsKey(monthKey)) reports[monthKey] = new MonthlyProfitReport { Month = monthKey };

                var report = reports[monthKey];
                report.Revenue += m.QuantityChanged * m.UnitPrice;

                foreach (var u in usages)
                {
                    if (u.StockMovementId == m.Id) report.COGS += u.QuantityUsed * u.CostPerUnit;
                }
            }

            var result = new List<MonthlyProfitReport>(reports.Values);
            result.Sort((a, b) => string.Compare(b.Month, a.Month, StringComparison.Ordinal));
            return result;
        }

        public async Task<FinancialOverview> GetFinancialOverviewAsync()
        {
            var reports = await GetMonthlyProfitSummaryAsync();
            var overview = new FinancialOverview();

            foreach (var r in reports)
            {
                overview.TotalRevenue += r.Revenue;
                overview.TotalCOGS += r.COGS;
                overview.TotalProfit += r.Profit;
            }
            overview.TotalInventoryValue = await GetTotalInventoryValueAsync();

            return overview;
        }
    }

    public class MonthlyProfitReport
    {
        public string Month { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal COGS { get; set; }
        public decimal Profit => Revenue - COGS;
    }

    public class FinancialOverview
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalCOGS { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal TotalInventoryValue { get; set; }
    }
}
