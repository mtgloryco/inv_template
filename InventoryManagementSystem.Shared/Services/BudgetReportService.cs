using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class BudgetReportService
    {
        private readonly DatabaseService _databaseService;
        private readonly AuditService? _auditService;

        public BudgetReportService(DatabaseService databaseService, AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _auditService = auditService;
        }

        public async Task<List<BudgetLine>> GetBudgetLinesAsync(int fiscalYear, int? periodMonth = null)
        {
            var query = _databaseService.Connection.Table<BudgetLine>()
                .Where(b => b.FiscalYear == fiscalYear);

            var lines = await query.ToListAsync();
            if (periodMonth.HasValue)
            {
                lines = lines.Where(b => b.PeriodMonth == 0 || b.PeriodMonth == periodMonth.Value).ToList();
            }

            return lines.OrderBy(b => b.AccountId).ThenBy(b => b.PeriodMonth).ToList();
        }

        public async Task SaveBudgetLineAsync(BudgetLine line)
        {
            var isCreate = line.Id == 0;
            BudgetLine? old = null;
            if (isCreate)
            {
                await _databaseService.Connection.InsertAsync(line);
            }
            else
            {
                old = await _databaseService.Connection.FindAsync<BudgetLine>(line.Id);
                await _databaseService.Connection.UpdateAsync(line);
            }

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    UserSession.CurrentUser?.Username ?? "System",
                    isCreate ? "Create" : "Update",
                    "BudgetLine", line.Id, line, old);
            }
        }

        public async Task DeleteBudgetLineAsync(int id)
        {
            var line = await _databaseService.Connection.FindAsync<BudgetLine>(id);
            if (line != null)
            {
                await _databaseService.Connection.DeleteAsync(line);
                if (_auditService != null)
                {
                    await _auditService.LogActionAsync(
                        UserSession.CurrentUser?.Username ?? "System",
                        "Delete", "BudgetLine", id, null, line);
                }
            }
        }

        public async Task<List<BudgetVsActualLine>> GetBudgetVsActualAsync(int fiscalYear, int? periodMonth = null)
        {
            var accounts = await _databaseService.Connection.Table<Account>()
                .Where(a => !a.IsDeleted && a.IsActive)
                .ToListAsync();
            var budgetLines = await GetBudgetLinesAsync(fiscalYear, periodMonth);

            DateTime periodStart;
            DateTime periodEnd;
            if (periodMonth.HasValue && periodMonth.Value >= 1 && periodMonth.Value <= 12)
            {
                periodStart = new DateTime(fiscalYear, periodMonth.Value, 1);
                periodEnd = periodStart.AddMonths(1).AddTicks(-1);
            }
            else
            {
                periodStart = new DateTime(fiscalYear, 1, 1);
                periodEnd = new DateTime(fiscalYear, 12, 31, 23, 59, 59);
            }

            var entries = await _databaseService.Connection.Table<JournalEntry>()
                .Where(e => !e.IsDeleted && e.State == "Posted" && e.Date >= periodStart && e.Date <= periodEnd)
                .ToListAsync();
            var entryIds = entries.Select(e => e.Id).ToHashSet();

            var journalLines = await _databaseService.Connection.Table<JournalLine>()
                .Where(l => !l.IsDeleted)
                .ToListAsync();
            journalLines = journalLines.Where(l => entryIds.Contains(l.JournalEntryId)).ToList();

            var actualByAccount = journalLines
                .GroupBy(l => l.AccountId)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Debit - l.Credit));

            var budgetByAccount = budgetLines
                .GroupBy(b => b.AccountId)
                .ToDictionary(g => g.Key, g => g.Sum(b => b.BudgetAmount));

            var accountIds = budgetByAccount.Keys.Union(actualByAccount.Keys).Distinct();
            var results = new List<BudgetVsActualLine>();

            foreach (var accountId in accountIds)
            {
                var account = accounts.FirstOrDefault(a => a.Id == accountId);
                if (account == null)
                {
                    continue;
                }

                budgetByAccount.TryGetValue(accountId, out var budget);
                actualByAccount.TryGetValue(accountId, out var actual);

                if (account.Type.StartsWith("Income", StringComparison.OrdinalIgnoreCase)
                    || account.Type.StartsWith("Liability", StringComparison.OrdinalIgnoreCase)
                    || account.Type.StartsWith("Equity", StringComparison.OrdinalIgnoreCase))
                {
                    actual = -actual;
                }

                if (budget == 0 && actual == 0)
                {
                    continue;
                }

                results.Add(new BudgetVsActualLine
                {
                    AccountCode = account.Code,
                    AccountName = account.Name,
                    AccountType = account.Type,
                    BudgetAmount = budget,
                    ActualAmount = actual
                });
            }

            return results.OrderBy(r => r.AccountCode).ToList();
        }
    }
}
