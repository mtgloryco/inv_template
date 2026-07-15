using System;
using System.Collections.Generic;
using System.Linq;
using InventoryManagementSystem.Domain;
using SQLite;

namespace InventoryManagementSystem.Services
{
    public static class ProductTrackingModes
    {
        public const string ByQuantity = "by quantity";
        public const string Lots = "lots";
        public const string Serial = "by unique serial number";
    }

    public class BatchReceiveDetail
    {
        public string BatchNumber { get; set; } = string.Empty;
        public List<string> SerialNumbers { get; set; } = new();
        public DateTime? ExpiryDate { get; set; }
    }

    public class PurchaseReceiveLine
    {
        public int ItemId { get; set; }
        public int QuantityReceived { get; set; }
        public BatchReceiveDetail? BatchDetail { get; set; }
    }

    public class LandedCostInput
    {
        public string CostType { get; set; } = "Freight";
        public decimal Amount { get; set; }
    }

    public static class BatchTrackingService
    {
        public static bool RequiresLotTracking(Product product) =>
            string.Equals(product.Tracking, ProductTrackingModes.Lots, StringComparison.OrdinalIgnoreCase);

        public static bool RequiresSerialTracking(Product product) =>
            string.Equals(product.Tracking, ProductTrackingModes.Serial, StringComparison.OrdinalIgnoreCase);

        public static bool RequiresTrackedBatches(Product product) =>
            RequiresLotTracking(product) || RequiresSerialTracking(product);

        public static void ValidateReceiveDetails(Product product, int quantity, BatchReceiveDetail? detail)
        {
            if (quantity <= 0) return;
            if (!RequiresTrackedBatches(product)) return;

            if (RequiresSerialTracking(product))
            {
                var serials = detail?.SerialNumbers?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList()
                              ?? new List<string>();
                if (serials.Count != quantity)
                {
                    throw new InvalidOperationException(
                        $"Product '{product.Name}' requires one serial number per unit. Expected {quantity}, got {serials.Count}.");
                }

                if (serials.Distinct(StringComparer.OrdinalIgnoreCase).Count() != serials.Count)
                {
                    throw new InvalidOperationException($"Duplicate serial numbers are not allowed for '{product.Name}'.");
                }
            }
            else if (RequiresLotTracking(product))
            {
                if (string.IsNullOrWhiteSpace(detail?.BatchNumber))
                {
                    throw new InvalidOperationException($"Product '{product.Name}' requires a lot/batch number on receipt.");
                }
            }
        }

        public static List<PurchaseBatch> CreateBatchesOnReceive(
            SQLiteConnection conn,
            Product product,
            int quantity,
            decimal costPerUnit,
            DateTime purchaseDate,
            BatchReceiveDetail? detail,
            string defaultBatchPrefix)
        {
            ValidateReceiveDetails(product, quantity, detail);
            var batches = new List<PurchaseBatch>();

            if (RequiresSerialTracking(product))
            {
                var serials = detail!.SerialNumbers.Select(s => s.Trim()).ToList();
                foreach (var serial in serials)
                {
                    EnsureSerialNotInStock(conn, product.Id, serial);
                    var batch = new PurchaseBatch
                    {
                        ProductId = product.Id,
                        QuantityPurchased = 1,
                        QuantityRemaining = 1,
                        CostPerUnit = costPerUnit,
                        PurchaseDate = purchaseDate,
                        BatchNumber = serial,
                        SerialNumber = serial,
                        ExpiryDate = detail.ExpiryDate,
                        QualityStatus = "Good"
                    };
                    conn.Insert(batch);
                    batches.Add(batch);
                }
            }
            else if (RequiresLotTracking(product))
            {
                var lotNumber = detail!.BatchNumber.Trim();
                var existing = conn.Table<PurchaseBatch>()
                    .FirstOrDefault(b => b.ProductId == product.Id && b.BatchNumber == lotNumber && b.QuantityRemaining > 0);
                if (existing != null)
                {
                    existing.QuantityPurchased += quantity;
                    existing.QuantityRemaining += quantity;
                    existing.CostPerUnit = costPerUnit;
                    if (detail.ExpiryDate.HasValue) existing.ExpiryDate = detail.ExpiryDate;
                    conn.Update(existing);
                    batches.Add(existing);
                }
                else
                {
                    var batch = new PurchaseBatch
                    {
                        ProductId = product.Id,
                        QuantityPurchased = quantity,
                        QuantityRemaining = quantity,
                        CostPerUnit = costPerUnit,
                        PurchaseDate = purchaseDate,
                        BatchNumber = lotNumber,
                        ExpiryDate = detail.ExpiryDate,
                        QualityStatus = "Good"
                    };
                    conn.Insert(batch);
                    batches.Add(batch);
                }
            }
            else
            {
                var batch = new PurchaseBatch
                {
                    ProductId = product.Id,
                    QuantityPurchased = quantity,
                    QuantityRemaining = quantity,
                    CostPerUnit = costPerUnit,
                    PurchaseDate = purchaseDate,
                    BatchNumber = string.IsNullOrWhiteSpace(detail?.BatchNumber)
                        ? $"{defaultBatchPrefix}-{DateTime.Now:MMddHHmm}"
                        : detail.BatchNumber.Trim(),
                    ExpiryDate = detail?.ExpiryDate,
                    QualityStatus = "Good"
                };
                conn.Insert(batch);
                batches.Add(batch);
            }

            return batches;
        }

        public static decimal DeductBatchesOnIssue(
            SQLiteConnection conn,
            Product product,
            int quantity,
            int stockMovementId,
            IReadOnlyList<string>? specificBatchIds = null)
        {
            if (product.ProductType != "Good" || quantity <= 0) return 0;

            decimal cogsAmount = 0;
            int remainingToDeduct = quantity;

            if (RequiresSerialTracking(product) && specificBatchIds != null && specificBatchIds.Count > 0)
            {
                foreach (var batchId in specificBatchIds)
                {
                    if (remainingToDeduct <= 0) break;
                    var batch = conn.Table<PurchaseBatch>().FirstOrDefault(b => b.Id == batchId && b.ProductId == product.Id);
                    if (batch == null || batch.QuantityRemaining <= 0)
                    {
                        throw new InvalidOperationException($"Serial batch '{batchId}' is not available.");
                    }

                    conn.Insert(new SaleBatchUsage
                    {
                        StockMovementId = stockMovementId,
                        PurchaseBatchId = batch.Id,
                        QuantityUsed = 1,
                        CostPerUnit = batch.CostPerUnit
                    });
                    cogsAmount += batch.CostPerUnit;
                    batch.QuantityRemaining = 0;
                    conn.Update(batch);
                    remainingToDeduct--;
                }
            }
            else
            {
                var batches = conn.Table<PurchaseBatch>()
                    .Where(b => b.ProductId == product.Id && b.QuantityRemaining > 0)
                    .ToList()
                    .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                    .ThenBy(b => b.PurchaseDate)
                    .ThenBy(b => b.Id)
                    .ToList();

                if (batches.Count == 0 && product.StockQuantity > 0)
                {
                    var recoveryBatch = new PurchaseBatch
                    {
                        ProductId = product.Id,
                        QuantityPurchased = product.StockQuantity,
                        QuantityRemaining = product.StockQuantity,
                        CostPerUnit = product.Cost,
                        PurchaseDate = DateTime.Now.AddDays(-1),
                        BatchNumber = $"RECOVERY-{DateTime.Now:yyyyMMddHHmmss}",
                        QualityStatus = "Good"
                    };
                    conn.Insert(recoveryBatch);
                    batches.Add(recoveryBatch);
                }

                foreach (var batch in batches)
                {
                    if (remainingToDeduct <= 0) break;

                    int deductFromThisBatch = RequiresSerialTracking(product)
                        ? Math.Min(1, batch.QuantityRemaining)
                        : Math.Min(batch.QuantityRemaining, remainingToDeduct);

                    conn.Insert(new SaleBatchUsage
                    {
                        StockMovementId = stockMovementId,
                        PurchaseBatchId = batch.Id,
                        QuantityUsed = deductFromThisBatch,
                        CostPerUnit = batch.CostPerUnit
                    });

                    cogsAmount += deductFromThisBatch * batch.CostPerUnit;
                    batch.QuantityRemaining -= deductFromThisBatch;
                    conn.Update(batch);
                    remainingToDeduct -= deductFromThisBatch;
                }
            }

            if (remainingToDeduct > 0)
            {
                throw new InvalidOperationException($"Insufficient batch stock for '{product.Name}'.");
            }

            return cogsAmount;
        }

        public static void RestoreBatchesOnReturn(
            SQLiteConnection conn,
            Product product,
            int quantity,
            int stockMovementId,
            decimal costPerUnit,
            BatchReceiveDetail? detail = null)
        {
            if (product.ProductType != "Good" || quantity <= 0) return;

            if (RequiresSerialTracking(product))
            {
                var serials = detail?.SerialNumbers?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList()
                              ?? new List<string>();
                if (serials.Count != quantity)
                {
                    var outMovements = conn.Table<StockMovement>()
                        .Where(m => m.ProductId == product.Id && m.MovementType == "OUT")
                        .ToList()
                        .OrderByDescending(m => m.Date)
                        .Take(quantity * 2);

                    foreach (var movement in outMovements)
                    {
                        if (serials.Count >= quantity) break;
                        var usages = conn.Table<SaleBatchUsage>()
                            .Where(u => u.StockMovementId == movement.Id)
                            .ToList();
                        foreach (var usage in usages)
                        {
                            var batch = conn.Table<PurchaseBatch>().FirstOrDefault(b => b.Id == usage.PurchaseBatchId);
                            if (batch != null && !string.IsNullOrWhiteSpace(batch.SerialNumber))
                            {
                                serials.Add(batch.SerialNumber);
                            }
                        }
                    }
                }

                if (serials.Count != quantity)
                {
                    throw new InvalidOperationException($"Return of '{product.Name}' requires one serial per unit.");
                }

                foreach (var serial in serials)
                {
                    var batch = conn.Table<PurchaseBatch>()
                        .FirstOrDefault(b => b.ProductId == product.Id && b.BatchNumber == serial);
                    if (batch == null)
                    {
                        batch = new PurchaseBatch
                        {
                            ProductId = product.Id,
                            QuantityPurchased = 1,
                            QuantityRemaining = 1,
                            CostPerUnit = costPerUnit,
                            PurchaseDate = DateTime.Now,
                            BatchNumber = serial,
                            SerialNumber = serial,
                            QualityStatus = "Good"
                        };
                        conn.Insert(batch);
                    }
                    else
                    {
                        if (batch.QuantityRemaining > 0)
                        {
                            throw new InvalidOperationException($"Serial '{serial}' is already in stock.");
                        }
                        batch.QuantityRemaining = 1;
                        conn.Update(batch);
                    }
                }
            }
            else if (RequiresLotTracking(product))
            {
                var lot = detail?.BatchNumber?.Trim();
                if (string.IsNullOrWhiteSpace(lot))
                {
                    lot = $"RET-LOT-{DateTime.Now:yyyyMMddHHmmss}";
                }

                var batch = conn.Table<PurchaseBatch>()
                    .FirstOrDefault(b => b.ProductId == product.Id && b.BatchNumber == lot);
                if (batch == null)
                {
                    batch = new PurchaseBatch
                    {
                        ProductId = product.Id,
                        QuantityPurchased = quantity,
                        QuantityRemaining = quantity,
                        CostPerUnit = costPerUnit,
                        PurchaseDate = DateTime.Now,
                        BatchNumber = lot,
                        ExpiryDate = detail?.ExpiryDate,
                        QualityStatus = "Good"
                    };
                    conn.Insert(batch);
                }
                else
                {
                    batch.QuantityPurchased += quantity;
                    batch.QuantityRemaining += quantity;
                    conn.Update(batch);
                }
            }
            else
            {
                var batch = new PurchaseBatch
                {
                    ProductId = product.Id,
                    QuantityPurchased = quantity,
                    QuantityRemaining = quantity,
                    CostPerUnit = costPerUnit,
                    PurchaseDate = DateTime.Now,
                    BatchNumber = $"RET-{DateTime.Now:yyyyMMddHHmmss}",
                    QualityStatus = "Good"
                };
                conn.Insert(batch);
            }
        }

        public static decimal AllocateLandedCostPerUnit(
            decimal lineExtendedCost,
            decimal totalExtendedCost,
            decimal totalLandedCost,
            int quantity)
        {
            if (quantity <= 0 || totalExtendedCost <= 0 || totalLandedCost <= 0) return 0;
            var lineShare = lineExtendedCost / totalExtendedCost;
            var lineLanded = totalLandedCost * lineShare;
            return lineLanded / quantity;
        }

        private static void EnsureSerialNotInStock(SQLiteConnection conn, int productId, string serial)
        {
            var inStock = conn.Table<PurchaseBatch>()
                .Any(b => b.ProductId == productId && b.BatchNumber == serial && b.QuantityRemaining > 0);
            if (inStock)
            {
                throw new InvalidOperationException($"Serial '{serial}' is already in stock.");
            }
        }
    }
}
