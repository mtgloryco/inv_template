using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class SupplierService
    {
        private readonly DatabaseService _databaseService;

        public SupplierService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<Supplier>> GetAllSuppliersAsync()
        {
            return await _databaseService.Connection.Table<Supplier>()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task AddSupplierAsync(Supplier supplier)
        {
            supplier.CreatedAt = DateTime.Now;
            supplier.IsActive = true;
            await _databaseService.Connection.InsertAsync(supplier);
        }

        public async Task UpdateSupplierAsync(Supplier supplier)
        {
            await _databaseService.Connection.UpdateAsync(supplier);
        }

        public async Task DeleteSupplierAsync(int supplierId)
        {
            var supplier = await _databaseService.Connection.FindAsync<Supplier>(supplierId);
            if (supplier == null) return;
            supplier.IsActive = false;
            await _databaseService.Connection.UpdateAsync(supplier);
        }

        public async Task<SupplierPerformance> GetSupplierPerformanceAsync(int supplierId)
        {
            var pos = await _databaseService.Connection.Table<PurchaseOrder>()
                .Where(p => p.SupplierId == supplierId && p.Status == "Received")
                .ToListAsync();

            if (pos.Count == 0)
            {
                return new SupplierPerformance { SupplierId = supplierId };
            }

            var onTime = pos.Count(p => p.ExpectedDeliveryDate.HasValue && p.ActualDeliveryDate.HasValue &&
                                        p.ActualDeliveryDate.Value.Date <= p.ExpectedDeliveryDate.Value.Date);

            var leadTimes = pos.Where(p => p.ActualDeliveryDate.HasValue)
                .Select(p => (p.ActualDeliveryDate!.Value - p.OrderDate).TotalDays)
                .ToList();

            return new SupplierPerformance
            {
                SupplierId = supplierId,
                TotalOrders = pos.Count,
                OnTimeDeliveryPercent = pos.Count == 0 ? 0 : (decimal)onTime * 100m / pos.Count,
                AverageLeadTimeDays = leadTimes.Count == 0 ? 0 : leadTimes.Average()
            };
        }

        public async Task<List<Supplier>> GetTopSuppliersAsync(int limit = 5)
        {
            var suppliers = await GetAllSuppliersAsync();
            var scored = new List<(Supplier Supplier, decimal Score)>();

            foreach (var supplier in suppliers)
            {
                var perf = await GetSupplierPerformanceAsync(supplier.Id);
                var score = (supplier.Rating * 0.6m) + (perf.OnTimeDeliveryPercent * 0.4m / 100m * 5m);
                scored.Add((supplier, score));
            }

            return scored.OrderByDescending(s => s.Score).Take(limit).Select(s => s.Supplier).ToList();
        }

        public async Task<List<Product>> GetSupplierProductsAsync(int supplierId)
        {
            var relations = await _databaseService.Connection.Table<SupplierProduct>()
                .Where(sp => sp.SupplierId == supplierId)
                .ToListAsync();

            var productIds = relations.Select(sp => sp.ProductId).ToList();
            if (!productIds.Any()) return new List<Product>();

            var products = new List<Product>();
            foreach (var id in productIds)
            {
                var prod = await _databaseService.Connection.FindAsync<Product>(id);
                if (prod != null)
                {
                    products.Add(prod);
                }
            }
            return products.OrderBy(p => p.Name).ToList();
        }

        public async Task LinkProductToSupplierAsync(int supplierId, int productId)
        {
            var exists = await _databaseService.Connection.Table<SupplierProduct>()
                .Where(sp => sp.SupplierId == supplierId && sp.ProductId == productId)
                .FirstOrDefaultAsync();

            if (exists == null)
            {
                await _databaseService.Connection.InsertAsync(new SupplierProduct
                {
                    SupplierId = supplierId,
                    ProductId = productId
                });
            }
        }

        public async Task UnlinkProductFromSupplierAsync(int supplierId, int productId)
        {
            var relation = await _databaseService.Connection.Table<SupplierProduct>()
                .Where(sp => sp.SupplierId == supplierId && sp.ProductId == productId)
                .FirstOrDefaultAsync();

            if (relation != null)
            {
                await _databaseService.Connection.DeleteAsync(relation);
            }
        }

        public async Task<Supplier?> GetSupplierByIdAsync(int supplierId)
        {
            return await _databaseService.Connection.FindAsync<Supplier>(supplierId);
        }
    }
}

