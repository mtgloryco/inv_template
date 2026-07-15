using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using SQLite;

namespace InventoryManagementSystem.Services
{
    public class PurchaseOrderService
    {
        private readonly DatabaseService _databaseService;
        private readonly InventoryService _inventoryService;
        private readonly AuditService? _auditService;

        public PurchaseOrderService(DatabaseService databaseService, InventoryService inventoryService, AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _inventoryService = inventoryService;
            _auditService = auditService;
        }

        public async Task CreatePurchaseOrderAsync(PurchaseOrder po, List<PurchaseOrderItem> items)
        {
            po.OrderDate = DateTime.Now;
            po.PONumber = await GeneratePoNumberAsync();
            po.TotalAmount = items.Sum(i => i.QuantityOrdered * i.UnitCost);

            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Insert(po);
                foreach (var item in items)
                {
                    item.PurchaseOrderId = po.Id;
                    conn.Insert(item);
                }
            });
        }

        public async Task CreateRfqAsync(PurchaseOrder po, List<PurchaseOrderItem> items)
        {
            po.OrderDate = DateTime.Now;
            if (po.Status != "Sent") po.Status = "Draft";
            po.PONumber = await GenerateRfqNumberAsync();
            po.TotalAmount = items.Sum(i => i.QuantityOrdered * i.UnitCost);

            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Insert(po);
                foreach (var item in items)
                {
                    item.PurchaseOrderId = po.Id;
                    conn.Insert(item);
                }
            });
        }

        public async Task UpdatePurchaseOrderAsync(PurchaseOrder po, List<PurchaseOrderItem> items)
        {
            po.TotalAmount = items.Sum(i => i.QuantityOrdered * i.UnitCost);

            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Update(po);
                
                // Delete old items
                var oldItems = conn.Table<PurchaseOrderItem>().Where(i => i.PurchaseOrderId == po.Id).ToList();
                foreach (var oldItem in oldItems)
                {
                    conn.Delete(oldItem);
                }
                
                // Insert new items
                foreach (var item in items)
                {
                    item.PurchaseOrderId = po.Id;
                    conn.Insert(item);
                }
            });
        }

        public async Task<string> GenerateRfqNumberAsync()
        {
            var year = DateTime.Now.Year;
            var prefix = $"RFQ-{year}-";
            var count = await _databaseService.Connection.Table<PurchaseOrder>().CountAsync();
            return $"{prefix}{(count + 1):D4}";
        }

        public async Task ConvertRfqToPoAsync(int rfqId, string approverUsername)
        {
            var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(rfqId);
            if (po == null) return;
            po.Status = "Approved";
            po.ApprovedByUsername = approverUsername;
            
            // Rename to PO number prefix
            if (po.PONumber.StartsWith("RFQ-"))
            {
                var year = DateTime.Now.Year;
                var prefix = $"PO-{year}-";
                var count = await _databaseService.Connection.Table<PurchaseOrder>().Where(p => p.PONumber.StartsWith("PO-")).CountAsync();
                po.PONumber = $"{prefix}{(count + 1):D4}";
            }
            await _databaseService.Connection.UpdateAsync(po);
        }

        public async Task ApprovePurchaseOrderAsync(int poId, string approverUsername)
        {
            var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(poId);
            if (po == null) return;
            po.Status = "Approved";
            po.ApprovedByUsername = approverUsername;
            await _databaseService.Connection.UpdateAsync(po);
        }

        public async Task MarkAsShippedAsync(int poId)
        {
            var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(poId);
            if (po == null) return;
            po.Status = "Shipped";
            await _databaseService.Connection.UpdateAsync(po);
        }

        public async Task ReceivePurchaseOrderAsync(int poId, List<(int itemId, int quantityReceived)> receivedItems)
        {
            var lines = receivedItems.Select(r => new PurchaseReceiveLine
            {
                ItemId = r.itemId,
                QuantityReceived = r.quantityReceived
            }).ToList();
            await ReceivePurchaseOrderAsync(poId, lines, null);
        }

        public async Task ReceivePurchaseOrderAsync(
            int poId,
            List<PurchaseReceiveLine> receivedItems,
            List<LandedCostInput>? landedCosts = null)
        {
            var receivedAny = false;
            string? poNumber = null;
            decimal totalLandedCost = 0;

            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var po = conn.Find<PurchaseOrder>(poId);
                if (po == null || po.Status == "Cancelled" || po.Status == "Received") return;

                poNumber = po.PONumber;
                decimal totalExtendedCost = 0;
                var pendingLines = new List<(PurchaseOrderItem item, Product product, int qty, BatchReceiveDetail? detail)>();

                foreach (var line in receivedItems)
                {
                    if (line.QuantityReceived <= 0) continue;

                    var item = conn.Find<PurchaseOrderItem>(line.ItemId);
                    if (item == null || item.PurchaseOrderId != poId) continue;

                    int remainingToReceive = item.QuantityOrdered - item.QuantityReceived;
                    if (line.QuantityReceived > remainingToReceive) continue;

                    var product = conn.Find<Product>(item.ProductId);
                    if (product == null) continue;

                    BatchTrackingService.ValidateReceiveDetails(product, line.QuantityReceived, line.BatchDetail);
                    totalExtendedCost += line.QuantityReceived * item.UnitCost;
                    pendingLines.Add((item, product, line.QuantityReceived, line.BatchDetail));
                }

                totalLandedCost = landedCosts?.Sum(c => c.Amount) ?? 0;
                if (landedCosts != null)
                {
                    foreach (var cost in landedCosts.Where(c => c.Amount > 0))
                    {
                        conn.Insert(new LandedCostCharge
                        {
                            PurchaseOrderId = poId,
                            CostType = cost.CostType,
                            Amount = cost.Amount,
                            AppliedDate = DateTime.Now,
                            Reference = po.PONumber
                        });
                    }
                }

                if (pendingLines.Count == 0) return;
                receivedAny = true;

                foreach (var (item, product, qty, detail) in pendingLines)
                {
                    item.QuantityReceived += qty;
                    conn.Update(item);

                    var landedPerUnit = BatchTrackingService.AllocateLandedCostPerUnit(
                        qty * item.UnitCost, totalExtendedCost, totalLandedCost, qty);
                    var unitCostWithLanded = item.UnitCost + landedPerUnit;

                    var previousStock = product.StockQuantity;
                    product.StockQuantity += qty;
                    if (unitCostWithLanded > 0) product.Cost = unitCostWithLanded;
                    conn.Update(product);
                    LocationStockSync.ApplyDelta(conn, product.Id, product.StockQuantity - previousStock);

                    BatchTrackingService.CreateBatchesOnReceive(
                        conn,
                        product,
                        qty,
                        unitCostWithLanded,
                        DateTime.Now,
                        detail,
                        $"{po.PONumber}-BATCH-{item.Id}");

                    conn.Insert(new StockMovement
                    {
                        ProductId = product.Id,
                        QuantityChanged = qty,
                        MovementType = "IN",
                        Reason = $"PO Receipt: {po.PONumber}",
                        Date = DateTime.Now,
                        Username = UserSession.CurrentUser?.Username ?? "System",
                        UnitPrice = product.Price
                    });

                    PostPurchaseReceiptJournal(conn, po, item, product, qty, unitCostWithLanded, landedPerUnit * qty);
                }

                var allItems = conn.Table<PurchaseOrderItem>().Where(i => i.PurchaseOrderId == poId).ToList();
                bool allReceived = allItems.All(i => i.QuantityReceived >= i.QuantityOrdered);

                po.Status = allReceived ? "Received" : "Shipped";
                po.ReceiptStatus = allItems.Any(i => i.QuantityReceived > 0)
                    ? (allReceived ? "Received" : "Partially Received")
                    : "Pending";
                if (po.Status == "Received")
                {
                    po.ActualDeliveryDate = DateTime.Now;
                }
                conn.Update(po);
            });

            if (_auditService != null && receivedAny)
            {
                var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(poId);
                await _auditService.LogActionAsync(
                    UserSession.CurrentUser?.Username ?? "System",
                    "Receive", "PurchaseOrder", poId,
                    new { PONumber = poNumber ?? po?.PONumber, po?.Status, TotalLandedCost = totalLandedCost, LandedCostReference = poNumber });
            }
        }

        private static void PostPurchaseReceiptJournal(
            SQLiteConnection conn,
            PurchaseOrder po,
            PurchaseOrderItem item,
            Product product,
            int quantityReceived,
            decimal unitCost,
            decimal landedAmount)
        {
            var journal = conn.Table<Journal>().Where(j => j.Type == "Purchase").FirstOrDefault();
            if (journal == null) return;

            var entryCount = conn.Table<JournalEntry>().Where(e => e.JournalId == journal.Id).Count();
            var entryNumber = $"{journal.SequencePrefix}/{DateTime.Now.Year}/{(entryCount + 1):D5}";

            var entry = new JournalEntry
            {
                EntryNumber = entryNumber,
                JournalId = journal.Id,
                Date = DateTime.Now,
                Reference = $"PO Receipt: {po.PONumber}",
                State = "Posted"
            };
            conn.Insert(entry);

            decimal purchaseAmount = quantityReceived * item.UnitCost;
            decimal taxAmount = 0;
            decimal inventoryValue = purchaseAmount + landedAmount;
            decimal apAmount = purchaseAmount;

            if (item.TaxId.HasValue)
            {
                var tax = conn.Find<Tax>(item.TaxId.Value);
                if (tax != null)
                {
                    if (tax.IncludedInPrice == "Include")
                    {
                        if (tax.Computation == "Percentage")
                        {
                            inventoryValue = purchaseAmount / (1 + (tax.Amount / 100)) + landedAmount;
                        }
                        else
                        {
                            inventoryValue = Math.Max(0, purchaseAmount - (quantityReceived * tax.Amount)) + landedAmount;
                        }
                        taxAmount = purchaseAmount + landedAmount - inventoryValue;
                    }
                    else
                    {
                        if (tax.Computation == "Percentage")
                        {
                            taxAmount = purchaseAmount * (tax.Amount / 100);
                        }
                        else
                        {
                            taxAmount = quantityReceived * tax.Amount;
                        }
                        apAmount = purchaseAmount + taxAmount;
                    }
                }
            }

            int debitAccountId;
            if (product.ProductType == "Service")
            {
                debitAccountId = product.ExpenseAccountId ?? 0;
                if (debitAccountId == 0)
                {
                    var expAccount = conn.Table<Account>().Where(a => a.Code == "511000").FirstOrDefault();
                    debitAccountId = expAccount?.Id ?? 17;
                }
            }
            else
            {
                var inventoryAccount = conn.Table<Account>().Where(a => a.Code == "120000").FirstOrDefault();
                debitAccountId = inventoryAccount?.Id ?? 4;
            }

            var apAccount = conn.Table<Account>().Where(a => a.Code == "201000").FirstOrDefault();
            int creditAccountId = apAccount?.Id ?? 7;

            conn.Insert(new JournalLine
            {
                JournalEntryId = entry.Id,
                AccountId = debitAccountId,
                ProductId = product.Id,
                Label = $"Purchase Receipt - {product.Name} (Qty: {quantityReceived})",
                Debit = inventoryValue,
                Credit = 0
            });

            if (taxAmount > 0)
            {
                var taxAccount = conn.Table<Account>()
                    .Where(a => a.Code == "125000" || a.Name.Contains("VAT Receivable") || a.Name.Contains("Tax Receivable"))
                    .FirstOrDefault();
                int taxAccountId = taxAccount?.Id ?? 0;
                if (taxAccountId == 0)
                {
                    var vatPayable = conn.Table<Account>().Where(a => a.Code == "220000").FirstOrDefault();
                    taxAccountId = vatPayable?.Id ?? 9;
                }

                conn.Insert(new JournalLine
                {
                    JournalEntryId = entry.Id,
                    AccountId = taxAccountId,
                    ProductId = product.Id,
                    Label = $"VAT on Purchase - {product.Name}",
                    Debit = taxAmount,
                    Credit = 0
                });
            }

            conn.Insert(new JournalLine
            {
                JournalEntryId = entry.Id,
                AccountId = creditAccountId,
                ProductId = product.Id,
                Label = $"Purchase Receipt - {product.Name} (Qty: {quantityReceived})",
                Debit = 0,
                Credit = apAmount
            });
        }

        public async Task<List<PurchaseOrderListItem>> GetAllPurchaseOrdersAsync()
        {
            var purchaseOrders = await _databaseService.Connection.Table<PurchaseOrder>()
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();

            var suppliers = await _databaseService.Connection.Table<Supplier>().ToListAsync();
            return purchaseOrders.Select(po => new PurchaseOrderListItem
            {
                PurchaseOrder = po,
                SupplierName = suppliers.FirstOrDefault(s => s.Id == po.SupplierId)?.Name ?? "Unknown"
            }).ToList();
        }

        public async Task<List<PurchaseOrder>> GetPendingPurchaseOrdersAsync()
        {
            var pending = new[] { "Draft", "Pending", "Approved", "Shipped" };
            return await _databaseService.Connection.Table<PurchaseOrder>()
                .Where(po => pending.Contains(po.Status))
                .OrderByDescending(po => po.OrderDate)
                .ToListAsync();
        }

        public async Task CancelPurchaseOrderAsync(int poId)
        {
            var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(poId);
            if (po == null || po.Status == "Received") return;
            po.Status = "Cancelled";
            await _databaseService.Connection.UpdateAsync(po);
        }

        public async Task<List<PurchaseOrderItem>> GetItemsAsync(int poId)
        {
            return await _databaseService.Connection.Table<PurchaseOrderItem>()
                .Where(i => i.PurchaseOrderId == poId)
                .ToListAsync();
        }

        private async Task<string> GeneratePoNumberAsync()
        {
            var year = DateTime.Now.Year;
            var prefix = $"PO-{year}-";
            var count = await _databaseService.Connection.Table<PurchaseOrder>().CountAsync();
            return $"{prefix}{(count + 1):D4}";
        }

        public async Task CreateBillAsync(int poId)
        {
            var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(poId);
            if (po == null) return;
            po.BillingStatus = "Billed";
            await _databaseService.Connection.UpdateAsync(po);

            var items = await _databaseService.Connection.Table<PurchaseOrderItem>()
                .Where(i => i.PurchaseOrderId == poId)
                .ToListAsync();

            foreach (var item in items)
            {
                item.QuantityBilled = item.QuantityReceived;
                await _databaseService.Connection.UpdateAsync(item);
            }

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    UserSession.CurrentUser?.Username ?? "System",
                    "Bill", "PurchaseOrder", poId, po);
            }
        }

        public async Task ArchivePurchaseOrderAsync(int poId, bool archive)
        {
            var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(poId);
            if (po == null) return;
            po.IsArchived = archive;
            await _databaseService.Connection.UpdateAsync(po);
        }

        public async Task<bool> DeletePurchaseOrderAsync(int poId)
        {
            var items = await _databaseService.Connection.Table<PurchaseOrderItem>()
                .Where(i => i.PurchaseOrderId == poId)
                .ToListAsync();

            if (items.Any(i => i.QuantityReceived > 0 || i.QuantityBilled > 0))
            {
                return false; // Can't delete, must archive
            }

            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                foreach (var item in items)
                {
                    conn.Delete(item);
                }
                var po = conn.Find<PurchaseOrder>(poId);
                if (po != null)
                {
                    conn.Delete(po);
                }
            });
            return true;
        }
    }
}
