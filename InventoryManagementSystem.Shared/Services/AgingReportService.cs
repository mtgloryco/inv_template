using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class AgingReportService
    {
        private readonly DatabaseService _databaseService;

        public AgingReportService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<AgingLine>> GetAccountsReceivableAgingAsync(DateTime? asOf = null)
        {
            var reportDate = (asOf ?? DateTime.Today).Date;
            var orders = await _databaseService.Connection.Table<SalesOrder>()
                .Where(o => !o.IsDeleted && o.BillingStatus == "Invoiced" && !o.IsPosSale && o.Status != "Cancelled")
                .ToListAsync();
            var customers = await _databaseService.Connection.Table<Customer>().ToListAsync();
            var creditNotes = await _databaseService.Connection.Table<CreditNote>()
                .Where(c => !c.IsDeleted && c.Status == "Posted")
                .ToListAsync();
            var payments = await _databaseService.Connection.Table<InvoicePayment>()
                .Where(p => !p.IsDeleted && p.DocumentType == "SalesOrder")
                .ToListAsync();
            var paymentTerms = await _databaseService.Connection.Table<PaymentTerm>().ToListAsync();

            var lines = new List<AgingLine>();
            foreach (var order in orders)
            {
                var credits = creditNotes.Where(c => c.SalesOrderId == order.Id).Sum(c => c.Amount);
                var paid = payments.Where(p => p.DocumentId == order.Id).Sum(p => p.Amount);
                var openBalance = order.TotalAmount - credits - paid;
                if (openBalance <= 0)
                {
                    continue;
                }

                var dueDays = ResolveDueDays(order.PaymentTerms, paymentTerms);
                var dueDate = order.OrderDate.Date.AddDays(dueDays);
                var daysOverdue = Math.Max(0, (reportDate - dueDate).Days);
                var customer = customers.FirstOrDefault(c => c.Id == order.CustomerId);

                lines.Add(new AgingLine
                {
                    PartnerName = customer?.Name ?? "Unknown Customer",
                    DocumentNumber = order.SONumber,
                    DocumentDate = order.OrderDate,
                    DueDate = dueDate,
                    TotalAmount = order.TotalAmount,
                    OpenBalance = openBalance,
                    DaysOverdue = daysOverdue,
                    AgingBucket = GetBucket(daysOverdue)
                });
            }

            return lines.OrderByDescending(l => l.DaysOverdue).ThenBy(l => l.PartnerName).ToList();
        }

        public async Task<List<AgingLine>> GetAccountsPayableAgingAsync(DateTime? asOf = null)
        {
            var reportDate = (asOf ?? DateTime.Today).Date;
            var orders = await _databaseService.Connection.Table<PurchaseOrder>()
                .Where(o => !o.IsDeleted && o.BillingStatus == "Billed" && o.Status != "Cancelled")
                .ToListAsync();
            var suppliers = await _databaseService.Connection.Table<Supplier>().ToListAsync();
            var debitNotes = await _databaseService.Connection.Table<DebitNote>()
                .Where(d => !d.IsDeleted && d.Status == "Posted")
                .ToListAsync();
            var payments = await _databaseService.Connection.Table<InvoicePayment>()
                .Where(p => !p.IsDeleted && p.DocumentType == "PurchaseOrder")
                .ToListAsync();
            var paymentTerms = await _databaseService.Connection.Table<PaymentTerm>().ToListAsync();

            var lines = new List<AgingLine>();
            foreach (var order in orders)
            {
                var debits = debitNotes.Where(d => d.PurchaseOrderId == order.Id).Sum(d => d.Amount);
                var paid = payments.Where(p => p.DocumentId == order.Id).Sum(p => p.Amount);
                var openBalance = order.TotalAmount - debits - paid;
                if (openBalance <= 0)
                {
                    continue;
                }

                var dueDays = ResolveDueDays(order.PaymentTerms, paymentTerms);
                var dueDate = order.OrderDate.Date.AddDays(dueDays);
                var daysOverdue = Math.Max(0, (reportDate - dueDate).Days);
                var supplier = suppliers.FirstOrDefault(s => s.Id == order.SupplierId);

                lines.Add(new AgingLine
                {
                    PartnerName = supplier?.Name ?? "Unknown Supplier",
                    DocumentNumber = order.PONumber,
                    DocumentDate = order.OrderDate,
                    DueDate = dueDate,
                    TotalAmount = order.TotalAmount,
                    OpenBalance = openBalance,
                    DaysOverdue = daysOverdue,
                    AgingBucket = GetBucket(daysOverdue)
                });
            }

            return lines.OrderByDescending(l => l.DaysOverdue).ThenBy(l => l.PartnerName).ToList();
        }

        public AgingSummary Summarize(IEnumerable<AgingLine> lines)
        {
            var summary = new AgingSummary();
            foreach (var line in lines)
            {
                summary.TotalOpen += line.OpenBalance;
                switch (line.AgingBucket)
                {
                    case "Current": summary.Current += line.OpenBalance; break;
                    case "1-30": summary.Days1To30 += line.OpenBalance; break;
                    case "31-60": summary.Days31To60 += line.OpenBalance; break;
                    case "61-90": summary.Days61To90 += line.OpenBalance; break;
                    default: summary.Over90 += line.OpenBalance; break;
                }
            }

            return summary;
        }

        public static int ParseDueDays(string paymentTerms)
        {
            if (string.IsNullOrWhiteSpace(paymentTerms)
                || paymentTerms.Contains("Immediate", StringComparison.OrdinalIgnoreCase)
                || paymentTerms.Contains("Direct", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var digits = new string(paymentTerms.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var days) ? days : 30;
        }

        internal static int ResolveDueDays(string paymentTerms, List<PaymentTerm> terms)
        {
            var match = terms.FirstOrDefault(t => t.Name.Equals(paymentTerms, StringComparison.OrdinalIgnoreCase));
            if (match != null && match.DueDays >= 0)
            {
                return match.DueDays;
            }

            return ParseDueDays(paymentTerms);
        }

        internal static string GetBucket(int daysOverdue) => daysOverdue switch
        {
            0 => "Current",
            <= 30 => "1-30",
            <= 60 => "31-60",
            <= 90 => "61-90",
            _ => "90+"
        };
    }
}
