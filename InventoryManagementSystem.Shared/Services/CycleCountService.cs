using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using SQLite;

namespace InventoryManagementSystem.Services
{
    public class CycleCountService
    {
        private readonly DatabaseService _databaseService;

        public CycleCountService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<CycleCountListItem>> GetAllAsync()
        {
            var counts = await _databaseService.Connection.Table<CycleCount>()
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.CountDate)
                .ToListAsync();
            var locations = await _databaseService.Connection.Table<Location>().ToListAsync();
            var lines = await _databaseService.Connection.Table<CycleCountLine>().ToListAsync();

            return counts.Select(c =>
            {
                var countLines = lines.Where(l => l.CycleCountId == c.Id).ToList();
                return new CycleCountListItem
                {
                    CycleCount = c,
                    LocationName = locations.FirstOrDefault(l => l.Id == c.LocationId)?.Name ?? "Unknown",
                    LineCount = countLines.Count,
                    TotalVarianceUnits = countLines.Sum(l => l.Variance)
                };
            }).ToList();
        }

        public async Task<List<CycleCountLine>> GetLinesAsync(int cycleCountId) =>
            await _databaseService.Connection.Table<CycleCountLine>()
                .Where(l => l.CycleCountId == cycleCountId)
                .ToListAsync();

        public async Task<CycleCount> CreateDraftAsync(int locationId, string username, string? notes = null)
        {
            var location = await _databaseService.Connection.FindAsync<Location>(locationId)
                           ?? throw new InvalidOperationException("Location not found.");

            var locationStock = await _databaseService.Connection.Table<LocationStock>()
                .Where(ls => ls.LocationId == locationId && !ls.IsDeleted)
                .ToListAsync();

            var products = await _databaseService.Connection.Table<Product>()
                .Where(p => !p.IsDeleted)
                .ToListAsync();

            var count = new CycleCount
            {
                CountNumber = await GenerateCountNumberAsync(),
                LocationId = locationId,
                CountDate = DateTime.Today,
                Status = "Draft",
                CreatedByUsername = username,
                Notes = notes ?? string.Empty
            };

            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Insert(count);
                var stockByProduct = locationStock.ToDictionary(s => s.ProductId);
                foreach (var product in products.Where(p => p.ProductType == "Good"))
                {
                    var systemQty = stockByProduct.TryGetValue(product.Id, out var stock) ? stock.Quantity : 0;
                    conn.Insert(new CycleCountLine
                    {
                        CycleCountId = count.Id,
                        ProductId = product.Id,
                        SystemQuantity = systemQty,
                        CountedQuantity = systemQty
                    });
                }
            });

            return count;
        }

        public async Task<CycleCountLine> AddProductLineAsync(int cycleCountId, int productId)
        {
            var count = await _databaseService.Connection.FindAsync<CycleCount>(cycleCountId)
                        ?? throw new InvalidOperationException("Cycle count not found.");
            if (count.Status == "Posted")
            {
                throw new InvalidOperationException("Cannot add products to a posted cycle count.");
            }

            var product = await _databaseService.Connection.FindAsync<Product>(productId)
                          ?? throw new InvalidOperationException("Product not found.");
            if (product.ProductType != "Good")
            {
                throw new InvalidOperationException("Only stockable goods can be added to a cycle count.");
            }

            var existing = await _databaseService.Connection.Table<CycleCountLine>()
                .FirstOrDefaultAsync(l => l.CycleCountId == cycleCountId && l.ProductId == productId);
            if (existing != null)
            {
                throw new InvalidOperationException($"{product.Name} is already on this cycle count.");
            }

            var locStock = await _databaseService.Connection.Table<LocationStock>()
                .FirstOrDefaultAsync(ls => ls.LocationId == count.LocationId && ls.ProductId == productId && !ls.IsDeleted);
            var systemQty = locStock?.Quantity ?? 0;

            var line = new CycleCountLine
            {
                CycleCountId = cycleCountId,
                ProductId = productId,
                SystemQuantity = systemQty,
                CountedQuantity = systemQty
            };
            await _databaseService.Connection.InsertAsync(line);
            return line;
        }

        public async Task UpdateCountedQuantityAsync(int lineId, int countedQuantity)
        {
            var line = await _databaseService.Connection.FindAsync<CycleCountLine>(lineId)
                       ?? throw new InvalidOperationException("Cycle count line not found.");
            line.CountedQuantity = countedQuantity;
            await _databaseService.Connection.UpdateAsync(line);
        }

        public async Task PostVariancesAsync(int cycleCountId, string username)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var count = conn.Find<CycleCount>(cycleCountId);
                if (count == null) throw new InvalidOperationException("Cycle count not found.");
                if (count.Status == "Posted") throw new InvalidOperationException("Cycle count already posted.");

                var lines = conn.Table<CycleCountLine>().Where(l => l.CycleCountId == cycleCountId).ToList();
                decimal totalVarianceValue = 0;

                foreach (var line in lines)
                {
                    var variance = line.CountedQuantity - line.SystemQuantity;
                    if (variance == 0) continue;

                    var product = conn.Find<Product>(line.ProductId);
                    if (product == null || product.ProductType != "Good") continue;

                    var locStock = conn.Table<LocationStock>()
                        .FirstOrDefault(ls => ls.LocationId == count.LocationId && ls.ProductId == line.ProductId);
                    if (locStock == null)
                    {
                        locStock = new LocationStock
                        {
                            LocationId = count.LocationId,
                            ProductId = line.ProductId,
                            Quantity = 0
                        };
                        conn.Insert(locStock);
                    }

                    locStock.Quantity = line.CountedQuantity;
                    SyncMetadataHelper.Touch(locStock);
                    conn.Update(locStock);
                    LocationStockSync.ReconcileProductStockFromLocations(conn, line.ProductId);

                    var movement = new StockMovement
                    {
                        ProductId = line.ProductId,
                        QuantityChanged = Math.Abs(variance),
                        MovementType = "ADJUST",
                        Reason = $"Cycle Count {count.CountNumber} variance",
                        Date = DateTime.Now,
                        Username = username
                    };
                    SyncMetadataHelper.Touch(movement);
                    conn.Insert(movement);

                    if (variance > 0)
                    {
                        BatchTrackingService.CreateBatchesOnReceive(
                            conn, product, variance, product.Cost, DateTime.Now,
                            new BatchReceiveDetail { BatchNumber = $"CC-{count.CountNumber}-{line.ProductId}" },
                            $"CC-{count.CountNumber}");
                    }
                    else
                    {
                        BatchTrackingService.DeductBatchesOnIssue(conn, product, Math.Abs(variance), movement.Id);
                    }

                    totalVarianceValue += Math.Abs(variance) * product.Cost;
                }

                PostVarianceJournal(conn, count, totalVarianceValue);
                count.Status = "Posted";
                SyncMetadataHelper.Touch(count);
                conn.Update(count);
            });
        }

        private static void PostVarianceJournal(SQLiteConnection conn, CycleCount count, decimal totalVarianceValue)
        {
            if (totalVarianceValue <= 0) return;

            var journal = conn.Table<Journal>().FirstOrDefault(j => j.SequencePrefix == "STJ")
                          ?? conn.Table<Journal>().FirstOrDefault(j => j.Type == "Miscellaneous");
            if (journal == null) return;

            var entryCount = conn.Table<JournalEntry>().Count(e => e.JournalId == journal.Id);
            var entry = new JournalEntry
            {
                EntryNumber = $"{journal.SequencePrefix}/{DateTime.Now.Year}/{(entryCount + 1):D5}",
                JournalId = journal.Id,
                Date = DateTime.Now,
                Reference = $"Cycle Count: {count.CountNumber}",
                State = "Posted"
            };
            conn.Insert(entry);

            var inventoryAccount = conn.Table<Account>().FirstOrDefault(a => a.Code == "120000");
            var adjAccount = conn.Table<Account>().FirstOrDefault(a => a.Code == "520000");
            int assetId = inventoryAccount?.Id ?? 4;
            int offsetId = adjAccount?.Id ?? 18;

            var netVariance = conn.Table<CycleCountLine>()
                .Where(l => l.CycleCountId == count.Id)
                .ToList()
                .Sum(l =>
                {
                    var p = conn.Find<Product>(l.ProductId);
                    if (p == null) return 0m;
                    return (l.CountedQuantity - l.SystemQuantity) * p.Cost;
                });

            if (netVariance > 0)
            {
                conn.Insert(new JournalLine { JournalEntryId = entry.Id, AccountId = assetId, Label = "Cycle count gain", Debit = netVariance, Credit = 0 });
                conn.Insert(new JournalLine { JournalEntryId = entry.Id, AccountId = offsetId, Label = "Cycle count gain", Debit = 0, Credit = netVariance });
            }
            else if (netVariance < 0)
            {
                var loss = Math.Abs(netVariance);
                conn.Insert(new JournalLine { JournalEntryId = entry.Id, AccountId = offsetId, Label = "Cycle count loss", Debit = loss, Credit = 0 });
                conn.Insert(new JournalLine { JournalEntryId = entry.Id, AccountId = assetId, Label = "Cycle count loss", Debit = 0, Credit = loss });
            }
        }

        private async Task<string> GenerateCountNumberAsync()
        {
            var year = DateTime.Now.Year;
            var count = await _databaseService.Connection.Table<CycleCount>().CountAsync();
            return $"CC-{year}-{(count + 1):D4}";
        }
    }
}
