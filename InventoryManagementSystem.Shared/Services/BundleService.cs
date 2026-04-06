using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class BundleService
    {
        private readonly DatabaseService _databaseService;

        public BundleService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<ProductBundle>> GetBundleComponentsAsync(int bundleProductId)
        {
            return await _databaseService.Connection.Table<ProductBundle>()
                .Where(pb => pb.ParentProductId == bundleProductId)
                .ToListAsync();
        }

        public async Task<int> GetAvailableBundleQuantityAsync(int bundleProductId)
        {
            var components = await GetBundleComponentsAsync(bundleProductId);
            if (components.Count == 0) return 0;

            int minPossible = int.MaxValue;
            foreach (var comp in components)
            {
                var product = await _databaseService.Connection.Table<Product>()
                    .Where(p => p.Id == comp.ComponentProductId)
                    .FirstOrDefaultAsync();

                if (product == null) continue;

                int possible = product.StockQuantity / comp.QuantityRequired;
                if (possible < minPossible) minPossible = possible;
            }

            return minPossible == int.MaxValue ? 0 : minPossible;
        }

        public async Task AssembleBundleAsync(int bundleProductId, int quantity, string username)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var components = conn.Table<ProductBundle>()
                    .Where(pb => pb.ParentProductId == bundleProductId)
                    .ToList();

                foreach (var comp in components)
                {
                    var product = conn.Find<Product>(comp.ComponentProductId);
                    int totalNeeded = comp.QuantityRequired * quantity;

                    if (product.StockQuantity < totalNeeded)
                        throw new InvalidOperationException($"Insufficient component stock: {product.Name}");

                    product.StockQuantity -= totalNeeded;
                    conn.Update(product);

                    // Log movement
                    conn.Insert(new StockMovement
                    {
                        ProductId = product.Id,
                        QuantityChanged = -totalNeeded,
                        MovementType = "ADJUST",
                        Reason = $"Bundle Assembly: {bundleProductId}",
                        Date = DateTime.Now,
                        Username = username
                    });
                }

                var bundleProduct = conn.Find<Product>(bundleProductId);
                bundleProduct.StockQuantity += quantity;
                conn.Update(bundleProduct);

                conn.Insert(new StockMovement
                {
                    ProductId = bundleProductId,
                    QuantityChanged = quantity,
                    MovementType = "ADJUST",
                    Reason = "Bundle Assembly Result",
                    Date = DateTime.Now,
                    Username = username
                });
            });
        }

        public async Task DisassembleBundleAsync(int bundleProductId, int quantity, string username)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var bundleProduct = conn.Find<Product>(bundleProductId);
                if (bundleProduct.StockQuantity < quantity)
                    throw new InvalidOperationException("Insufficient bundle stock to disassemble.");

                bundleProduct.StockQuantity -= quantity;
                conn.Update(bundleProduct);

                conn.Insert(new StockMovement
                {
                    ProductId = bundleProductId,
                    QuantityChanged = -quantity,
                    MovementType = "ADJUST",
                    Reason = "Bundle Disassembly",
                    Date = DateTime.Now,
                    Username = username
                });

                var components = conn.Table<ProductBundle>()
                    .Where(pb => pb.ParentProductId == bundleProductId)
                    .ToList();

                foreach (var comp in components)
                {
                    var product = conn.Find<Product>(comp.ComponentProductId);
                    int totalReturn = comp.QuantityRequired * quantity;

                    product.StockQuantity += totalReturn;
                    conn.Update(product);

                    conn.Insert(new StockMovement
                    {
                        ProductId = product.Id,
                        QuantityChanged = totalReturn,
                        MovementType = "ADJUST",
                        Reason = $"Disassembly of Bundle: {bundleProductId}",
                        Date = DateTime.Now,
                        Username = username
                    });
                }
            });
        }
    }
}
