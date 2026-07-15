using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using SQLite;

namespace InventoryManagementSystem.Services
{
    public class ReturnsService
    {
        private readonly DatabaseService _databaseService;
        private readonly AuditService _auditService;

        public ReturnsService(DatabaseService databaseService, AuditService auditService)
        {
            _databaseService = databaseService;
            _auditService = auditService;
        }

        public async Task ProcessCustomerReturnAsync(CustomerReturn ret)
        {
            CreditNote? creditNote = null;
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Insert(ret);

                var product = conn.Find<Product>(ret.ProductId);
                if (product == null) return;

                decimal restockValue = 0;
                if (ret.Condition == "Resaleable")
                {
                    var movement = new StockMovement
                    {
                        ProductId = ret.ProductId,
                        QuantityChanged = ret.Quantity,
                        MovementType = "IN",
                        Reason = "Customer Return - Resaleable",
                        Date = DateTime.Now,
                        Username = ret.ProcessedByUsername
                    };
                    conn.Insert(movement);

                    BatchTrackingService.RestoreBatchesOnReturn(conn, product, ret.Quantity, movement.Id, product.Cost);

                    var previousStock = product.StockQuantity;
                    product.StockQuantity += ret.Quantity;
                    conn.Update(product);
                    LocationStockSync.ApplyDelta(conn, product.Id, product.StockQuantity - previousStock);
                    restockValue = ret.Quantity * product.Cost;
                }

                PostCustomerReturnJournal(conn, ret, product, restockValue);
                if (ret.RefundAmount > 0)
                {
                    creditNote = CreateCreditNote(conn, 0, ret.Id, null, ret.RefundAmount, ret.Reason, ret.ProcessedByUsername);
                }
            });

            if (creditNote != null)
            {
                await _auditService.LogActionAsync(ret.ProcessedByUsername, "Create", "CreditNote", creditNote.Id, creditNote);
            }

            await _auditService.LogActionAsync(ret.ProcessedByUsername, "Process", "CustomerReturn", ret.Id, ret);
        }

        public async Task ProcessSupplierReturnAsync(SupplierReturn ret)
        {
            DebitNote? debitNote = null;
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var product = conn.Find<Product>(ret.ProductId);
                if (product == null) return;

                if (ret.Quantity > product.StockQuantity)
                {
                    throw new InvalidOperationException($"Insufficient stock for return. Available: {product.StockQuantity}, Returning: {ret.Quantity}");
                }

                if (ret.CreditAmount <= 0)
                {
                    ret.CreditAmount = ret.Quantity * product.Cost;
                }

                conn.Insert(ret);

                var movement = new StockMovement
                {
                    ProductId = ret.ProductId,
                    QuantityChanged = ret.Quantity,
                    MovementType = "OUT",
                    Reason = "Supplier Return - Overstock/Defective",
                    Date = DateTime.Now,
                    Username = ret.ProcessedByUsername ?? "System",
                    UnitPrice = product.Price
                };
                conn.Insert(movement);

                BatchTrackingService.DeductBatchesOnIssue(conn, product, ret.Quantity, movement.Id);

                var previousStock = product.StockQuantity;
                product.StockQuantity -= ret.Quantity;
                conn.Update(product);
                LocationStockSync.ApplyDelta(conn, product.Id, product.StockQuantity - previousStock);

                PostSupplierReturnJournal(conn, ret, product);
                debitNote = CreateDebitNote(conn, ret.SupplierId, ret.Id, null, ret.CreditAmount, ret.Reason, ret.ProcessedByUsername ?? "System");
            });

            if (debitNote != null)
            {
                await _auditService.LogActionAsync(ret.ProcessedByUsername ?? "System", "Create", "DebitNote", debitNote.Id, debitNote);
            }

            await _auditService.LogActionAsync(ret.ProcessedByUsername ?? "System", "Process", "SupplierReturn", ret.Id, ret);
        }

        public async Task ProcessSalesOrderReturnAsync(int salesOrderId, List<(int itemId, int quantityToReturn, string condition, string reason, decimal refundAmount)> returns, string processedByUsername)
        {
            var creditNotes = new List<CreditNote>();
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var so = conn.Find<SalesOrder>(salesOrderId);
                if (so == null) throw new ArgumentException("Sales Order not found.");

                foreach (var ret in returns)
                {
                    if (ret.quantityToReturn <= 0) continue;

                    var item = conn.Find<SalesOrderItem>(ret.itemId);
                    if (item == null || item.SalesOrderId != salesOrderId) continue;

                    if (ret.quantityToReturn > item.QuantityDelivered)
                    {
                        throw new InvalidOperationException($"Cannot return more than delivered. Delivered: {item.QuantityDelivered}, Returning: {ret.quantityToReturn}");
                    }

                    item.QuantityDelivered -= ret.quantityToReturn;
                    conn.Update(item);

                    var customerReturn = new CustomerReturn
                    {
                        ReturnNumber = $"RET-SO-{so.SONumber}-{DateTime.Now:yyyyMMddHHmmss}-{item.ProductId}",
                        ProductId = item.ProductId,
                        Quantity = ret.quantityToReturn,
                        Reason = ret.reason,
                        Condition = ret.condition,
                        RefundAmount = ret.refundAmount,
                        ProcessedByUsername = processedByUsername,
                        ReturnDate = DateTime.Now,
                        OriginalReceiptId = salesOrderId.ToString(),
                        Resolution = "Restocked"
                    };
                    conn.Insert(customerReturn);

                    var product = conn.Find<Product>(item.ProductId);
                    if (product == null) continue;

                    decimal restockValue = 0;
                    if (ret.condition == "Resaleable")
                    {
                        var movement = new StockMovement
                        {
                            ProductId = item.ProductId,
                            QuantityChanged = ret.quantityToReturn,
                            MovementType = "IN",
                            Reason = $"Customer Return (SO: {so.SONumber})",
                            Date = DateTime.Now,
                            Username = processedByUsername,
                            UnitPrice = item.UnitPrice
                        };
                        conn.Insert(movement);

                        BatchTrackingService.RestoreBatchesOnReturn(conn, product, ret.quantityToReturn, movement.Id, product.Cost);

                        var previousStock = product.StockQuantity;
                        product.StockQuantity += ret.quantityToReturn;
                        conn.Update(product);
                        LocationStockSync.ApplyDelta(conn, product.Id, product.StockQuantity - previousStock);
                        restockValue = ret.quantityToReturn * product.Cost;
                    }

                    PostCustomerReturnJournal(conn, customerReturn, product, restockValue);
                    if (ret.refundAmount > 0)
                    {
                        var note = CreateCreditNote(conn, so.CustomerId, customerReturn.Id, salesOrderId, ret.refundAmount, ret.reason, processedByUsername);
                        creditNotes.Add(note);
                    }
                }

                var allItems = conn.Table<SalesOrderItem>().Where(i => i.SalesOrderId == salesOrderId).ToList();
                bool allDelivered = allItems.All(i => i.QuantityDelivered >= i.QuantityOrdered);

                so.Status = allDelivered ? "Delivered" : "Confirmed";
                so.DeliveryStatus = allItems.Any(i => i.QuantityDelivered > 0)
                    ? (allDelivered ? "Delivered" : "Partially Delivered")
                    : "Pending";

                so.DeliveryDate = so.Status == "Delivered" ? DateTime.Now : null;
                conn.Update(so);
            });

            foreach (var note in creditNotes)
            {
                await _auditService.LogActionAsync(processedByUsername, "Create", "CreditNote", note.Id, note);
            }

            await _auditService.LogActionAsync(processedByUsername, "Process", "SalesOrderReturn", salesOrderId, new { salesOrderId, lines = returns.Count });
        }

        public async Task ProcessPurchaseOrderReturnAsync(int purchaseOrderId, List<(int itemId, int quantityToReturn, string reason, decimal creditAmount)> returns, string processedByUsername)
        {
            var debitNotes = new List<DebitNote>();
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var po = conn.Find<PurchaseOrder>(purchaseOrderId);
                if (po == null) throw new ArgumentException("Purchase Order not found.");

                foreach (var ret in returns)
                {
                    if (ret.quantityToReturn <= 0) continue;

                    var item = conn.Find<PurchaseOrderItem>(ret.itemId);
                    if (item == null || item.PurchaseOrderId != purchaseOrderId) continue;

                    if (ret.quantityToReturn > item.QuantityReceived)
                    {
                        throw new InvalidOperationException($"Cannot return more than received. Received: {item.QuantityReceived}, Returning: {ret.quantityToReturn}");
                    }

                    var product = conn.Find<Product>(item.ProductId);
                    if (product == null) continue;

                    if (ret.quantityToReturn > product.StockQuantity)
                    {
                        throw new InvalidOperationException($"Insufficient stock for return. Available: {product.StockQuantity}, Returning: {ret.quantityToReturn}");
                    }

                    item.QuantityReceived -= ret.quantityToReturn;
                    conn.Update(item);

                    var creditAmount = ret.creditAmount > 0 ? ret.creditAmount : ret.quantityToReturn * item.UnitCost;

                    var supplierReturn = new SupplierReturn
                    {
                        ReturnNumber = $"RET-PO-{po.PONumber}-{DateTime.Now:yyyyMMddHHmmss}-{item.ProductId}",
                        SupplierId = po.SupplierId,
                        ProductId = item.ProductId,
                        Quantity = ret.quantityToReturn,
                        Reason = ret.reason,
                        Status = "Credited",
                        CreditAmount = creditAmount,
                        ProcessedByUsername = processedByUsername,
                        ReturnDate = DateTime.Now,
                        OriginalReceiptId = purchaseOrderId.ToString()
                    };
                    conn.Insert(supplierReturn);

                    var previousStock = product.StockQuantity;
                    product.StockQuantity -= ret.quantityToReturn;
                    conn.Update(product);
                    LocationStockSync.ApplyDelta(conn, product.Id, product.StockQuantity - previousStock);

                    var movement = new StockMovement
                    {
                        ProductId = item.ProductId,
                        QuantityChanged = ret.quantityToReturn,
                        MovementType = "OUT",
                        Reason = $"Supplier Return (PO: {po.PONumber})",
                        Date = DateTime.Now,
                        Username = processedByUsername,
                        UnitPrice = product.Price
                    };
                    conn.Insert(movement);

                    BatchTrackingService.DeductBatchesOnIssue(conn, product, ret.quantityToReturn, movement.Id);

                    PostSupplierReturnJournal(conn, supplierReturn, product);
                    debitNotes.Add(CreateDebitNote(conn, po.SupplierId, supplierReturn.Id, purchaseOrderId, creditAmount, ret.reason, processedByUsername));
                }

                var allItems = conn.Table<PurchaseOrderItem>().Where(i => i.PurchaseOrderId == purchaseOrderId).ToList();
                bool allReceived = allItems.All(i => i.QuantityReceived >= i.QuantityOrdered);

                po.Status = allReceived ? "Received" : "Shipped";
                po.ReceiptStatus = allItems.Any(i => i.QuantityReceived > 0)
                    ? (allReceived ? "Received" : "Partially Received")
                    : "Pending";

                po.ActualDeliveryDate = po.Status == "Received" ? DateTime.Now : null;
                conn.Update(po);
            });

            foreach (var note in debitNotes)
            {
                await _auditService.LogActionAsync(processedByUsername, "Create", "DebitNote", note.Id, note);
            }

            await _auditService.LogActionAsync(processedByUsername, "Process", "PurchaseOrderReturn", purchaseOrderId, new { purchaseOrderId, lines = returns.Count });
        }

        private static CreditNote CreateCreditNote(SQLiteConnection conn, int customerId, int customerReturnId, int? salesOrderId, decimal amount, string reason, string username)
        {
            var count = conn.Table<CreditNote>().Count() + 1;
            var note = new CreditNote
            {
                CreditNoteNumber = $"CN-{DateTime.Now:yyyyMMdd}-{count:D4}",
                CustomerId = customerId,
                CustomerReturnId = customerReturnId,
                SalesOrderId = salesOrderId,
                Amount = amount,
                IssueDate = DateTime.Now,
                Status = "Posted",
                Reason = reason,
                CreatedByUsername = username
            };
            conn.Insert(note);
            return note;
        }

        private static DebitNote CreateDebitNote(SQLiteConnection conn, int supplierId, int supplierReturnId, int? purchaseOrderId, decimal amount, string reason, string username)
        {
            var count = conn.Table<DebitNote>().Count() + 1;
            var note = new DebitNote
            {
                DebitNoteNumber = $"DN-{DateTime.Now:yyyyMMdd}-{count:D4}",
                SupplierId = supplierId,
                SupplierReturnId = supplierReturnId,
                PurchaseOrderId = purchaseOrderId,
                Amount = amount,
                IssueDate = DateTime.Now,
                Status = "Posted",
                Reason = reason,
                CreatedByUsername = username
            };
            conn.Insert(note);
            return note;
        }

        private static void PostCustomerReturnJournal(SQLiteConnection conn, CustomerReturn ret, Product product, decimal restockValue)
        {
            if (ret.RefundAmount <= 0 && restockValue <= 0) return;

            var journal = conn.Table<Journal>().Where(j => j.Type == "Sales").FirstOrDefault();
            if (journal == null) return;

            var entryCount = conn.Table<JournalEntry>().Where(e => e.JournalId == journal.Id).Count();
            var entry = new JournalEntry
            {
                EntryNumber = $"{journal.SequencePrefix}/{DateTime.Now.Year}/{(entryCount + 1):D5}",
                JournalId = journal.Id,
                Date = DateTime.Now,
                Reference = $"Customer Return: {ret.ReturnNumber}",
                State = "Posted"
            };
            conn.Insert(entry);

            var cashAccount = conn.Table<Account>().Where(a => a.Code == "101000").FirstOrDefault();
            int cashAccountId = cashAccount?.Id ?? 1;

            int revenueAccountId = product.IncomeAccountId ?? 0;
            if (revenueAccountId == 0)
            {
                var revAccount = conn.Table<Account>().Where(a => a.Code == "401000").FirstOrDefault();
                revenueAccountId = revAccount?.Id ?? 13;
            }

            if (ret.RefundAmount > 0)
            {
                conn.Insert(new JournalLine
                {
                    JournalEntryId = entry.Id,
                    AccountId = revenueAccountId,
                    ProductId = product.Id,
                    Label = $"Sales Return - {product.Name}",
                    Debit = ret.RefundAmount,
                    Credit = 0
                });

                conn.Insert(new JournalLine
                {
                    JournalEntryId = entry.Id,
                    AccountId = cashAccountId,
                    ProductId = product.Id,
                    Label = $"Customer Refund - {ret.ReturnNumber}",
                    Debit = 0,
                    Credit = ret.RefundAmount
                });
            }

            if (restockValue > 0)
            {
                var inventoryAccount = conn.Table<Account>().Where(a => a.Code == "120000").FirstOrDefault();
                int assetAccountId = inventoryAccount?.Id ?? 4;

                int cogsAccountId = product.ExpenseAccountId ?? 0;
                if (cogsAccountId == 0)
                {
                    var expAccount = conn.Table<Account>().Where(a => a.Code == "501000").FirstOrDefault();
                    cogsAccountId = expAccount?.Id ?? 16;
                }

                conn.Insert(new JournalLine
                {
                    JournalEntryId = entry.Id,
                    AccountId = assetAccountId,
                    ProductId = product.Id,
                    Label = $"Restock from Return - {product.Name}",
                    Debit = restockValue,
                    Credit = 0
                });

                conn.Insert(new JournalLine
                {
                    JournalEntryId = entry.Id,
                    AccountId = cogsAccountId,
                    ProductId = product.Id,
                    Label = $"COGS Reversal - {product.Name}",
                    Debit = 0,
                    Credit = restockValue
                });
            }
        }

        private static void PostSupplierReturnJournal(SQLiteConnection conn, SupplierReturn ret, Product product)
        {
            if (ret.CreditAmount <= 0) return;

            var journal = conn.Table<Journal>().Where(j => j.Type == "Purchase").FirstOrDefault();
            if (journal == null) return;

            var entryCount = conn.Table<JournalEntry>().Where(e => e.JournalId == journal.Id).Count();
            var entry = new JournalEntry
            {
                EntryNumber = $"{journal.SequencePrefix}/{DateTime.Now.Year}/{(entryCount + 1):D5}",
                JournalId = journal.Id,
                Date = DateTime.Now,
                Reference = $"Supplier Return: {ret.ReturnNumber}",
                State = "Posted"
            };
            conn.Insert(entry);

            var apAccount = conn.Table<Account>().Where(a => a.Code == "201000").FirstOrDefault();
            int apAccountId = apAccount?.Id ?? 7;

            var inventoryAccount = conn.Table<Account>().Where(a => a.Code == "120000").FirstOrDefault();
            int assetAccountId = inventoryAccount?.Id ?? 4;

            conn.Insert(new JournalLine
            {
                JournalEntryId = entry.Id,
                AccountId = apAccountId,
                ProductId = product.Id,
                Label = $"Supplier Credit - {ret.ReturnNumber}",
                Debit = ret.CreditAmount,
                Credit = 0
            });

            conn.Insert(new JournalLine
            {
                JournalEntryId = entry.Id,
                AccountId = assetAccountId,
                ProductId = product.Id,
                Label = $"Inventory Return - {product.Name}",
                Debit = 0,
                Credit = ret.CreditAmount
            });
        }

        public async Task<List<CustomerReturn>> GetCustomerReturnsAsync(DateTime from, DateTime to)
        {
            return await _databaseService.Connection.Table<CustomerReturn>()
                .Where(r => r.ReturnDate >= from && r.ReturnDate <= to)
                .ToListAsync();
        }

        public async Task<List<SupplierReturn>> GetSupplierReturnsAsync(DateTime from, DateTime to)
        {
            return await _databaseService.Connection.Table<SupplierReturn>()
                .Where(r => r.ReturnDate >= from && r.ReturnDate <= to)
                .ToListAsync();
        }

        public async Task<List<CreditNote>> GetCreditNotesAsync() =>
            await _databaseService.Connection.Table<CreditNote>()
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.IssueDate)
                .ToListAsync();

        public async Task<List<DebitNote>> GetDebitNotesAsync() =>
            await _databaseService.Connection.Table<DebitNote>()
                .Where(d => !d.IsDeleted)
                .OrderByDescending(d => d.IssueDate)
                .ToListAsync();

        public async Task<List<CreditNoteDisplayRow>> GetCreditNoteDisplayRowsAsync()
        {
            var notes = await GetCreditNotesAsync();
            var customers = await _databaseService.Connection.Table<Customer>().ToListAsync();
            var salesOrders = await _databaseService.Connection.Table<SalesOrder>().ToListAsync();
            var returns = await _databaseService.Connection.Table<CustomerReturn>().ToListAsync();

            return notes.Select(n =>
            {
                var customer = customers.FirstOrDefault(c => c.Id == n.CustomerId);
                var so = n.SalesOrderId.HasValue
                    ? salesOrders.FirstOrDefault(s => s.Id == n.SalesOrderId.Value)
                    : null;
                var ret = n.CustomerReturnId.HasValue
                    ? returns.FirstOrDefault(r => r.Id == n.CustomerReturnId.Value)
                    : null;

                return new CreditNoteDisplayRow
                {
                    Note = n,
                    CustomerName = customer?.Name ?? (n.CustomerId == 0 ? "Walk-in / POS" : $"Customer #{n.CustomerId}"),
                    LinkedDocument = so?.SONumber ?? ret?.ReturnNumber ?? "—",
                    LinkedReturnNumber = ret?.ReturnNumber ?? "—"
                };
            }).ToList();
        }

        public async Task<List<DebitNoteDisplayRow>> GetDebitNoteDisplayRowsAsync()
        {
            var notes = await GetDebitNotesAsync();
            var suppliers = await _databaseService.Connection.Table<Supplier>().ToListAsync();
            var purchaseOrders = await _databaseService.Connection.Table<PurchaseOrder>().ToListAsync();
            var returns = await _databaseService.Connection.Table<SupplierReturn>().ToListAsync();

            return notes.Select(n =>
            {
                var supplier = suppliers.FirstOrDefault(s => s.Id == n.SupplierId);
                var po = n.PurchaseOrderId.HasValue
                    ? purchaseOrders.FirstOrDefault(p => p.Id == n.PurchaseOrderId.Value)
                    : null;
                var ret = n.SupplierReturnId.HasValue
                    ? returns.FirstOrDefault(r => r.Id == n.SupplierReturnId.Value)
                    : null;

                return new DebitNoteDisplayRow
                {
                    Note = n,
                    SupplierName = supplier?.Name ?? $"Supplier #{n.SupplierId}",
                    LinkedDocument = po?.PONumber ?? ret?.ReturnNumber ?? "—",
                    LinkedReturnNumber = ret?.ReturnNumber ?? "—"
                };
            }).ToList();
        }
    }

    public class CreditNoteDisplayRow
    {
        public CreditNote Note { get; set; } = new();
        public string CustomerName { get; set; } = string.Empty;
        public string LinkedDocument { get; set; } = string.Empty;
        public string LinkedReturnNumber { get; set; } = string.Empty;

        public string DocumentNumber => Note.CreditNoteNumber;
        public DateTime IssueDate => Note.IssueDate;
        public decimal Amount => Note.Amount;
        public string Status => Note.Status;
        public string Reason => Note.Reason;
        public string CreatedBy => Note.CreatedByUsername;
    }

    public class DebitNoteDisplayRow
    {
        public DebitNote Note { get; set; } = new();
        public string SupplierName { get; set; } = string.Empty;
        public string LinkedDocument { get; set; } = string.Empty;
        public string LinkedReturnNumber { get; set; } = string.Empty;

        public string DocumentNumber => Note.DebitNoteNumber;
        public DateTime IssueDate => Note.IssueDate;
        public decimal Amount => Note.Amount;
        public string Status => Note.Status;
        public string Reason => Note.Reason;
        public string CreatedBy => Note.CreatedByUsername;
    }
}
