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
        private readonly LicenseService _licenseService;
        private readonly AuditService _auditService;

        public InventoryService(DatabaseService databaseService, LicenseService licenseService, AuditService auditService)
        {
            _databaseService = databaseService;
            _licenseService = licenseService;
            _auditService = auditService;
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
            var count = await GetTotalProductCountAsync();
            if (count >= _licenseService.GetMaxProductCount())
            {
                 throw new InvalidOperationException($"Product limit reached for your {_licenseService.CurrentLicense.Type} license. Upgrade to add more.");
            }

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
                        PurchaseDate = DateTime.Now,
                        BatchNumber = $"BATCH-INIT-{product.Id}-{Guid.NewGuid():N}",
                        QualityStatus = "Good",
                        ExpiryDate = null
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

            await _auditService.LogActionAsync(
                UserSession.CurrentUser?.Username ?? "System",
                "CREATE",
                "Product",
                product.Id,
                product
            );
        }

        public async Task AddProductsAsync(IEnumerable<Product> products)
        {
             var currentCount = await GetTotalProductCountAsync();
             var newCount = products.Count();
             if (currentCount + newCount > _licenseService.GetMaxProductCount())
             {
                 throw new InvalidOperationException($"Import would exceed product limit for your {_licenseService.CurrentLicense.Type} license.");
             }

            await _databaseService.Connection.InsertAllAsync(products);
        }

        public async Task BulkAddProductsWithMovementsAsync(IEnumerable<Product> products, string user, string reasonPrefix)
        {
             var currentCount = await GetTotalProductCountAsync();
             var newCount = products.Count();
             if (currentCount + newCount > _licenseService.GetMaxProductCount())
             {
                 throw new InvalidOperationException($"Import would exceed product limit for your {_licenseService.CurrentLicense.Type} license.");
             }

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
                            PurchaseDate = DateTime.Now,
                            BatchNumber = $"BATCH-INIT-{product.Id}-{Guid.NewGuid():N}",
                            QualityStatus = "Good",
                            ExpiryDate = null
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
        public async Task AddStockMovementAsync(
            int productId,
            int quantity,
            string type,
            string reason,
            string user,
            decimal? customCost = null,
            decimal? unitPrice = null,
            string? batchNumber = null,
            DateTime? expiryDate = null,
            string? qualityStatus = null)
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
                        PurchaseDate = DateTime.Now,
                        BatchNumber = string.IsNullOrWhiteSpace(batchNumber)
                            ? $"BATCH-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"
                            : batchNumber,
                        ExpiryDate = expiryDate,
                        QualityStatus = string.IsNullOrWhiteSpace(qualityStatus) ? "Good" : qualityStatus
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

                    decimal cogsAmount = 0;
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

                        cogsAmount += deductFromThisBatch * batch.CostPerUnit;

                        batch.QuantityRemaining -= deductFromThisBatch;
                        conn.Update(batch);

                        remainingToDeduct -= deductFromThisBatch;
                    }

                    if (remainingToDeduct > 0)
                    {
                        throw new InvalidOperationException("Insufficient batch stock.");
                    }

                    // --- Post Journal Entry for Sales ---
                    var journal = conn.Table<Journal>().Where(j => j.Type == "Sales").FirstOrDefault()
                                  ?? conn.Table<Journal>().Where(j => j.Name == "Point of Sale").FirstOrDefault();
                    if (journal != null)
                    {
                        var entryCount = conn.Table<JournalEntry>().Where(e => e.JournalId == journal.Id).Count();
                        var entryNumber = $"{journal.SequencePrefix}/{DateTime.Now.Year}/{(entryCount + 1):D5}";

                        var entry = new JournalEntry
                        {
                            EntryNumber = entryNumber,
                            JournalId = journal.Id,
                            Date = DateTime.Now,
                            Reference = $"POS Sale - {movement.Id}",
                            State = "Posted"
                        };
                        conn.Insert(entry);

                        // Debit Cash on Hand (101000)
                        var cashAccount = conn.Table<Account>().Where(a => a.Code == "101000").FirstOrDefault();
                        int debitAccountId = cashAccount?.Id ?? 1;
                        decimal revenueAmount = quantity * movement.UnitPrice;

                        conn.Insert(new JournalLine
                        {
                            JournalEntryId = entry.Id,
                            AccountId = debitAccountId,
                            ProductId = productId,
                            Label = $"POS Sale - {product.Name} (Qty: {quantity})",
                            Debit = revenueAmount,
                            Credit = 0
                        });

                        // Credit Income Account (product.IncomeAccountId or 401000 Product Sales Revenue)
                        int creditAccountId = product.IncomeAccountId ?? 0;
                        if (creditAccountId == 0)
                        {
                            var revAccount = conn.Table<Account>().Where(a => a.Code == "401000").FirstOrDefault();
                            creditAccountId = revAccount?.Id ?? 13;
                        }

                        conn.Insert(new JournalLine
                        {
                            JournalEntryId = entry.Id,
                            AccountId = creditAccountId,
                            ProductId = productId,
                            Label = $"POS Sale - {product.Name} (Qty: {quantity})",
                            Debit = 0,
                            Credit = revenueAmount
                        });

                        // COGS & Inventory Asset
                        if (product.ProductType == "Good" && cogsAmount > 0)
                        {
                            int cogsAccountId = product.ExpenseAccountId ?? 0;
                            if (cogsAccountId == 0)
                            {
                                var expAccount = conn.Table<Account>().Where(a => a.Code == "501000").FirstOrDefault();
                                cogsAccountId = expAccount?.Id ?? 16;
                            }

                            var inventoryAccount = conn.Table<Account>().Where(a => a.Code == "120000").FirstOrDefault();
                            int assetAccountId = inventoryAccount?.Id ?? 4;

                            conn.Insert(new JournalLine
                            {
                                JournalEntryId = entry.Id,
                                AccountId = cogsAccountId,
                                ProductId = productId,
                                Label = $"COGS - {product.Name} (Qty: {quantity})",
                                Debit = cogsAmount,
                                Credit = 0
                            });

                            conn.Insert(new JournalLine
                            {
                                JournalEntryId = entry.Id,
                                AccountId = assetAccountId,
                                ProductId = productId,
                                Label = $"Inventory Issue - {product.Name} (Qty: {quantity})",
                                Debit = 0,
                                Credit = cogsAmount
                            });
                        }
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

                    // Post Journal Entry for Adjustment/IN
                    var journal = conn.Table<Journal>().Where(j => j.SequencePrefix == "STJ").FirstOrDefault()
                                  ?? conn.Table<Journal>().Where(j => j.Type == "Miscellaneous").FirstOrDefault();
                    if (journal != null)
                    {
                        var entryCount = conn.Table<JournalEntry>().Where(e => e.JournalId == journal.Id).Count();
                        var entryNumber = $"{journal.SequencePrefix}/{DateTime.Now.Year}/{(entryCount + 1):D5}";

                        var entry = new JournalEntry
                        {
                            EntryNumber = entryNumber,
                            JournalId = journal.Id,
                            Date = DateTime.Now,
                            Reference = $"Adjustment: {reason}",
                            State = "Posted"
                        };
                        conn.Insert(entry);

                        decimal costPerUnit = customCost ?? product.Cost;
                        decimal totalVal = Math.Abs(quantity) * costPerUnit;

                        var inventoryAccount = conn.Table<Account>().Where(a => a.Code == "120000").FirstOrDefault();
                        int assetAccountId = inventoryAccount?.Id ?? 4;

                        var adjExpAccount = conn.Table<Account>().Where(a => a.Code == "520000").FirstOrDefault();
                        int offsetAccountId = adjExpAccount?.Id ?? 18;

                        if (type == "IN" || (type == "ADJUST" && quantity > 0))
                        {
                            conn.Insert(new JournalLine
                            {
                                JournalEntryId = entry.Id,
                                AccountId = assetAccountId,
                                ProductId = productId,
                                Label = $"Stock Adj IN - {product.Name} (Qty: {Math.Abs(quantity)})",
                                Debit = totalVal,
                                Credit = 0
                            });

                            conn.Insert(new JournalLine
                            {
                                JournalEntryId = entry.Id,
                                AccountId = offsetAccountId,
                                ProductId = productId,
                                Label = $"Stock Adj IN - {product.Name} (Qty: {Math.Abs(quantity)})",
                                Debit = 0,
                                Credit = totalVal
                            });
                        }
                        else if (type == "ADJUST" && quantity < 0)
                        {
                            conn.Insert(new JournalLine
                            {
                                JournalEntryId = entry.Id,
                                AccountId = offsetAccountId,
                                ProductId = productId,
                                Label = $"Stock Adj OUT - {product.Name} (Qty: {Math.Abs(quantity)})",
                                Debit = totalVal,
                                Credit = 0
                            });

                            conn.Insert(new JournalLine
                            {
                                JournalEntryId = entry.Id,
                                AccountId = assetAccountId,
                                ProductId = productId,
                                Label = $"Stock Adj OUT - {product.Name} (Qty: {Math.Abs(quantity)})",
                                Debit = 0,
                                Credit = totalVal
                            });
                        }
                    }
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

        // Product Unit UoM CRUD Methods
        public async Task<List<ProductUnit>> GetProductUnitsAsync()
        {
            return await _databaseService.Connection.Table<ProductUnit>().OrderBy(u => u.Name).ToListAsync();
        }

        public async Task AddProductUnitAsync(ProductUnit unit)
        {
            await _databaseService.Connection.InsertAsync(unit);
        }

        public async Task UpdateProductUnitAsync(ProductUnit unit)
        {
            await _databaseService.Connection.UpdateAsync(unit);
        }

        public async Task DeleteProductUnitAsync(int unitId)
        {
            var unit = await _databaseService.Connection.FindAsync<ProductUnit>(unitId);
            if (unit != null)
            {
                await _databaseService.Connection.DeleteAsync(unit);
            }
        }

        // Category CRUD methods
        public async Task<List<Category>> GetCategoriesAsync()
        {
            return await _databaseService.Connection.Table<Category>().OrderBy(c => c.Name).ToListAsync();
        }

        public async Task AddCategoryAsync(Category category)
        {
            await _databaseService.Connection.InsertAsync(category);
        }

        // Backend Invoicing Policy Enforcement validation
        public void ValidateInvoicingPolicy(Product product, int orderedQty, int deliveredQty, int toInvoiceQty)
        {
            if (product.InvoicingPolicy == "Delivered quantities")
            {
                if (toInvoiceQty > deliveredQty)
                {
                    throw new InvalidOperationException($"Invoicing policy constraint: Cannot invoice {toInvoiceQty} units for product '{product.Name}' because only {deliveredQty} units have been delivered.");
                }
            }
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
