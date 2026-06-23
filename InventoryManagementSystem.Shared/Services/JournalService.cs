using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class JournalService
    {
        private readonly DatabaseService _databaseService;

        public JournalService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<Journal>> GetAllJournalsAsync()
        {
            return await _databaseService.Connection.Table<Journal>()
                .OrderBy(j => j.Name)
                .ToListAsync();
        }

        public async Task<List<JournalListItem>> GetJournalListItemsAsync()
        {
            var journals = await GetAllJournalsAsync();
            var accounts = await _databaseService.Connection.Table<Account>().ToListAsync();

            var items = new List<JournalListItem>();
            foreach (var journal in journals)
            {
                var accountDisplay = string.Empty;
                if (journal.DefaultAccountId.HasValue)
                {
                    var account = accounts.Find(a => a.Id == journal.DefaultAccountId.Value);
                    if (account != null)
                        accountDisplay = $"{account.Code} {account.Name}";
                }
                items.Add(new JournalListItem { Journal = journal, DefaultAccountDisplay = accountDisplay });
            }
            return items;
        }

        public async Task AddJournalAsync(Journal journal)
        {
            await _databaseService.Connection.InsertAsync(journal);
        }

        public async Task UpdateJournalAsync(Journal journal)
        {
            await _databaseService.Connection.UpdateAsync(journal);
        }

        public async Task DeleteJournalAsync(int id)
        {
            var journal = await _databaseService.Connection.FindAsync<Journal>(id);
            if (journal != null)
                await _databaseService.Connection.DeleteAsync(journal);
        }
    }
}
