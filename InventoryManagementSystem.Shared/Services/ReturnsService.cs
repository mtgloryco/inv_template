using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class ReturnsService
    {
        private readonly DatabaseService _databaseService;

        public ReturnsService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task ProcessCustomerReturnAsync(CustomerReturn ret)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                // Log return
                conn.Insert(ret);

                if (ret.Condition == "Resaleable")
                {
                    // Add stock back via StockMovement IN
                    var movement = new StockMovement
                    {
                        ProductId = ret.ProductId,
                        QuantityChanged = ret.Quantity,
                        MovementType = "IN",
                        Reason = "Customer Return - Resaleable",
                        Date = DateTime.Now,
                        Username = ret.ProcessedByUsername
                    };
                    conn.Insert(movement);

                    // Update product total stock
                    var product = conn.Find<Product>(ret.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += ret.Quantity;
                        conn.Update(product);
                    }
                }
                else
                {
                    // Log as waste (no stock added back)
                    // Optionally log separate waste record if there's a waste table
                }
            });
        }

        public async Task ProcessSupplierReturnAsync(SupplierReturn ret)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                // Log return
                conn.Insert(ret);

                // Deduct stock via StockMovement OUT
                var movement = new StockMovement
                {
                    ProductId = ret.ProductId,
                    QuantityChanged = -ret.Quantity,
                    MovementType = "OUT",
                    Reason = "Supplier Return - Overstock/Defective",
                    Date = DateTime.Now,
                };
                conn.Insert(movement);

                // Update product total stock
                var product = conn.Find<Product>(ret.ProductId);
                if (product != null)
                {
                    product.StockQuantity -= ret.Quantity;
                    conn.Update(product);
                }

                // Note: Real batch logic would require knowing which batch is being returned
                // For simplicity as per Phase 5 prompt, we focus on stock deduction
            });
        }

        public async Task<List<CustomerReturn>> GetCustomerReturnsAsync(DateTime from, DateTime to)
        {
            return await _databaseService.Connection.Table<CustomerReturn>()
                .Where(r => r.ReturnDate >= from && r.ReturnDate <= to)
                .ToListAsync();
        }

        public async Task<List<SupplierReturn>> GetSupplierReturnsAsync(DateTime from, DateTime to)
        {
            return await _databaseService.Connection.Table<SupplierReturn>()
                .Where(r => r.ReturnDate >= from && r.ReturnDate <= to)
                .ToListAsync();
        }
    }
}
