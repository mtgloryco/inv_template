using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class AdvancedAnalyticsService
    {
        private readonly DatabaseService _databaseService;

        public AdvancedAnalyticsService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<decimal> GetStockTurnoverRatioAsync(int productId, DateTime from, DateTime to)
        {
            // Ratio = COGS / Average Inventory Value
            var movements = await _databaseService.Connection.Table<StockMovement>()
                .Where(m => m.ProductId == productId && m.MovementType == "OUT" && m.Date >= from && m.Date <= to)
                .ToListAsync();

            var totalCogs = 0m;
            foreach (var m in movements)
            {
                var usages = await _databaseService.Connection.Table<SaleBatchUsage>()
                    .Where(u => u.StockMovementId == m.Id)
                    .ToListAsync();
                totalCogs += usages.Sum(u => u.QuantityUsed * u.CostPerUnit);
            }

            // Simple avg stock estimation: (beginning + ending)/2
            // For now, use current value as estimation or if history exists, use that.
            var product = await _databaseService.Connection.FindWithQueryAsync<Product>("SELECT * FROM Product WHERE Id = ?", productId);
            var avgInventoryValue = Math.Max(product.StockQuantity * product.Cost, 1m);

            return totalCogs / avgInventoryValue;
        }

        public async Task<decimal> GetCarryingCostAnalysisAsync()
        {
            // estimate cost of holding inventory (typically 20-30% of inventory value per year)
            var batches = await _databaseService.Connection.Table<PurchaseBatch>()
                                .Where(b => b.QuantityRemaining > 0)
                                .ToListAsync();

            decimal totalInventoryValue = 0;
            foreach (var b in batches) totalInventoryValue += b.QuantityRemaining * b.CostPerUnit;

            return totalInventoryValue * 0.25m; // 25% avg
        }

        public async Task<List<ProfitabilityItem>> GetProfitabilityRankingAsync()
        {
            // rank all products by total profit generated, margin %, and revenue contribution
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var result = new List<ProfitabilityItem>();

            foreach (var p in products)
            {
                var movements = await _databaseService.Connection.Table<StockMovement>()
                    .Where(m => m.ProductId == p.Id && m.MovementType == "OUT")
                    .ToListAsync();

                var revenue = movements.Sum(m => m.QuantityChanged * m.UnitPrice);
                var totalCogs = 0m;
                foreach (var m in movements)
                {
                    var usages = await _databaseService.Connection.Table<SaleBatchUsage>()
                        .Where(u => u.StockMovementId == m.Id)
                        .ToListAsync();
                    totalCogs += usages.Sum(u => u.QuantityUsed * u.CostPerUnit);
                }

                var profit = revenue - totalCogs;
                var margin = revenue == 0 ? 0 : (profit / revenue);

                result.Add(new ProfitabilityItem
                {
                    SKU = p.SKU ?? string.Empty,
                    ProductName = p.Name,
                    TotalProfit = profit,
                    MarginPercent = margin,
                    TotalRevenue = revenue
                });
            }

            return result.OrderByDescending(x => x.TotalProfit).ToList();
        }

        public async Task<List<MonthlyTrend>> GetSalesTrendAsync(int productId, int months = 12)
        {
            // month-by-month sales data for charting
            var result = new List<MonthlyTrend>();
            var now = DateTime.Now;

            for (int i = months - 1; i >= 0; i--)
            {
                var monthDate = now.AddMonths(-i);
                var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1);

                var movements = await _databaseService.Connection.Table<StockMovement>()
                    .Where(m => m.ProductId == productId && m.MovementType == "OUT" && m.Date >= monthStart && m.Date < monthEnd)
                    .ToListAsync();

                var units = movements.Sum(m => m.QuantityChanged);
                var revenue = movements.Sum(m => m.QuantityChanged * m.UnitPrice);

                result.Add(new MonthlyTrend
                {
                    MonthLabel = monthStart.ToString("MMM yyyy"),
                    UnitsSold = units,
                    Revenue = revenue
                });
            }

            return result;
        }

        public async Task<List<ProfitabilityItem>> GetParetoAnalysisAsync()
        {
            // which products make up 80% of revenue (Pareto/80-20 rule)
            var ranking = await GetProfitabilityRankingAsync();
            var totalRevenue = ranking.Sum(r => r.TotalRevenue);

            if (totalRevenue <= 0) return ranking;

            decimal cumulativeRev = 0;
            var result = new List<ProfitabilityItem>();

            foreach (var item in ranking)
            {
                cumulativeRev += item.TotalRevenue;
                item.CumulativePercentage = totalRevenue > 0 ? cumulativeRev / totalRevenue : 0;
                result.Add(item);
                if (item.CumulativePercentage >= 0.80m) break;
            }

            return result;
        }

        public async Task<int> GetInventoryHealthScoreAsync()
        {
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            if (products.Count == 0) return 100;

            var deadStock = await GetDeadStockCountAsync();
            var deadPct = (decimal)deadStock / products.Count;

            var lowMargin = await GetLowMarginCountAsync();
            var lowMarginPct = (decimal)lowMargin / products.Count;

            var stockoutRisk = products.Count(p => p.StockQuantity <= 0);
            var stockoutPct = (decimal)stockoutRisk / products.Count;

            var score = 100m;
            score -= deadPct * 40m;
            score -= lowMarginPct * 30m;
            score -= stockoutPct * 30m;
            return (int)Math.Clamp(Math.Round(score), 0, 100);
        }

        public async Task<List<CategoryMarginLine>> GetMarginByCategoryAsync(DateTime? from = null, DateTime? to = null)
        {
            var end = to ?? DateTime.Now;
            var start = from ?? end.AddDays(-90);
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var categories = products
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Category) ? "Uncategorized" : p.Category)
                .ToList();

            var result = new List<CategoryMarginLine>();
            foreach (var group in categories)
            {
                decimal revenue = 0;
                decimal cost = 0;
                foreach (var product in group)
                {
                    var sales = await _databaseService.Connection.Table<StockMovement>()
                        .Where(m => m.ProductId == product.Id && m.MovementType == "OUT" && m.Date >= start && m.Date <= end)
                        .ToListAsync();

                    revenue += sales.Sum(s => s.QuantityChanged * s.UnitPrice);
                    cost += sales.Sum(s => s.QuantityChanged) * product.Cost;
                }

                var profit = revenue - cost;
                result.Add(new CategoryMarginLine
                {
                    CategoryName = group.Key,
                    Revenue = revenue,
                    Cost = cost,
                    GrossProfit = profit,
                    MarginPercent = revenue == 0 ? 0 : profit / revenue,
                    ProductCount = group.Count()
                });
            }

            return result.OrderByDescending(c => c.GrossProfit).ToList();
        }

        public async Task<List<AbcAnalysisLine>> GetAbcAnalysisAsync()
        {
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var revenues = new List<(Product Product, decimal Revenue)>();

            foreach (var product in products)
            {
                var revenue = await _databaseService.Connection.Table<StockMovement>()
                    .Where(m => m.ProductId == product.Id && m.MovementType == "OUT")
                    .ToListAsync();
                revenues.Add((product, revenue.Sum(m => m.QuantityChanged * m.UnitPrice)));
            }

            var total = revenues.Sum(r => r.Revenue);
            if (total <= 0)
            {
                return products.Select(p => new AbcAnalysisLine
                {
                    SKU = p.SKU ?? string.Empty,
                    ProductName = p.Name,
                    Classification = "C",
                    Revenue = 0,
                    RevenueSharePercent = 0
                }).ToList();
            }

            var ordered = revenues.OrderByDescending(r => r.Revenue).ToList();
            var result = new List<AbcAnalysisLine>();
            decimal cumulative = 0;

            foreach (var (product, revenue) in ordered)
            {
                var cumulativeBefore = cumulative;
                cumulative += revenue;
                var share = revenue / total * 100m;
                var threshold80 = total * 0.80m;
                var threshold95 = total * 0.95m;
                var classification = cumulativeBefore < threshold80 ? "A"
                    : cumulativeBefore < threshold95 ? "B"
                    : "C";

                result.Add(new AbcAnalysisLine
                {
                    SKU = product.SKU ?? string.Empty,
                    ProductName = product.Name,
                    Classification = classification,
                    Revenue = revenue,
                    RevenueSharePercent = share
                });
            }

            return result;
        }

        public async Task<List<ProductRecommendation>> GetDeadStockReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var analytics = new AnalyticsService(_databaseService);
            return await analytics.GetDeadStockAsync(startDate, endDate);
        }

        private async Task<int> GetDeadStockCountAsync()
        {
            var dead = await GetDeadStockReportAsync();
            return dead.Count;
        }

        private async Task<int> GetLowMarginCountAsync()
        {
            var analytics = new AnalyticsService(_databaseService);
            var low = await analytics.GetLowMarginProductsAsync();
            return low.Count;
        }
    }

    public class ProfitabilityItem
    {
        public string SKU { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalProfit { get; set; }
        public decimal MarginPercent { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal CumulativePercentage { get; set; }
    }

    public class MonthlyTrend
    {
        public string MonthLabel { get; set; } = string.Empty;
        public int UnitsSold { get; set; }
        public decimal Revenue { get; set; }
    }
}
