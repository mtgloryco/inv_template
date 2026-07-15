using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class ForecastingService
    {
        private readonly DatabaseService _databaseService;

        public ForecastingService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        // DTOs used by the UI layer
        public class ForecastingRow
        {
            public Product Product { get; set; } = new();
            public ReorderRule? Rule { get; set; }
            public decimal DailyVelocity { get; set; }
            public int DaysUntilStockout { get; set; }
            public int RecommendedOrderQuantity { get; set; }
            public string ABCClassification { get; set; } = "C";
            public string XYZClassification { get; set; } = "Z";
            public string StockoutRisk { get; set; } = "Healthy"; // Critical/Warning/Healthy
            public string RiskColorHex { get; set; } = "#10B981"; // Green by default
        }

        public class ReorderRecommendation
        {
            public Product Product { get; set; } = new();
            public ReorderRule Rule { get; set; } = new();
            public int LocationId { get; set; }
            public string LocationName { get; set; } = string.Empty;
            public int LocationStockQuantity { get; set; }
            public decimal DailyVelocity { get; set; }
            public int DaysUntilStockout { get; set; }
            public int ThresholdDays { get; set; }
            public int RecommendedOrderQuantity { get; set; }
            public string SupplierName { get; set; } = string.Empty;
        }

        public async Task<decimal> GetSalesVelocityAsync(int productId, int days = 30)
        {
            if (days <= 0) return 0;

            var fromDate = DateTime.Now.Date.AddDays(-(days - 1));
            var toDate = DateTime.Now.Date;

            var soldMovements = await _databaseService.Connection.Table<StockMovement>()
                .Where(m => m.ProductId == productId &&
                            m.MovementType == "OUT" &&
                            m.Date >= fromDate &&
                            m.Date <= toDate)
                .ToListAsync();

            var soldUnits = soldMovements.Sum(m => m.QuantityChanged);

            // Average units/day
            return soldUnits <= 0 ? 0 : (decimal)soldUnits / days;
        }

        public async Task<int> GetDaysUntilStockoutAsync(int productId, int? locationId = null)
        {
            var stockQty = await GetLocationStockQuantityAsync(productId, locationId);
            if (stockQty <= 0) return 0;

            var dailyVelocity = await GetSalesVelocityAsync(productId, 30);
            if (dailyVelocity <= 0) return int.MaxValue;

            var daysDecimal = stockQty / dailyVelocity;
            if (daysDecimal < 0) return 0;
            return (int)Math.Floor(daysDecimal);
        }

        public async Task<int> GetLocationStockQuantityAsync(int productId, int? locationId = null)
        {
            if (locationId.HasValue && locationId.Value > 0)
            {
                var locStock = await _databaseService.Connection.Table<LocationStock>()
                    .FirstOrDefaultAsync(ls => ls.LocationId == locationId.Value && ls.ProductId == productId && !ls.IsDeleted);
                return locStock?.Quantity ?? 0;
            }

            var product = await _databaseService.Connection.Table<Product>()
                .Where(p => p.Id == productId)
                .FirstOrDefaultAsync();
            return product?.StockQuantity ?? 0;
        }

        public async Task<List<ReorderRecommendation>> GetReorderRecommendationsAsync()
        {
            var rules = await _databaseService.Connection.Table<ReorderRule>().ToListAsync();
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var suppliers = await _databaseService.Connection.Table<Supplier>().ToListAsync();
            var locations = await _databaseService.Connection.Table<Location>().Where(l => l.IsActive).ToListAsync();
            var locationStocks = await _databaseService.Connection.Table<LocationStock>().Where(ls => !ls.IsDeleted).ToListAsync();
            var defaultLocation = locations.OrderBy(l => l.Id).FirstOrDefault();

            var recs = new List<ReorderRecommendation>();

            foreach (var rule in rules)
            {
                var product = products.FirstOrDefault(p => p.Id == rule.ProductId);
                if (product == null) continue;

                var locationId = rule.LocationId > 0 ? rule.LocationId : defaultLocation?.Id ?? 0;
                var location = locations.FirstOrDefault(l => l.Id == locationId);
                var locStock = locationStocks.FirstOrDefault(ls => ls.LocationId == locationId && ls.ProductId == product.Id);
                var stockQty = locStock?.Quantity ?? (locationId == 0 ? product.StockQuantity : 0);
                var reorderPoint = rule.ReorderPoint > 0 ? rule.ReorderPoint : locStock?.ReorderPoint ?? 0;

                if (stockQty > reorderPoint && reorderPoint > 0) continue;

                var daysUntilStockout = await GetDaysUntilStockoutAsync(product.Id, locationId > 0 ? locationId : null);
                var thresholdDays = rule.LeadTimeDays + rule.SafetyStockDays;

                if (stockQty <= reorderPoint || daysUntilStockout <= thresholdDays)
                {
                    var supplierName = suppliers.FirstOrDefault(s => s.Id == rule.PreferredSupplierId)?.Name ?? "Unknown";
                    recs.Add(new ReorderRecommendation
                    {
                        Product = product,
                        Rule = rule,
                        LocationId = locationId,
                        LocationName = location?.Name ?? "All Locations",
                        LocationStockQuantity = stockQty,
                        DailyVelocity = await GetSalesVelocityAsync(product.Id, 30),
                        DaysUntilStockout = daysUntilStockout,
                        ThresholdDays = thresholdDays,
                        RecommendedOrderQuantity = rule.ReorderQuantity,
                        SupplierName = supplierName
                    });
                }
            }

            recs.Sort((a, b) => a.DaysUntilStockout.CompareTo(b.DaysUntilStockout));
            return recs;
        }

        public async Task<decimal> CalculateEOQAsync(int productId)
        {
            // EOQ = sqrt((2 * AnnualDemand * OrderCost) / HoldingCost)
            var dailyVelocity = await GetSalesVelocityAsync(productId, 30);
            var annualDemand = dailyVelocity * 365m;

            var product = await _databaseService.Connection.Table<Product>()
                .Where(p => p.Id == productId)
                .FirstOrDefaultAsync();

            if (product == null) return 0;
            if (annualDemand <= 0) return 0;

            // No explicit costing fields exist yet, so we use pragmatic defaults.
            // These can be tuned later (settings / supplier / rule level).
            decimal orderCost = 100m;                 // cost per order (admin + handling)
            decimal holdingCost = Math.Max(product.Cost * 0.25m, 0.01m); // 25% of cost per year

            var numerator = (2m * annualDemand * orderCost) / holdingCost;
            var eoq = Math.Sqrt((double)numerator);
            return (decimal)Math.Floor(eoq);
        }

        public async Task<decimal> GetSeasonalTrendAsync(int productId)
        {
            var now = DateTime.Now;
            var currentMonth = new DateTime(now.Year, now.Month, 1);
            var lastYearSameMonth = new DateTime(now.Year - 1, now.Month, 1);

            var currentNext = currentMonth.AddMonths(1);
            var lastNext = lastYearSameMonth.AddMonths(1);

            var currentSold = await _databaseService.Connection.Table<StockMovement>()
                .Where(m => m.ProductId == productId && m.MovementType == "OUT" && m.Date >= currentMonth && m.Date < currentNext)
                .ToListAsync();

            var lastYearSold = await _databaseService.Connection.Table<StockMovement>()
                .Where(m => m.ProductId == productId && m.MovementType == "OUT" && m.Date >= lastYearSameMonth && m.Date < lastNext)
                .ToListAsync();

            var currentSoldUnits = currentSold.Sum(m => m.QuantityChanged);
            var lastYearSoldUnits = lastYearSold.Sum(m => m.QuantityChanged);

            if (lastYearSoldUnits <= 0) return 1m;
            if (currentSoldUnits <= 0) return 0.5m;

            return (decimal)currentSoldUnits / lastYearSoldUnits;
        }

        public async Task<decimal> GetDemandForecastAsync(int productId, int futureDays)
        {
            if (futureDays <= 0) return 0;

            // Simple linear regression over daily sales for the last 90 days.
            const int historyDays = 90;
            var series = await GetDailySalesSeriesAsync(productId, historyDays);

            if (series.Count == 0) return 0;

            // x = 0..n-1, y = daily units
            var n = series.Count;
            var xs = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
            var ys = series.Select(s => (double)s.Units).ToArray();

            var xMean = xs.Average();
            var yMean = ys.Average();

            double num = 0;
            double den = 0;
            for (var i = 0; i < n; i++)
            {
                var dx = xs[i] - xMean;
                num += dx * (ys[i] - yMean);
                den += dx * dx;
            }

            var slope = den <= 0 ? 0 : num / den;
            var intercept = yMean - slope * xMean;

            var predictedTotal = 0m;
            for (var step = 1; step <= futureDays; step++)
            {
                var x = n - 1 + step;
                var y = intercept + slope * x;
                var units = Math.Max(0, y);
                predictedTotal += (decimal)units;
            }

            return predictedTotal;
        }

        public async Task<Dictionary<int, string>> GetABCClassificationAsync()
        {
            // A: top 80% revenue, B: next 15%, C: bottom 5%
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var revenues = new Dictionary<int, decimal>();

            foreach (var p in products)
            {
                var revenue = await _databaseService.Connection.Table<StockMovement>()
                    .Where(m => m.ProductId == p.Id && m.MovementType == "OUT")
                    .ToListAsync();

                var revenueTotal = revenue.Sum(m => m.QuantityChanged * m.UnitPrice);

                revenues[p.Id] = revenueTotal;
            }

            var total = revenues.Values.Sum();
            if (total <= 0)
            {
                return products.ToDictionary(p => p.Id, _ => "C");
            }

            var ordered = revenues.OrderByDescending(kv => kv.Value).ToList();
            var result = new Dictionary<int, string>();
            decimal acc = 0;

            foreach (var (productId, rev) in ordered)
            {
                var pct = rev / total;
                acc += pct;
                if (acc <= 0.80m) result[productId] = "A";
                else if (acc <= 0.95m) result[productId] = "B";
                else result[productId] = "C";
            }

            return result;
        }

        public async Task<Dictionary<int, string>> GetXYZClassificationAsync()
        {
            // X: stable, Y: variable, Z: erratic (based on coefficient of variation over last 90 days)
            const int historyDays = 90;
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();

            var result = new Dictionary<int, string>();

            foreach (var p in products)
            {
                var series = await GetDailySalesSeriesAsync(p.Id, historyDays);
                var mean = series.Count == 0 ? 0 : series.Average(s => (double)s.Units);
                if (mean <= 0.0001)
                {
                    result[p.Id] = "Z";
                    continue;
                }

                var variance = series.Count == 0
                    ? 0
                    : series.Average(s => Math.Pow(s.Units - mean, 2));

                var stdDev = Math.Sqrt(variance);
                var cv = stdDev / mean;

                if (cv < 0.25) result[p.Id] = "X";
                else if (cv < 0.5) result[p.Id] = "Y";
                else result[p.Id] = "Z";
            }

            return result;
        }

        // Extra helper for the UI (not part of the required prompt methods).
        public async Task<List<ForecastingRow>> GetForecastingSnapshotAsync(int velocityDays = 30)
        {
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var rules = await _databaseService.Connection.Table<ReorderRule>().ToListAsync();
            var abc = await GetABCClassificationAsync();
            var xyz = await GetXYZClassificationAsync();

            var ruleByProduct = rules.ToDictionary(r => r.ProductId, r => r);

            var rows = new List<ForecastingRow>();
            foreach (var product in products)
            {
                var rule = ruleByProduct.TryGetValue(product.Id, out var r) ? r : null;
                var dailyVelocity = await GetSalesVelocityAsync(product.Id, velocityDays);
                var daysUntilStockout = await GetDaysUntilStockoutAsync(product.Id);

                var risk = GetRiskFor(daysUntilStockout);
                rows.Add(new ForecastingRow
                {
                    Product = product,
                    Rule = rule,
                    DailyVelocity = dailyVelocity,
                    DaysUntilStockout = daysUntilStockout,
                    RecommendedOrderQuantity = rule?.ReorderQuantity ?? 0,
                    ABCClassification = abc.TryGetValue(product.Id, out var a) ? a : "C",
                    XYZClassification = xyz.TryGetValue(product.Id, out var z) ? z : "Z",
                    StockoutRisk = risk.label,
                    RiskColorHex = risk.color
                });
            }

            // Urgency first
            rows.Sort((a, b) => a.DaysUntilStockout.CompareTo(b.DaysUntilStockout));
            return rows;
        }

        private static (string label, string color) GetRiskFor(int daysUntilStockout)
        {
            if (daysUntilStockout <= 1) return ("Critical", "#FF5252"); // Red
            if (daysUntilStockout <= 7) return ("Warning", "#F59E0B");  // Orange
            if (daysUntilStockout == int.MaxValue) return ("Healthy", "#10B981");
            return ("Healthy", "#10B981");
        }

        private async Task<List<(DateTime Day, int Units)>> GetDailySalesSeriesAsync(int productId, int days)
        {
            if (days <= 0) return new List<(DateTime, int)>();

            var fromDate = DateTime.Now.Date.AddDays(-(days - 1));
            var toDate = DateTime.Now.Date;

            var movements = await _databaseService.Connection.Table<StockMovement>()
                .Where(m => m.ProductId == productId &&
                            m.MovementType == "OUT" &&
                            m.Date >= fromDate &&
                            m.Date <= toDate)
                .ToListAsync();

            var grouped = movements
                .GroupBy(m => m.Date.Date)
                .ToDictionary(g => g.Key, g => g.Sum(m => m.QuantityChanged));

            var series = new List<(DateTime Day, int Units)>();
            for (var i = 0; i < days; i++)
            {
                var day = fromDate.AddDays(i);
                grouped.TryGetValue(day, out var units);
                series.Add((day, units));
            }

            return series;
        }
    }
}

