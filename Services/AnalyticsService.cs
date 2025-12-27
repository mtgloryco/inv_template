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

        public async Task<List<ProductRecommendation>> GetDeadStockAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? end.AddDays(-90);
            
            // Get all products with stock
            var productsWithStock = await _databaseService.Connection.Table<Product>()
                                        .Where(p => p.StockQuantity > 0)
                                        .ToListAsync();

            var deadStock = new List<ProductRecommendation>();

            foreach (var p in productsWithStock)
            {
                // Check if there were ANY sales in the window
                var saleInWindow = await _databaseService.Connection.Table<StockMovement>()
                                    .Where(m => m.ProductId == p.Id && m.MovementType == "OUT" && m.Date >= start && m.Date <= end)
                                    .CountAsync();

                if (saleInWindow == 0)
                {
                    // Logic: If added AFTER the start date, it's not "dead" yet, it's just new.
                    // We only flag items that existed BEFORE the window but had 0 sales WITHIN the window.
                    var creationDate = await GetProductCreationDate(p.Id);
                    if (creationDate < start)
                    {
                        deadStock.Add(new ProductRecommendation
                        {
                            Product = p,
                            Reason = $"No sales between {start:MM/dd} and {end:MM/dd}",
                            Action = "Discount this item to free up cash."
                        });
                    }
                }
            }

            return deadStock;
        }

        // 3. LOW MARGIN ALERTS
        // Logic: Calculate Realized Margin from actual sales in the period
        public async Task<List<ProductRecommendation>> GetLowMarginProductsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var end = endDate ?? DateTime.Now;
            var start = startDate ?? end.AddDays(-30);

            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var lowMarginItems = new List<ProductRecommendation>();

            foreach (var p in products)
            {
                // 1. Analyze Actual Sales in Period
                var sales = await _databaseService.Connection.Table<StockMovement>()
                                .Where(m => m.ProductId == p.Id && m.MovementType == "OUT" && m.Date >= start && m.Date <= end)
                                .ToListAsync();

                decimal avgMargin = 0;

                if (sales.Any())
                {
                    // Calculate weighted average realized margin
                    decimal totalRevenue = sales.Sum(s => s.QuantityChanged * s.UnitPrice);
                    
                    // COGS Approximation (since we don't have easy batch linking here without complex query, use current Cost or batch trace if available)
                    // For speed in this loop, we'll use the Product.Cost (Current Replacement Cost) as the baseline for "Strategy"
                    // Or ideally, we look at BatchUsage. Let's stick to Product.Cost for the Recommendation engine to keep it fast.
                    decimal totalEstCost = sales.Sum(s => s.QuantityChanged) * p.Cost;

                    if (totalRevenue > 0)
                    {
                        avgMargin = (totalRevenue - totalEstCost) / totalRevenue;
                    }
                }
                else
                {
                     // Fallback to theoretical margin
                     if (p.Price > 0) avgMargin = (p.Price - p.Cost) / p.Price;
                }

                if (avgMargin < 0.20m) // 20% threshold
                {
                    lowMarginItems.Add(new ProductRecommendation
                    {
                        Product = p,
                        Reason = $"Low Margin ({(avgMargin * 100):N1}%) in selected period",
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
