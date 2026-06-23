using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class PurchaseOrderService
    {
        private readonly DatabaseService _databaseService;
        private readonly InventoryService _inventoryService;

        public PurchaseOrderService(DatabaseService databaseService, InventoryService inventoryService)
        {
            _databaseService = databaseService;
            _inventoryService = inventoryService;
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
            po.Status = "Draft";
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
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var po = conn.Find<PurchaseOrder>(poId);
                if (po == null || po.Status == "Cancelled" || po.Status == "Received") return;

                foreach (var line in receivedItems)
                {
                    if (line.quantityReceived <= 0) continue;

                    var item = conn.Find<PurchaseOrderItem>(line.itemId);
                    if (item == null || item.PurchaseOrderId != poId) continue;

                    int remainingToReceive = item.QuantityOrdered - item.QuantityReceived;
                    if (line.quantityReceived > remainingToReceive) continue;

                    item.QuantityReceived += line.quantityReceived;
                    conn.Update(item);

                    var product = conn.Find<Product>(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += line.quantityReceived;
                        if (item.UnitCost > 0) product.Cost = item.UnitCost;
                        conn.Update(product);

                        conn.Insert(new PurchaseBatch
                        {
                            ProductId = product.Id,
                            QuantityPurchased = line.quantityReceived,
                            QuantityRemaining = line.quantityReceived,
                            CostPerUnit = item.UnitCost,
                            PurchaseDate = DateTime.Now,
                            BatchNumber = $"{po.PONumber}-BATCH-{item.Id}-{DateTime.Now:MMddHHmm}",
                            QualityStatus = "Good"
                        });

                        conn.Insert(new StockMovement
                        {
                            ProductId = product.Id,
                            QuantityChanged = line.quantityReceived,
                            MovementType = "IN",
                            Reason = $"PO Receipt: {po.PONumber}",
                            Date = DateTime.Now,
                            Username = UserSession.CurrentUser?.Username ?? "System",
                            UnitPrice = product.Price
                        });

                        // --- Post Journal Entry for Purchase Receipt ---
                        var journal = conn.Table<Journal>().Where(j => j.Type == "Purchase").FirstOrDefault();
                        if (journal != null)
                        {
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

                            decimal purchaseAmount = line.quantityReceived * item.UnitCost;

                            int debitAccountId;
                            if (product.ProductType == "Service")
                            {
                                debitAccountId = product.ExpenseAccountId ?? 0;
                                if (debitAccountId == 0)
                                {
                                    var expAccount = conn.Table<Account>().Where(a => a.Code == "511000").FirstOrDefault();
                                    debitAccountId = expAccount?.Id ?? 17; // General Expenses fallback
                                }
                            }
                            else
                            {
                                var inventoryAccount = conn.Table<Account>().Where(a => a.Code == "120000").FirstOrDefault();
                                debitAccountId = inventoryAccount?.Id ?? 4; // Inventory Asset fallback
                            }

                            var apAccount = conn.Table<Account>().Where(a => a.Code == "201000").FirstOrDefault();
                            int creditAccountId = apAccount?.Id ?? 7; // Accounts Payable fallback

                            conn.Insert(new JournalLine
                            {
                                JournalEntryId = entry.Id,
                                AccountId = debitAccountId,
                                ProductId = product.Id,
                                Label = $"Purchase Receipt - {product.Name} (Qty: {line.quantityReceived})",
                                Debit = purchaseAmount,
                                Credit = 0
                            });

                            conn.Insert(new JournalLine
                            {
                                JournalEntryId = entry.Id,
                                AccountId = creditAccountId,
                                ProductId = product.Id,
                                Label = $"Purchase Receipt - {product.Name} (Qty: {line.quantityReceived})",
                                Debit = 0,
                                Credit = purchaseAmount
                            });
                        }
                    }
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
