using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class SalesOrderService
    {
        private readonly DatabaseService _databaseService;
        private readonly InventoryService _inventoryService;
        private readonly AuditService? _auditService;

        public SalesOrderService(DatabaseService databaseService, InventoryService inventoryService, AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _inventoryService = inventoryService;
            _auditService = auditService;
        }

        // --- PAYMENT TERMS ---
        public async Task<List<PaymentTerm>> GetAllPaymentTermsAsync()
        {
            return await _databaseService.Connection.Table<PaymentTerm>().ToListAsync();
        }

        public async Task CreatePaymentTermAsync(PaymentTerm term)
        {
            if (term.DueDays == 0 && !string.IsNullOrWhiteSpace(term.Name))
            {
                term.DueDays = AgingReportService.ParseDueDays(term.Name);
            }

            await _databaseService.Connection.InsertAsync(term);
        }

        // --- CRUD OPERATIONS ---
        public async Task CreateSalesQuotationAsync(SalesOrder so, List<SalesOrderItem> items)
        {
            so.OrderDate = DateTime.Now;
            so.QuotationDate = DateTime.Now;
            so.Status = "Draft";
            so.SONumber = await GenerateSqNumberAsync();
            so.TotalAmount = await CalculateTotalAmountAsync(so, items);
            
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Insert(so);
                foreach (var item in items)
                {
                    item.SalesOrderId = so.Id;
                    conn.Insert(item);
                }
            });
        }

        public async Task CreateSalesOrderAsync(SalesOrder so, List<SalesOrderItem> items)
        {
            so.OrderDate = DateTime.Now;
            so.Status = "Confirmed";
            so.SONumber = await GenerateSoNumberAsync();
            so.TotalAmount = await CalculateTotalAmountAsync(so, items);
            
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Insert(so);
                foreach (var item in items)
                {
                    item.SalesOrderId = so.Id;
                    conn.Insert(item);
                }
            });
        }

        public async Task UpdateSalesOrderAsync(SalesOrder so, List<SalesOrderItem> items)
        {
            so.TotalAmount = await CalculateTotalAmountAsync(so, items);
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Update(so);
                
                // Delete old items
                var oldItems = conn.Table<SalesOrderItem>().Where(i => i.SalesOrderId == so.Id).ToList();
                foreach (var oldItem in oldItems)
                {
                    conn.Delete(oldItem);
                }
                
                // Insert new items
                foreach (var item in items)
                {
                    item.SalesOrderId = so.Id;
                    conn.Insert(item);
                }
            });
        }

        private async Task<decimal> CalculateTotalAmountAsync(SalesOrder so, List<SalesOrderItem> items)
        {
            decimal total = 0;
            var taxes = await _databaseService.Connection.Table<Tax>().ToListAsync();
            foreach (var item in items)
            {
                decimal itemSubtotal = item.QuantityOrdered * item.UnitPrice;
                if (item.TaxId.HasValue)
                {
                    var tax = taxes.FirstOrDefault(t => t.Id == item.TaxId.Value);
                    if (tax != null)
                    {
                        if (so.IsTaxInclusive || tax.IncludedInPrice == "Include")
                        {
                            total += itemSubtotal;
                        }
                        else
                        {
                            decimal taxAmount = 0;
                            if (tax.Computation == "Percentage")
                            {
                                taxAmount = itemSubtotal * (tax.Amount / 100);
                            }
                            else
                            {
                                taxAmount = item.QuantityOrdered * tax.Amount;
                            }
                            total += itemSubtotal + taxAmount;
                        }
                    }
                    else
                    {
                        total += itemSubtotal;
                    }
                }
                else
                {
                    total += itemSubtotal;
                }
            }
            return total;
        }

        public async Task<List<SalesOrderListItem>> GetAllSalesOrdersAsync()
        {
            var salesOrders = await _databaseService.Connection.Table<SalesOrder>()
                .OrderByDescending(s => s.OrderDate)
                .ToListAsync();

            var customers = await _databaseService.Connection.Table<Customer>().ToListAsync();
            return salesOrders.Select(so => new SalesOrderListItem
            {
                SalesOrder = so,
                CustomerName = customers.FirstOrDefault(c => c.Id == so.CustomerId)?.Name ?? "Unknown Customer"
            }).ToList();
        }

        public async Task<List<SalesOrderItem>> GetItemsAsync(int soId)
        {
            return await _databaseService.Connection.Table<SalesOrderItem>()
                .Where(i => i.SalesOrderId == soId)
                .ToListAsync();
        }

        public async Task<bool> DeleteSalesOrderAsync(int soId)
        {
            var items = await _databaseService.Connection.Table<SalesOrderItem>()
                .Where(i => i.SalesOrderId == soId)
                .ToListAsync();

            if (items.Any(i => i.QuantityDelivered > 0 || i.QuantityInvoiced > 0))
            {
                return false; // Can't delete delivered/invoiced orders
            }

            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                foreach (var item in items)
                {
                    conn.Delete(item);
                }
                var so = conn.Find<SalesOrder>(soId);
                if (so != null)
                {
                    conn.Delete(so);
                }
            });
            return true;
        }

        public async Task ConfirmQuotationAsync(int quotationId)
        {
            var so = await _databaseService.Connection.FindAsync<SalesOrder>(quotationId);
            if (so == null) return;
            so.Status = "Confirmed";
            
            if (so.SONumber.StartsWith("SQ-"))
            {
                so.SONumber = await GenerateSoNumberAsync();
            }
            await _databaseService.Connection.UpdateAsync(so);
        }

        public async Task CancelSalesOrderAsync(int soId)
        {
            var so = await _databaseService.Connection.FindAsync<SalesOrder>(soId);
            if (so == null || so.Status == "Delivered") return;
            so.Status = "Cancelled";
            await _databaseService.Connection.UpdateAsync(so);
        }

        public async Task InvoiceSalesOrderAsync(int soId)
        {
            var so = await _databaseService.Connection.FindAsync<SalesOrder>(soId);
            if (so == null) return;
            so.BillingStatus = "Invoiced";
            await _databaseService.Connection.UpdateAsync(so);

            var items = await _databaseService.Connection.Table<SalesOrderItem>()
                .Where(i => i.SalesOrderId == soId)
                .ToListAsync();

            foreach (var item in items)
            {
                item.QuantityInvoiced = item.QuantityOrdered;
                await _databaseService.Connection.UpdateAsync(item);
            }

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    UserSession.CurrentUser?.Username ?? "System",
                    "Invoice", "SalesOrder", soId, so);
            }
        }

        public async Task DeliverSalesOrderAsync(int soId, List<(int itemId, int quantityDelivered)> deliveryLines)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var so = conn.Find<SalesOrder>(soId);
                if (so == null || so.Status == "Cancelled" || so.Status == "Delivered") return;

                foreach (var line in deliveryLines)
                {
                    if (line.quantityDelivered <= 0) continue;

                    var item = conn.Find<SalesOrderItem>(line.itemId);
                    if (item == null || item.SalesOrderId != soId) continue;

                    int remainingToDeliver = item.QuantityOrdered - item.QuantityDelivered;
                    if (line.quantityDelivered > remainingToDeliver) continue;

                    item.QuantityDelivered += line.quantityDelivered;
                    conn.Update(item);

                    var product = conn.Find<Product>(item.ProductId);
                    if (product != null)
                    {
                        var previousStock = product.StockQuantity;
                        product.StockQuantity = Math.Max(0, product.StockQuantity - line.quantityDelivered);
                        conn.Update(product);
                        LocationStockSync.ApplyDelta(conn, product.Id, product.StockQuantity - previousStock);

                        // --- Record Stock Movement OUT ---
                        var movement = new StockMovement
                        {
                            ProductId = product.Id,
                            QuantityChanged = line.quantityDelivered,
                            MovementType = "OUT",
                            Reason = $"Sales Delivery: {so.SONumber}",
                            Date = DateTime.Now,
                            Username = UserSession.CurrentUser?.Username ?? "System",
                            UnitPrice = item.UnitPrice
                        };
                        conn.Insert(movement);

                        // --- Batch Tracking / FIFO Costing for Goods ---
                        decimal cogsAmount = 0;
                        if (product.ProductType == "Good")
                        {
                            cogsAmount = BatchTrackingService.DeductBatchesOnIssue(
                                conn, product, line.quantityDelivered, movement.Id);
                        }

                        // --- Double Entry Journal Entry ---
                        var journal = conn.Table<Journal>().Where(j => j.Type == "Sales").FirstOrDefault();
                        if (journal != null)
                        {
                            var entryCount = conn.Table<JournalEntry>().Where(e => e.JournalId == journal.Id).Count();
                            var entryNumber = $"{journal.SequencePrefix}/{DateTime.Now.Year}/{(entryCount + 1):D5}";

                            var entry = new JournalEntry
                            {
                                EntryNumber = entryNumber,
                                JournalId = journal.Id,
                                Date = DateTime.Now,
                                Reference = $"Sales Delivery: {so.SONumber}",
                                State = "Posted"
                            };
                            conn.Insert(entry);

                            // Calculate Tax portion
                            decimal totalSalesAmount = line.quantityDelivered * item.UnitPrice;
                            decimal taxAmount = 0;
                            decimal subtotalAmount = totalSalesAmount;
                            Tax? appliedTax = null;

                            if (item.TaxId.HasValue)
                            {
                                var tax = conn.Find<Tax>(item.TaxId.Value);
                                if (tax != null)
                                {
                                    appliedTax = tax;
                                    if (so.IsTaxInclusive)
                                    {
                                        if (tax.Computation == "Percentage")
                                        {
                                            subtotalAmount = totalSalesAmount / (1 + (tax.Amount / 100));
                                        }
                                        else
                                        {
                                            subtotalAmount = Math.Max(0, totalSalesAmount - (line.quantityDelivered * tax.Amount));
                                        }
                                        taxAmount = totalSalesAmount - subtotalAmount;
                                    }
                                    else
                                    {
                                        if (tax.Computation == "Percentage")
                                        {
                                            taxAmount = totalSalesAmount * (tax.Amount / 100);
                                        }
                                        else
                                        {
                                            taxAmount = line.quantityDelivered * tax.Amount;
                                        }
                                        totalSalesAmount = subtotalAmount + taxAmount;
                                    }
                                }
                            }

                            // Accounts
                            var arAccount = conn.Table<Account>().Where(a => a.Code == "111000").FirstOrDefault();
                            int debitAccountId = arAccount?.Id ?? 3; // Accounts Receivable fallback

                            int creditAccountId = product.IncomeAccountId ?? 0;
                            if (creditAccountId == 0)
                            {
                                var revAccount = conn.Table<Account>().Where(a => a.Code == "401000").FirstOrDefault();
                                creditAccountId = revAccount?.Id ?? 13; // Product Sales Revenue fallback
                            }

                            // Debit Accounts Receivable
                            conn.Insert(new JournalLine
                            {
                                JournalEntryId = entry.Id,
                                AccountId = debitAccountId,
                                ProductId = product.Id,
                                Label = $"Sale - {product.Name} (Qty: {line.quantityDelivered})",
                                Debit = totalSalesAmount,
                                Credit = 0
                            });

                            // Credit Sales Revenue
                            conn.Insert(new JournalLine
                            {
                                JournalEntryId = entry.Id,
                                AccountId = creditAccountId,
                                ProductId = product.Id,
                                Label = $"Sale - {product.Name} (Qty: {line.quantityDelivered})",
                                Debit = 0,
                                Credit = subtotalAmount
                            });

                            // Credit VAT Payable if taxes apply
                            if (taxAmount > 0)
                            {
                                int taxAccountId = appliedTax?.AccountId ?? 0;
                                if (taxAccountId == 0)
                                {
                                    var vatAccount = conn.Table<Account>().Where(a => a.Code == "220000").FirstOrDefault();
                                    taxAccountId = vatAccount?.Id ?? 9; // VAT Payable fallback
                                }

                                conn.Insert(new JournalLine
                                {
                                    JournalEntryId = entry.Id,
                                    AccountId = taxAccountId,
                                    ProductId = product.Id,
                                    Label = $"VAT on Sale - {product.Name}",
                                    Debit = 0,
                                    Credit = taxAmount
                                });
                            }

                            // Stock Valuation Adjustment & COGS for Goods
                            if (product.ProductType == "Good" && cogsAmount > 0)
                            {
                                int cogsAccountId = product.ExpenseAccountId ?? 0;
                                if (cogsAccountId == 0)
                                {
                                    var expAccount = conn.Table<Account>().Where(a => a.Code == "501000").FirstOrDefault();
                                    cogsAccountId = expAccount?.Id ?? 16; // COGS fallback
                                }

                                var inventoryAccount = conn.Table<Account>().Where(a => a.Code == "120000").FirstOrDefault();
                                int assetAccountId = inventoryAccount?.Id ?? 4; // Inventory Asset fallback

                                // Debit COGS
                                conn.Insert(new JournalLine
                                {
                                    JournalEntryId = entry.Id,
                                    AccountId = cogsAccountId,
                                    ProductId = product.Id,
                                    Label = $"COGS - {product.Name}",
                                    Debit = cogsAmount,
                                    Credit = 0
                                });

                                // Credit Inventory Asset
                                conn.Insert(new JournalLine
                                {
                                    JournalEntryId = entry.Id,
                                    AccountId = assetAccountId,
                                    ProductId = product.Id,
                                    Label = $"Inventory Issue - {product.Name}",
                                    Debit = 0,
                                    Credit = cogsAmount
                                });
                            }
                        }
                    }
                }

                var allItems = conn.Table<SalesOrderItem>().Where(i => i.SalesOrderId == soId).ToList();
                bool allDelivered = allItems.All(i => i.QuantityDelivered >= i.QuantityOrdered);

                so.Status = allDelivered ? "Delivered" : "Confirmed";
                so.DeliveryStatus = allItems.Any(i => i.QuantityDelivered > 0)
                    ? (allDelivered ? "Delivered" : "Partially Delivered")
                    : "Pending";
                
                if (so.Status == "Delivered")
                {
                    so.DeliveryDate = DateTime.Now;
                }
                conn.Update(so);
            });
        }

        // --- SEQUENCE GENERATORS ---
        private async Task<string> GenerateSqNumberAsync()
        {
            var year = DateTime.Now.Year;
            var prefix = $"SQ-{year}-";
            var count = await _databaseService.Connection.Table<SalesOrder>().CountAsync();
            return $"{prefix}{(count + 1):D4}";
        }

        private async Task<string> GenerateSoNumberAsync()
        {
            var year = DateTime.Now.Year;
            var prefix = $"SO-{year}-";
            var count = await _databaseService.Connection.Table<SalesOrder>().Where(s => s.SONumber.StartsWith("SO-")).CountAsync();
            return $"{prefix}{(count + 1):D4}";
        }

        // --- POS OPERATIONS ---
        public async Task<List<SalesOrderListItem>> GetPosSalesOrdersAsync()
        {
            var posOrders = await _databaseService.Connection.Table<SalesOrder>()
                .Where(so => so.IsPosSale)
                .OrderByDescending(so => so.OrderDate)
                .ToListAsync();

            var customers = await _databaseService.Connection.Table<Customer>().ToListAsync();
            
            var items = new List<SalesOrderListItem>();
            foreach (var so in posOrders)
            {
                var customer = customers.Find(c => c.Id == so.CustomerId);
                items.Add(new SalesOrderListItem 
                { 
                    SalesOrder = so, 
                    CustomerName = customer?.Name ?? "Walk-in Customer" 
                });
            }
            return items;
        }

        public async Task<string> GeneratePosNumberAsync()
        {
            var year = DateTime.Now.Year;
            var prefix = $"POS-{year}-";
            var count = await _databaseService.Connection.Table<SalesOrder>().Where(s => s.IsPosSale).CountAsync();
            return $"{prefix}{(count + 1):D4}";
        }
    }
}
