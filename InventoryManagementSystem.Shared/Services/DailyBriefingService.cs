using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class BriefingItem
    {
        public string Priority { get; set; } = "Info"; // Critical, Warning, Info, Positive
        public string Icon { get; set; } = "📋"; // emoji or icon key
        public string Message { get; set; } = string.Empty;
        public string ActionLabel { get; set; } = "Review Now";
        public string NavigateTo { get; set; } = "Inventory"; // View name
    }

    public class DailyBriefingService
    {
        private readonly DatabaseService _databaseService;

        public DailyBriefingService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<BriefingItem>> GetDailyBriefingAsync()
        {
            var briefing = new List<BriefingItem>();

            // 1. Check for Expiring items
            var now = DateTime.Now;
            var sevenDays = now.AddDays(7);
            var threeDays = now.AddDays(3);

            var expiringBatches = await _databaseService.Connection.Table<PurchaseBatch>()
                .Where(b => b.ExpiryDate != null && b.ExpiryDate <= sevenDays && b.QuantityRemaining > 0)
                .ToListAsync();

            if (expiringBatches.Any())
            {
                var criticalCount = expiringBatches.Count(b => b.ExpiryDate <= threeDays);
                var warningCount = expiringBatches.Count(b => b.ExpiryDate > threeDays);

                if (criticalCount > 0)
                {
                    briefing.Add(new BriefingItem
                    {
                        Priority = "Critical",
                        Icon = "🚨",
                        Message = $"{criticalCount} items are expiring in less than 3 days!",
                        ActionLabel = "See All",
                        NavigateTo = "ExpiryDashboard"
                    });
                }

                if (warningCount > 0)
                {
                    briefing.Add(new BriefingItem
                    {
                        Priority = "Warning",
                        Icon = "⚠️",
                        Message = $"{warningCount} items are expiring within a week.",
                        ActionLabel = "Review",
                        NavigateTo = "ExpiryDashboard"
                    });
                }
            }

            // 2. Check for Stockout risks (items below reorder point)
            var lowStock = await _databaseService.Connection.Table<Product>()
                .Where(p => p.StockQuantity <= 5) // Generic threshold as per prompt logic
                .ToListAsync();

            if (lowStock.Any())
            {
                briefing.Add(new BriefingItem
                {
                    Priority = "Warning",
                    Icon = "📦",
                    Message = $"{lowStock.Count} products are low on stock.",
                    ActionLabel = "Order Now",
                    NavigateTo = "Forecasting"
                });
            }

            // 3. Pending POs
            var pendingPOs = await _databaseService.Connection.Table<PurchaseOrder>()
                .Where(po => po.Status == "Pending" || po.Status == "Approved")
                .ToListAsync();

            if (pendingPOs.Any())
            {
                briefing.Add(new BriefingItem
                {
                    Priority = "Info",
                    Icon = "📝",
                    Message = $"{pendingPOs.Count} purchase orders are awaiting next steps.",
                    ActionLabel = "View Orders",
                    NavigateTo = "PurchaseOrders"
                });
            }

            // 4. POS Daily Performance (Simulated)
            briefing.Add(new BriefingItem
            {
                Priority = "Positive",
                Icon = "📈",
                Message = "Sales are up 12% compared to last Monday!",
                ActionLabel = "View Reports",
                NavigateTo = "Reports"
            });

            return briefing.OrderBy(b => b.Priority == "Critical" ? 0 : b.Priority == "Warning" ? 1 : 2).ToList();
        }
    }
}
