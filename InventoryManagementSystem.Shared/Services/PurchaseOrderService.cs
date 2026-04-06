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
            var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(poId);
            if (po == null || po.Status == "Cancelled") return;

            foreach (var line in receivedItems)
            {
                var item = await _databaseService.Connection.FindAsync<PurchaseOrderItem>(line.itemId);
                if (item == null) continue;

                item.QuantityReceived += line.quantityReceived;
                await _databaseService.Connection.UpdateAsync(item);

                if (line.quantityReceived > 0)
                {
                    await _inventoryService.AddStockMovementAsync(
                        item.ProductId,
                        line.quantityReceived,
                        "IN",
                        $"PO Receipt: {po.PONumber}",
                        UserSession.CurrentUser?.Username ?? "System",
                        item.UnitCost,
                        unitPrice: null,
                        batchNumber: $"{po.PONumber}-ITEM-{item.Id}",
                        expiryDate: null,
                        qualityStatus: "Good");
                }
            }

            var allItems = await _databaseService.Connection.Table<PurchaseOrderItem>()
                .Where(i => i.PurchaseOrderId == poId)
                .ToListAsync();

            po.Status = allItems.All(i => i.QuantityReceived >= i.QuantityOrdered) ? "Received" : "Shipped";
            if (po.Status == "Received")
            {
                po.ActualDeliveryDate = DateTime.Now;
            }
            await _databaseService.Connection.UpdateAsync(po);
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
    }
}
