using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class VatExportService
    {
        private readonly DatabaseService _databaseService;

        public VatExportService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<VatReturnSummary> ComputeVatReturnAsync(DateTime periodStart, DateTime periodEnd)
        {
            var start = periodStart.Date;
            var end = periodEnd.Date.AddDays(1).AddTicks(-1);

            var entries = await _databaseService.Connection.Table<JournalEntry>()
                .Where(e => !e.IsDeleted && e.Date >= start && e.Date <= end && e.State == "Posted")
                .ToListAsync();
            var entryIds = entries.Select(e => e.Id).ToHashSet();

            var lines = await _databaseService.Connection.Table<JournalLine>()
                .Where(l => !l.IsDeleted)
                .ToListAsync();
            lines = lines.Where(l => entryIds.Contains(l.JournalEntryId)).ToList();

            var accounts = await _databaseService.Connection.Table<Account>().ToListAsync();
            var vatPayable = accounts.FirstOrDefault(a => a.Code == "220000");
            var vatReceivable = accounts.FirstOrDefault(a => a.Code == "125000");

            decimal outputVat = 0;
            decimal inputVat = 0;

            if (vatPayable != null)
            {
                outputVat = lines.Where(l => l.AccountId == vatPayable.Id).Sum(l => l.Credit - l.Debit);
            }

            if (vatReceivable != null)
            {
                inputVat = lines.Where(l => l.AccountId == vatReceivable.Id).Sum(l => l.Debit - l.Credit);
            }

            var salesOrders = await _databaseService.Connection.Table<SalesOrder>()
                .Where(o => !o.IsDeleted && o.BillingStatus == "Invoiced" && o.OrderDate >= start && o.OrderDate <= end)
                .ToListAsync();
            var purchaseOrders = await _databaseService.Connection.Table<PurchaseOrder>()
                .Where(o => !o.IsDeleted && o.BillingStatus == "Billed" && o.OrderDate >= start && o.OrderDate <= end)
                .ToListAsync();

            return new VatReturnSummary
            {
                PeriodStart = start,
                PeriodEnd = periodEnd.Date,
                OutputVat = Math.Max(0, outputVat),
                InputVat = Math.Max(0, inputVat),
                TaxableSales = salesOrders.Sum(o => o.TotalAmount),
                TaxablePurchases = purchaseOrders.Sum(o => o.TotalAmount)
            };
        }

        public string BuildVatReturnCsv(VatReturnSummary summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Field,Value");
            sb.AppendLine($"Period Start,{summary.PeriodStart:yyyy-MM-dd}");
            sb.AppendLine($"Period End,{summary.PeriodEnd:yyyy-MM-dd}");
            sb.AppendLine($"Taxable Sales,{summary.TaxableSales:N2}");
            sb.AppendLine($"Output VAT,{summary.OutputVat:N2}");
            sb.AppendLine($"Taxable Purchases,{summary.TaxablePurchases:N2}");
            sb.AppendLine($"Input VAT,{summary.InputVat:N2}");
            sb.AppendLine($"Net VAT Payable,{summary.NetVatPayable:N2}");
            return sb.ToString();
        }
    }
}
