using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class AccountService
    {
        private readonly DatabaseService _databaseService;

        public AccountService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<Account>> GetAllAccountsAsync()
        {
            return await _databaseService.Connection.Table<Account>()
                .OrderBy(a => a.Code)
                .ToListAsync();
        }

        public async Task<Account?> GetAccountByCodeAsync(string code)
        {
            return await _databaseService.Connection.Table<Account>()
                .Where(a => a.Code == code)
                .FirstOrDefaultAsync();
        }

        public async Task AddAccountAsync(Account account)
        {
            var existing = await GetAccountByCodeAsync(account.Code);
            if (existing != null)
            {
                throw new InvalidOperationException($"An account with code {account.Code} already exists.");
            }
            await _databaseService.Connection.InsertAsync(account);
        }

        public async Task UpdateAccountAsync(Account account)
        {
            await _databaseService.Connection.UpdateAsync(account);
        }

        public async Task DeleteAccountAsync(int id)
        {
            var account = await _databaseService.Connection.FindAsync<Account>(id);
            if (account != null)
            {
                await _databaseService.Connection.DeleteAsync(account);
            }
        }

        public async Task<List<AccountTransactionRow>> GetAccountTransactionsAsync(int accountId)
        {
            var lines = await _databaseService.Connection.Table<JournalLine>()
                .Where(l => l.AccountId == accountId)
                .ToListAsync();

            var result = new List<AccountTransactionRow>();
            var entries = await _databaseService.Connection.Table<JournalEntry>().ToListAsync();
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();

            foreach (var line in lines)
            {
                var entry = entries.Find(e => e.Id == line.JournalEntryId);
                var product = line.ProductId.HasValue ? products.Find(p => p.Id == line.ProductId.Value) : null;

                result.Add(new AccountTransactionRow
                {
                    Date = entry?.Date ?? DateTime.Now,
                    EntryNumber = entry?.EntryNumber ?? string.Empty,
                    Reference = entry?.Reference ?? string.Empty,
                    Label = line.Label,
                    ProductName = product?.Name ?? string.Empty,
                    Debit = line.Debit,
                    Credit = line.Credit
                });
            }

            return result.OrderByDescending(r => r.Date).ToList();
        }
    }

    public class AccountTransactionRow
    {
        public DateTime Date { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}
