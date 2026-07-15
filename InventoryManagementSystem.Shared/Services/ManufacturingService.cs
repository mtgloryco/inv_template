using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class ManufacturingService
    {
        private readonly DatabaseService _databaseService;

        public ManufacturingService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<BillOfMaterialListItem>> GetAllBomsAsync()
        {
            var boms = await _databaseService.Connection.Table<BillOfMaterial>().ToListAsync();
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var productDict = products.ToDictionary(p => p.Id, p => p.Name);

            var items = new List<BillOfMaterialListItem>();
            foreach (var bom in boms)
            {
                productDict.TryGetValue(bom.ProductId, out var prodName);
                items.Add(new BillOfMaterialListItem
                {
                    BillOfMaterial = bom,
                    ProductName = prodName ?? "Unknown Product"
                });
            }
            return items;
        }

        public async Task<List<BillOfMaterialLine>> GetBomLinesAsync(int bomId)
        {
            return await _databaseService.Connection.Table<BillOfMaterialLine>()
                .Where(line => line.BillOfMaterialId == bomId)
                .ToListAsync();
        }

        public async Task<BillOfMaterial> SaveBomAsync(BillOfMaterial bom, List<BillOfMaterialLine> lines)
        {
            if (bom.Id == 0)
            {
                await _databaseService.Connection.InsertAsync(bom);
                var lastId = await _databaseService.Connection.ExecuteScalarAsync<long>("SELECT last_insert_rowid()");
                bom.Id = (int)lastId;
            }
            else
            {
                await _databaseService.Connection.UpdateAsync(bom);
                await _databaseService.Connection.ExecuteAsync(
                    "DELETE FROM BillOfMaterialLine WHERE BillOfMaterialId = ?", bom.Id);
            }

            foreach (var line in lines)
            {
                line.BillOfMaterialId = bom.Id;
                line.Id = 0;
            }
            if (lines.Any())
            {
                await _databaseService.Connection.InsertAllAsync(lines);
            }
            return bom;
        }

        public async Task DeleteBomAsync(int bomId)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var bom = conn.Find<BillOfMaterial>(bomId);
                if (bom != null)
                {
                    conn.Delete(bom);
                }

                var lines = conn.Table<BillOfMaterialLine>()
                    .Where(l => l.BillOfMaterialId == bomId)
                    .ToList();
                foreach (var line in lines)
                    conn.Delete(line);
            });
        }

        public async Task<List<ManufacturingOrderListItem>> GetAllManufacturingOrdersAsync()
        {
            var orders = await _databaseService.Connection.Table<ManufacturingOrder>().ToListAsync();
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var productDict = products.ToDictionary(p => p.Id, p => p.Name);

            var items = new List<ManufacturingOrderListItem>();
            foreach (var order in orders)
            {
                productDict.TryGetValue(order.ProductId, out var prodName);

                items.Add(new ManufacturingOrderListItem
                {
                    ManufacturingOrder = order,
                    ProductName = prodName ?? "Unknown Product"
                });
            }
            return items.OrderByDescending(o => o.ManufacturingOrder.OrderDate).ToList();
        }

        public async Task<List<ManufacturingOrderLine>> GetManufacturingOrderLinesAsync(int orderId)
        {
            return await _databaseService.Connection.Table<ManufacturingOrderLine>()
                .Where(line => line.ManufacturingOrderId == orderId)
                .ToListAsync();
        }

        public async Task<ManufacturingOrder> SaveManufacturingOrderAsync(ManufacturingOrder order, List<ManufacturingOrderLine> lines)
        {
            if (order.Id == 0)
            {
                await _databaseService.Connection.InsertAsync(order);
                var lastId = await _databaseService.Connection.ExecuteScalarAsync<long>("SELECT last_insert_rowid()");
                order.Id = (int)lastId;
            }
            else
            {
                await _databaseService.Connection.UpdateAsync(order);
                await _databaseService.Connection.ExecuteAsync(
                    "DELETE FROM ManufacturingOrderLine WHERE ManufacturingOrderId = ?", order.Id);
            }

            foreach (var line in lines)
            {
                line.ManufacturingOrderId = order.Id;
                line.Id = 0;
            }
            if (lines.Any())
            {
                await _databaseService.Connection.InsertAllAsync(lines);
            }
            return order;
        }

        public async Task ConfirmManufacturingOrderAsync(int orderId)
        {
            var order = await _databaseService.Connection.FindAsync<ManufacturingOrder>(orderId);
            if (order != null && order.Status == "Draft")
            {
                order.Status = "Confirmed";
                await _databaseService.Connection.UpdateAsync(order);
            }
        }

        public static double CalculateComponentQuantity(
            double bomOutputQty,
            double bomLineQty,
            double moTargetQty,
            double bomYieldPercent,
            double lineScrapPercent,
            double bomScrapPercent = 0)
        {
            if (bomOutputQty <= 0 || moTargetQty <= 0) return 0;

            var yieldFactor = bomYieldPercent <= 0 ? 1.0 : bomYieldPercent / 100.0;
            var scrapFactor = 1.0 + ((lineScrapPercent + bomScrapPercent) / 100.0);
            var scaledTarget = moTargetQty / yieldFactor;
            return bomLineQty * (scaledTarget / bomOutputQty) * scrapFactor;
        }

        public async Task<List<ManufacturingOrderLine>> BuildExpectedLinesFromBomAsync(int bomId, double targetQuantity)
        {
            var bom = await _databaseService.Connection.FindAsync<BillOfMaterial>(bomId);
            if (bom == null) return new List<ManufacturingOrderLine>();

            var lines = await GetBomLinesAsync(bomId);
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var result = new List<ManufacturingOrderLine>();

            foreach (var line in lines)
            {
                var product = products.FirstOrDefault(p => p.Id == line.ProductId);
                var expectedQty = CalculateComponentQuantity(
                    bom.Quantity, line.Quantity, targetQuantity, bom.YieldPercent, line.ScrapPercent, bom.ScrapPercent);

                result.Add(new ManufacturingOrderLine
                {
                    ProductId = line.ProductId,
                    ExpectedQuantity = expectedQty,
                    ActualQuantity = expectedQty,
                    Unit = line.Unit,
                    UnitCost = product?.Cost ?? 0m
                });
            }

            return result;
        }

        public async Task ProduceManufacturingOrderAsync(int orderId, double actualQty, List<ManufacturingOrderLine> actualLines, string username)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var order = conn.Find<ManufacturingOrder>(orderId);
                if (order == null) throw new Exception("Manufacturing order not found.");
                if (order.Status != "Confirmed") throw new Exception("Only confirmed manufacturing orders can be processed.");

                var bom = conn.Find<BillOfMaterial>(order.BomId);
                var finishedProduct = conn.Find<Product>(order.ProductId);
                if (finishedProduct == null) throw new Exception($"Finished Product ID {order.ProductId} not found.");

                var units = conn.Table<ProductUnit>().ToList();
                decimal totalCostSum = 0m;
                var yieldFactor = bom == null || bom.YieldPercent <= 0 ? 1.0 : bom.YieldPercent / 100.0;
                var effectiveOutputQty = actualQty * yieldFactor;

                // 1. Deduct component quantities from stock and insert OUT stock movements
                foreach (var line in actualLines)
                {
                    var componentProduct = conn.Find<Product>(line.ProductId);
                    if (componentProduct == null) throw new Exception($"Component Product ID {line.ProductId} not found.");

                    // Record unit cost of ingredient at production time
                    line.UnitCost = componentProduct.Cost;
                    line.ActualQuantity = line.ActualQuantity;
                    line.ManufacturingOrderId = order.Id;

                    // Convert quantities if units differ
                    double convertedQty = UnitConverter.Convert(line.ActualQuantity, line.Unit, componentProduct.Unit, units);
                    var componentPreviousStock = componentProduct.StockQuantity;
                    componentProduct.StockQuantity -= (int)Math.Round(convertedQty);
                    conn.Update(componentProduct);
                    LocationStockSync.ApplyDelta(conn, componentProduct.Id, componentProduct.StockQuantity - componentPreviousStock);

                    conn.Insert(new StockMovement
                    {
                        ProductId = componentProduct.Id,
                        QuantityChanged = -(int)Math.Round(convertedQty),
                        MovementType = "OUT",
                        Reason = $"Manufacturing Order Consumption - {order.MONumber}",
                        Date = DateTime.Now,
                        Username = username,
                        UnitPrice = componentProduct.Cost
                    });

                    // Add to total manufacturing cost sum
                    totalCostSum += componentProduct.Cost * (decimal)convertedQty;

                    if (line.Id == 0)
                    {
                        conn.Insert(line);
                    }
                    else
                    {
                        conn.Update(line);
                    }
                }

                // 2. Add finished product quantity to stock and insert IN stock movement
                var finishedPreviousStock = finishedProduct.StockQuantity;
                finishedProduct.StockQuantity += (int)Math.Round(effectiveOutputQty);
                conn.Update(finishedProduct);
                LocationStockSync.ApplyDelta(conn, finishedProduct.Id, finishedProduct.StockQuantity - finishedPreviousStock);

                decimal unitCostOfFinishedProduct = effectiveOutputQty > 0 ? totalCostSum / (decimal)effectiveOutputQty : 0m;
                
                // Update product unit cost/selling price optionally, but standard is updating inventory asset cost
                finishedProduct.Cost = unitCostOfFinishedProduct;
                conn.Update(finishedProduct);

                conn.Insert(new StockMovement
                {
                    ProductId = finishedProduct.Id,
                    QuantityChanged = (int)Math.Round(effectiveOutputQty),
                    MovementType = "IN",
                    Reason = $"Manufacturing Production - {order.MONumber}",
                    Date = DateTime.Now,
                    Username = username,
                    UnitPrice = unitCostOfFinishedProduct
                });

                // Update order state
                order.ActualQuantity = effectiveOutputQty;
                order.Status = "Done";
                order.ProduceDate = DateTime.Now;
                order.TotalCost = totalCostSum;
                conn.Update(order);
            });
        }

        public async Task DeleteManufacturingOrderAsync(int orderId)
        {
            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                var order = conn.Find<ManufacturingOrder>(orderId);
                if (order != null)
                {
                    conn.Delete(order);
                }

                var lines = conn.Table<ManufacturingOrderLine>()
                    .Where(l => l.ManufacturingOrderId == orderId)
                    .ToList();
                foreach (var line in lines)
                    conn.Delete(line);
            });
        }

        public async Task<ProductionReportSummary> GetProductionReportAsync()
        {
            var orders = await GetAllManufacturingOrdersAsync();
            var completed = orders.Where(o => o.ManufacturingOrder.Status == "Done").ToList();
            var totalCost = completed.Sum(o => o.ManufacturingOrder.TotalCost);

            return new ProductionReportSummary
            {
                TotalMOs = orders.Count,
                CompletedMOs = completed.Count,
                TotalProductionCost = totalCost,
                CompletedOrders = completed
            };
        }
    }

    public class ProductionReportSummary
    {
        public int TotalMOs { get; set; }
        public int CompletedMOs { get; set; }
        public decimal TotalProductionCost { get; set; }
        public List<ManufacturingOrderListItem> CompletedOrders { get; set; } = new();
    }
}
