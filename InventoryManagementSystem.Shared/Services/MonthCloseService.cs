using System;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class MonthCloseService
    {
        private readonly DatabaseService _databaseService;
        private readonly AccountingReportService _accountingReportService;
        private readonly PaymentService _paymentService;
        private readonly AuditService? _auditService;

        public MonthCloseService(
            DatabaseService databaseService,
            AccountingReportService accountingReportService,
            PaymentService paymentService,
            AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _accountingReportService = accountingReportService;
            _paymentService = paymentService;
            _auditService = auditService;
        }

        public async Task<MonthCloseSummary> GetMonthCloseSummaryAsync(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddTicks(-1);

            var journalLines = await _databaseService.Connection.Table<JournalLine>().ToListAsync();
            var entries = await _databaseService.Connection.Table<JournalEntry>()
                .Where(e => e.Date >= start && e.Date <= end && e.State == "Posted")
                .ToListAsync();
            var entryIds = entries.Select(e => e.Id).ToHashSet();

            var periodLines = journalLines.Where(l => entryIds.Contains(l.JournalEntryId)).ToList();
            var totalDebits = periodLines.Sum(l => l.Debit);
            var totalCredits = periodLines.Sum(l => l.Credit);

            var reports = await _accountingReportService.GetAllReportsAsync();
            var pnlReport = reports.FirstOrDefault(r => r.Name == "Profit and Loss");
            decimal netProfit = 0;
            if (pnlReport != null)
            {
                var pnlLines = await _accountingReportService.ComputeReportBalancesAsync(pnlReport.Id);
                var netLine = pnlLines.FirstOrDefault(l => l.Code == "pnl_net_profit_year");
                netProfit = netLine?.Balance ?? 0;
            }

            var invoicedSales = await _databaseService.Connection.Table<SalesOrder>()
                .Where(so => so.BillingStatus == "Invoiced")
                .ToListAsync();
            var openAr = 0;
            foreach (var so in invoicedSales)
            {
                var open = await _paymentService.GetOpenBalanceAsync("SalesOrder", so.Id);
                if (open > 0.01m) openAr++;
            }

            var billedPos = await _databaseService.Connection.Table<PurchaseOrder>()
                .Where(po => po.BillingStatus == "Billed")
                .ToListAsync();
            var openAp = 0;
            foreach (var po in billedPos)
            {
                var open = await _paymentService.GetOpenBalanceAsync("PurchaseOrder", po.Id);
                if (open > 0.01m) openAp++;
            }

            return new MonthCloseSummary
            {
                Year = year,
                Month = month,
                TotalDebits = totalDebits,
                TotalCredits = totalCredits,
                NetProfit = netProfit,
                PostedEntryCount = entries.Count,
                OpenArCount = openAr,
                OpenApCount = openAp
            };
        }

        public async Task<bool> ValidateTrialBalanceAsync()
        {
            var journalLines = await _databaseService.Connection.Table<JournalLine>().ToListAsync();
            var totalDebits = journalLines.Sum(l => l.Debit);
            var totalCredits = journalLines.Sum(l => l.Credit);
            return Math.Abs(totalDebits - totalCredits) < 0.01m;
        }

        public async Task<MonthCloseSummary> RunMonthCloseAsync(int year, int month, string username)
        {
            var summary = await GetMonthCloseSummaryAsync(year, month);
            if (!summary.IsBalanced)
            {
                throw new InvalidOperationException(
                    $"Month {year}-{month:D2} is not balanced (debits {summary.TotalDebits:N2} vs credits {summary.TotalCredits:N2}).");
            }

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "MonthClose", "AccountingPeriod", month, summary);
            }

            return summary;
        }
    }
}
