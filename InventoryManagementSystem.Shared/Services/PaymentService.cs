using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class PaymentService
    {
        private readonly DatabaseService _databaseService;
        private readonly AuditService? _auditService;

        public PaymentService(DatabaseService databaseService, AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _auditService = auditService;
        }

        // Bank CRUD
        public async Task<List<Bank>> GetAllBanksAsync()
        {
            return await _databaseService.Connection.Table<Bank>()
                .OrderBy(b => b.Name)
                .ToListAsync();
        }

        public async Task AddBankAsync(Bank bank)
        {
            await _databaseService.Connection.InsertAsync(bank);
        }

        public async Task UpdateBankAsync(Bank bank)
        {
            await _databaseService.Connection.UpdateAsync(bank);
        }

        public async Task DeleteBankAsync(int bankId)
        {
            var bank = await _databaseService.Connection.FindAsync<Bank>(bankId);
            if (bank != null)
            {
                await _databaseService.Connection.DeleteAsync(bank);
            }
        }

        // Bank Account CRUD
        public async Task<List<BankAccount>> GetAllBankAccountsAsync()
        {
            return await _databaseService.Connection.Table<BankAccount>()
                .ToListAsync();
        }

        public async Task AddBankAccountAsync(BankAccount account)
        {
            await _databaseService.Connection.InsertAsync(account);
        }

        public async Task UpdateBankAccountAsync(BankAccount account)
        {
            await _databaseService.Connection.UpdateAsync(account);
        }

        public async Task DeleteBankAccountAsync(int accountId)
        {
            var account = await _databaseService.Connection.FindAsync<BankAccount>(accountId);
            if (account != null)
            {
                await _databaseService.Connection.DeleteAsync(account);
            }
        }

        // --- Invoice payments (partial / full) ---

        public async Task<List<InvoicePayment>> GetPaymentsForDocumentAsync(string documentType, int documentId)
        {
            return await _databaseService.Connection.Table<InvoicePayment>()
                .Where(p => !p.IsDeleted && p.DocumentType == documentType && p.DocumentId == documentId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        public async Task<decimal> GetDocumentTotalAsync(string documentType, int documentId)
        {
            if (documentType == "SalesOrder")
            {
                var so = await _databaseService.Connection.FindAsync<SalesOrder>(documentId);
                return so?.TotalAmount ?? 0;
            }

            if (documentType == "PurchaseOrder")
            {
                var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(documentId);
                return po?.TotalAmount ?? 0;
            }

            return 0;
        }

        public async Task<decimal> GetDocumentCreditsAsync(string documentType, int documentId)
        {
            if (documentType == "SalesOrder")
            {
                var notes = await _databaseService.Connection.Table<CreditNote>()
                    .Where(c => !c.IsDeleted && c.Status == "Posted" && c.SalesOrderId == documentId)
                    .ToListAsync();
                return notes.Sum(c => c.Amount);
            }

            if (documentType == "PurchaseOrder")
            {
                var notes = await _databaseService.Connection.Table<DebitNote>()
                    .Where(d => !d.IsDeleted && d.Status == "Posted" && d.PurchaseOrderId == documentId)
                    .ToListAsync();
                return notes.Sum(d => d.Amount);
            }

            return 0;
        }

        public async Task<decimal> GetAmountPaidAsync(string documentType, int documentId)
        {
            var payments = await GetPaymentsForDocumentAsync(documentType, documentId);
            return payments.Sum(p => p.Amount);
        }

        public async Task<decimal> GetOpenBalanceAsync(string documentType, int documentId)
        {
            var total = await GetDocumentTotalAsync(documentType, documentId);
            var credits = await GetDocumentCreditsAsync(documentType, documentId);
            var paid = await GetAmountPaidAsync(documentType, documentId);
            return Math.Max(0, total - credits - paid);
        }

        public async Task<InvoicePayment> RecordInvoicePaymentAsync(
            string documentType,
            int documentId,
            decimal amount,
            string paymentMethod,
            string username,
            int? bankAccountId = null,
            string reference = "",
            DateTime? paymentDate = null)
        {
            if (amount <= 0)
            {
                throw new InvalidOperationException("Payment amount must be greater than zero.");
            }

            var openBalance = await GetOpenBalanceAsync(documentType, documentId);
            if (amount > openBalance + 0.01m)
            {
                throw new InvalidOperationException($"Payment amount ({amount:N2}) exceeds open balance ({openBalance:N2}).");
            }

            string currency = "RWF";
            string docNumber = string.Empty;
            if (documentType == "SalesOrder")
            {
                var so = await _databaseService.Connection.FindAsync<SalesOrder>(documentId)
                    ?? throw new InvalidOperationException("Sales order not found.");
                if (so.BillingStatus != "Invoiced")
                {
                    throw new InvalidOperationException("Invoice must be posted before recording payment.");
                }

                currency = so.Currency;
                docNumber = so.SONumber;
            }
            else if (documentType == "PurchaseOrder")
            {
                var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(documentId)
                    ?? throw new InvalidOperationException("Purchase order not found.");
                if (po.BillingStatus != "Billed")
                {
                    throw new InvalidOperationException("Vendor bill must be posted before recording payment.");
                }

                currency = po.Currency;
                docNumber = po.PONumber;
            }
            else
            {
                throw new InvalidOperationException($"Unsupported document type: {documentType}");
            }

            var payment = new InvoicePayment
            {
                PaymentNumber = await GeneratePaymentNumberAsync(),
                DocumentType = documentType,
                DocumentId = documentId,
                Amount = amount,
                Currency = currency,
                PaymentDate = paymentDate ?? DateTime.Now,
                PaymentMethod = paymentMethod,
                BankAccountId = bankAccountId,
                Reference = reference,
                CreatedByUsername = username
            };

            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Insert(payment);
                PostPaymentJournalEntry(conn, payment, docNumber);
            });

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "PaymentRecorded", documentType, documentId, payment);
            }

            return payment;
        }

        private void PostPaymentJournalEntry(SQLite.SQLiteConnection conn, InvoicePayment payment, string docNumber)
        {
            var journalType = payment.PaymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase) ? "Cash" : "Bank";
            var journal = conn.Table<Journal>().FirstOrDefault(j => j.Type == journalType)
                ?? conn.Table<Journal>().FirstOrDefault(j => j.Type == "Bank");

            if (journal == null)
            {
                return;
            }

            var entryCount = conn.Table<JournalEntry>().Count(e => e.JournalId == journal.Id);
            var entry = new JournalEntry
            {
                EntryNumber = $"{journal.SequencePrefix}/{DateTime.Now.Year}/{(entryCount + 1):D5}",
                JournalId = journal.Id,
                Date = payment.PaymentDate,
                Reference = $"Payment {payment.PaymentNumber} - {docNumber}",
                State = "Posted"
            };
            conn.Insert(entry);

            var bankAccount = conn.Table<Account>().FirstOrDefault(a => a.Code == "102000");
            var cashAccount = conn.Table<Account>().FirstOrDefault(a => a.Code == "101000");
            var arAccount = conn.Table<Account>().FirstOrDefault(a => a.Code == "111000");
            var apAccount = conn.Table<Account>().FirstOrDefault(a => a.Code == "201000");

            int liquidityAccountId = payment.PaymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase)
                ? (cashAccount?.Id ?? journal.DefaultAccountId ?? 1)
                : (bankAccount?.Id ?? journal.DefaultAccountId ?? 2);

            if (payment.DocumentType == "SalesOrder")
            {
                int receivableId = arAccount?.Id ?? 3;
                conn.Insert(new JournalLine
                {
                    JournalEntryId = entry.Id,
                    AccountId = liquidityAccountId,
                    Label = $"Customer payment - {docNumber}",
                    Debit = payment.Amount,
                    Credit = 0
                });
                conn.Insert(new JournalLine
                {
                    JournalEntryId = entry.Id,
                    AccountId = receivableId,
                    Label = $"Customer payment - {docNumber}",
                    Debit = 0,
                    Credit = payment.Amount
                });
            }
            else
            {
                int payableId = apAccount?.Id ?? 7;
                conn.Insert(new JournalLine
                {
                    JournalEntryId = entry.Id,
                    AccountId = payableId,
                    Label = $"Vendor payment - {docNumber}",
                    Debit = payment.Amount,
                    Credit = 0
                });
                conn.Insert(new JournalLine
                {
                    JournalEntryId = entry.Id,
                    AccountId = liquidityAccountId,
                    Label = $"Vendor payment - {docNumber}",
                    Debit = 0,
                    Credit = payment.Amount
                });
            }
        }

        private async Task<string> GeneratePaymentNumberAsync()
        {
            var year = DateTime.Now.Year;
            var count = await _databaseService.Connection.Table<InvoicePayment>().CountAsync();
            return $"PAY-{year}-{(count + 1):D5}";
        }

        // --- Bank reconciliation ---

        public async Task<BankStatement> ImportBankStatementAsync(
            int bankAccountId,
            DateTime statementDate,
            decimal openingBalance,
            decimal closingBalance,
            IEnumerable<(DateTime date, string description, decimal amount, string reference)> lines,
            string reference = "")
        {
            var statement = new BankStatement
            {
                BankAccountId = bankAccountId,
                StatementDate = statementDate,
                OpeningBalance = openingBalance,
                ClosingBalance = closingBalance,
                Reference = reference,
                ImportedAt = DateTime.Now
            };

            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                conn.Insert(statement);
                foreach (var line in lines)
                {
                    conn.Insert(new BankStatementLine
                    {
                        BankStatementId = statement.Id,
                        TransactionDate = line.date,
                        Description = line.description,
                        Amount = line.amount,
                        Reference = line.reference
                    });
                }
            });

            return statement;
        }

        public async Task MatchPaymentToStatementLineAsync(int paymentId, int statementLineId, string username)
        {
            var payment = await _databaseService.Connection.FindAsync<InvoicePayment>(paymentId)
                ?? throw new InvalidOperationException("Payment not found.");
            var line = await _databaseService.Connection.FindAsync<BankStatementLine>(statementLineId)
                ?? throw new InvalidOperationException("Bank statement line not found.");

            if (line.IsReconciled)
            {
                throw new InvalidOperationException("Statement line is already reconciled.");
            }

            if (Math.Abs(line.Amount) != payment.Amount)
            {
                throw new InvalidOperationException("Payment amount does not match statement line amount.");
            }

            payment.BankStatementLineId = statementLineId;
            line.IsReconciled = true;
            line.MatchedPaymentId = paymentId;

            await _databaseService.Connection.UpdateAsync(payment);
            await _databaseService.Connection.UpdateAsync(line);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "BankReconciled", "InvoicePayment", paymentId,
                    new { paymentId, statementLineId });
            }
        }

        public async Task<List<ReconciliationCandidate>> GetUnreconciledPaymentsAsync(int? bankAccountId = null)
        {
            var payments = await _databaseService.Connection.Table<InvoicePayment>()
                .Where(p => !p.IsDeleted && p.BankStatementLineId == null)
                .ToListAsync();

            if (bankAccountId.HasValue)
            {
                payments = payments.Where(p => p.BankAccountId == bankAccountId || p.BankAccountId == null).ToList();
            }

            return payments.Select(p => new ReconciliationCandidate
            {
                Payment = p,
                Label = $"{p.PaymentNumber} ({p.DocumentType})",
                Amount = p.Amount,
                Date = p.PaymentDate,
                IsMatched = false
            }).OrderByDescending(c => c.Date).ToList();
        }

        public async Task<List<ReconciliationCandidate>> GetUnreconciledStatementLinesAsync(int bankAccountId)
        {
            var statements = await _databaseService.Connection.Table<BankStatement>()
                .Where(s => s.BankAccountId == bankAccountId)
                .ToListAsync();
            var statementIds = statements.Select(s => s.Id).ToHashSet();

            var lines = await _databaseService.Connection.Table<BankStatementLine>()
                .Where(l => !l.IsReconciled)
                .ToListAsync();

            lines = lines.Where(l => statementIds.Contains(l.BankStatementId)).ToList();

            return lines.Select(l => new ReconciliationCandidate
            {
                StatementLine = l,
                Label = l.Description,
                Amount = l.Amount,
                Date = l.TransactionDate,
                IsMatched = false
            }).OrderByDescending(c => c.Date).ToList();
        }

        public async Task<List<BankStatement>> GetBankStatementsAsync(int bankAccountId)
        {
            return await _databaseService.Connection.Table<BankStatement>()
                .Where(s => s.BankAccountId == bankAccountId)
                .OrderByDescending(s => s.StatementDate)
                .ToListAsync();
        }
    }
}
