using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class AnalyticsService
    {
        private readonly DatabaseService _databaseService;

        public AnalyticsService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        // 1. REORDER RECOMMENDATIONS
        // Logic: Suggest reorder if Stock < 5 or Stock < (AvgDailySales * 7)
        public async Task<List<ProductRecommendation>> GetReorderRecommendationsAsync()
        {
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var recommendations = new List<ProductRecommendation>();

            foreach (var p in products)
            {
                // Simple hygiene check
                if (p.StockQuantity <= 5)
                {
                    recommendations.Add(new ProductRecommendation
                    {
                        Product = p,
                        Reason = "Low Stock (Critical)",
                        Action = "Reorder immediately to avoid stockout."
                    });
                }
            }

            return recommendations;
        }

        // 2. DEAD STOCK ALERTS
        // Logic: Items with Stock > 0 but NO sales in last 90 days
        public async Task<List<ProductRecommendation>> GetDeadStockAsync()
        {
            var cutoffDate = DateTime.Now.AddDays(-90);
            
            // Get all products with stock
            var productsWithStock = await _databaseService.Connection.Table<Product>()
                                        .Where(p => p.StockQuantity > 0)
                                        .ToListAsync();

            var deadStock = new List<ProductRecommendation>();

            foreach (var p in productsWithStock)
            {
                // Check last "OUT" movement
                var lastSale = await _databaseService.Connection.Table<StockMovement>()
                                    .Where(m => m.ProductId == p.Id && m.MovementType == "OUT")
                                    .OrderByDescending(m => m.Date)
                                    .FirstOrDefaultAsync();

                // If never sold, or last sold > 90 days ago
                if (lastSale == null || lastSale.Date < cutoffDate)
                {
                    // Logic: If added recently (e.g., last week), it's not dead stock yet.
                    var creationDate = await GetProductCreationDate(p.Id);
                    if (creationDate < cutoffDate)
                    {
                        deadStock.Add(new ProductRecommendation
                        {
                            Product = p,
                            Reason = "No sales in 90+ days",
                            Action = "Discount this item to free up cash."
                        });
                    }
                }
            }

            return deadStock;
        }

        // 3. LOW MARGIN ALERTS
        // Logic: Margin < 20%
        public async Task<List<ProductRecommendation>> GetLowMarginProductsAsync()
        {
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var lowMarginItems = new List<ProductRecommendation>();

            foreach (var p in products)
            {
                if (p.Price <= 0) continue; // Avoid division by zero

                var margin = (p.Price - p.Cost) / p.Price;
                if (margin < 0.20m) // 20% threshold
                {
                    lowMarginItems.Add(new ProductRecommendation
                    {
                        Product = p,
                        Reason = $"Low Margin ({(margin * 100):N1}%)",
                        Action = "Consider raising price or negotiating cost."
                    });
                }
            }

            return lowMarginItems;
        }

        private async Task<DateTime> GetProductCreationDate(int productId)
        {
            // Heuristic: First ever movement for this product
            var firstMove = await _databaseService.Connection.Table<StockMovement>()
                                .Where(m => m.ProductId == productId)
                                .OrderBy(m => m.Date)
                                .FirstOrDefaultAsync();
            return firstMove?.Date ?? DateTime.Now;
        }
    }

    public class ProductRecommendation
    {
        public Product Product { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }
}
