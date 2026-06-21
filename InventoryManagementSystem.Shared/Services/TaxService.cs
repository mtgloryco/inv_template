using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class TaxService
    {
        private readonly DatabaseService _databaseService;

        public TaxService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<Tax>> GetAllTaxesAsync()
        {
            return await _databaseService.Connection.Table<Tax>()
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task AddTaxAsync(Tax tax)
        {
            await _databaseService.Connection.InsertAsync(tax);
        }

        public async Task UpdateTaxAsync(Tax tax)
        {
            await _databaseService.Connection.UpdateAsync(tax);
        }

        public async Task DeleteTaxAsync(int taxId)
        {
            var tax = await _databaseService.Connection.FindAsync<Tax>(taxId);
            if (tax != null)
            {
                await _databaseService.Connection.DeleteAsync(tax);
            }
        }

        public async Task<List<Tax>> GetSearchableTaxesAsync(string? type = null)
        {
            var query = _databaseService.Connection.Table<Tax>()
                .Where(t => t.IsActive && (t.TaxType == "Sales" || t.TaxType == "Purchases"));

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(t => t.TaxType == type);
            }

            return await query.OrderBy(t => t.Name).ToListAsync();
        }
    }
}
