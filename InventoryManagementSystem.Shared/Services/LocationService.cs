using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class LocationService
    {
        private readonly DatabaseService _databaseService;

        public LocationService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<Location>> GetAllLocationsAsync()
        {
            return await _databaseService.Connection.Table<Location>().Where(l => l.IsActive).ToListAsync();
        }

        public async Task AddLocationAsync(Location location)
        {
            await _databaseService.Connection.InsertAsync(location);
        }

        public async Task<List<LocationStock>> GetStockByLocationAsync(int locationId)
        {
            return await _databaseService.Connection.Table<LocationStock>()
                .Where(ls => ls.LocationId == locationId)
                .ToListAsync();
        }

        public async Task<List<LocationStock>> GetProductLocationsAsync(int productId)
        {
            return await _databaseService.Connection.Table<LocationStock>()
                .Where(ls => ls.ProductId == productId)
                .ToListAsync();
        }

        public async Task TransferStockAsync(StockTransfer transfer)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                // 1. Deduct from source
                var sourceStock = conn.Table<LocationStock>()
                    .Where(ls => ls.LocationId == transfer.FromLocationId && ls.ProductId == transfer.ProductId)
                    .FirstOrDefault();

                if (sourceStock == null || sourceStock.Quantity < transfer.Quantity)
                {
                    throw new InvalidOperationException("Insufficient stock at source location.");
                }

                sourceStock.Quantity -= transfer.Quantity;
                conn.Update(sourceStock);

                // 2. Add to destination
                var destStock = conn.Table<LocationStock>()
                    .Where(ls => ls.LocationId == transfer.ToLocationId && ls.ProductId == transfer.ProductId)
                    .FirstOrDefault();

                if (destStock == null)
                {
                    destStock = new LocationStock
                    {
                        LocationId = transfer.ToLocationId,
                        ProductId = transfer.ProductId,
                        Quantity = transfer.Quantity
                    };
                    conn.Insert(destStock);
                }
                else
                {
                    destStock.Quantity += transfer.Quantity;
                    conn.Update(destStock);
                }

                // 3. Create transfer record
                transfer.Status = "Completed";
                transfer.CompletedDate = DateTime.Now;
                conn.Insert(transfer);

                // 4. Create stock movements for audit
                var moveOut = new StockMovement
                {
                    ProductId = transfer.ProductId,
                    QuantityChanged = -transfer.Quantity,
                    MovementType = "ADJUST",
                    Reason = $"Transfer to Location {transfer.ToLocationId}",
                    Date = DateTime.Now,
                    Username = transfer.RequestedByUsername
                };
                conn.Insert(moveOut);

                var moveIn = new StockMovement
                {
                    ProductId = transfer.ProductId,
                    QuantityChanged = transfer.Quantity,
                    MovementType = "ADJUST",
                    Reason = $"Transfer from Location {transfer.FromLocationId}",
                    Date = DateTime.Now,
                    Username = transfer.RequestedByUsername
                };
                conn.Insert(moveIn);
            });
        }

        public async Task<int> GetTotalStockAcrossLocationsAsync(int productId)
        {
            var stocks = await GetProductLocationsAsync(productId);
            return stocks.Sum(s => s.Quantity);
        }

        public async Task<List<LocationStock>> GetLowStockByLocationAsync(int locationId)
        {
            return await _databaseService.Connection.Table<LocationStock>()
                .Where(ls => ls.LocationId == locationId && ls.Quantity <= ls.ReorderPoint)
                .ToListAsync();
        }
    }
}
