using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class MrpPlanningService
    {
        private readonly DatabaseService _databaseService;
        private readonly AuditService? _auditService;

        public MrpPlanningService(DatabaseService databaseService, AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _auditService = auditService;
        }

        public async Task<List<WorkCenter>> GetWorkCentersAsync()
        {
            return await _databaseService.Connection.Table<WorkCenter>()
                .Where(w => w.IsActive)
                .OrderBy(w => w.Name)
                .ToListAsync();
        }

        public async Task<WorkCenter> SaveWorkCenterAsync(WorkCenter center, string username)
        {
            var isNew = center.Id == 0;
            if (isNew)
            {
                await _databaseService.Connection.InsertAsync(center);
            }
            else
            {
                await _databaseService.Connection.UpdateAsync(center);
            }

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, isNew ? "Create" : "Update", "WorkCenter", center.Id, center);
            }

            return center;
        }

        public async Task<List<MrpPlannedOrder>> RunMrpAsync(string username, int horizonDays = 14)
        {
            var products = await _databaseService.Connection.Table<Product>()
                .Where(p => !p.IsDeleted && p.ProductType == "Good")
                .ToListAsync();
            var rules = await _databaseService.Connection.Table<ReorderRule>().ToListAsync();
            var boms = await _databaseService.Connection.Table<BillOfMaterial>().ToListAsync();
            var bomProductIds = boms.Select(b => b.ProductId).ToHashSet();
            var workCenters = await GetWorkCentersAsync();
            var defaultWorkCenter = workCenters.FirstOrDefault();

            var planned = new List<MrpPlannedOrder>();
            var endDate = DateTime.Today.AddDays(horizonDays);

            foreach (var rule in rules)
            {
                var product = products.FirstOrDefault(p => p.Id == rule.ProductId);
                if (product == null)
                {
                    continue;
                }

                var locationStock = rule.LocationId > 0
                    ? await _databaseService.Connection.Table<LocationStock>()
                        .FirstOrDefaultAsync(ls => ls.LocationId == rule.LocationId && ls.ProductId == product.Id && !ls.IsDeleted)
                    : null;

                var onHand = locationStock?.Quantity ?? product.StockQuantity;
                if (onHand >= rule.ReorderPoint)
                {
                    continue;
                }

                var qtyNeeded = Math.Max(rule.ReorderQuantity, rule.ReorderPoint - onHand + rule.ReorderQuantity);
                var orderType = bomProductIds.Contains(product.Id) ? "Manufacturing" : "Purchase";
                var leadDays = Math.Max(1, rule.LeadTimeDays);

                var order = new MrpPlannedOrder
                {
                    ProductId = product.Id,
                    OrderType = orderType,
                    Quantity = qtyNeeded,
                    PlannedStartDate = DateTime.Today,
                    PlannedEndDate = DateTime.Today.AddDays(leadDays),
                    Status = "Planned",
                    SourceReference = $"ReorderRule:{rule.Id}",
                    WorkCenterId = orderType == "Manufacturing" ? defaultWorkCenter?.Id : null,
                    CreatedAt = DateTime.UtcNow
                };

                await _databaseService.Connection.InsertAsync(order);
                planned.Add(order);
            }

            if (_auditService != null && planned.Count > 0)
            {
                await _auditService.LogActionAsync(username, "RunMRP", "MrpPlannedOrder", 0,
                    new { PlannedCount = planned.Count, HorizonDays = horizonDays, EndDate = endDate });
            }

            return planned;
        }

        public async Task<List<MrpPlannedOrder>> GetPlannedOrdersAsync()
        {
            return await _databaseService.Connection.Table<MrpPlannedOrder>()
                .Where(o => o.Status == "Planned")
                .OrderBy(o => o.PlannedStartDate)
                .ToListAsync();
        }

        public async Task<List<MrpCapacityLine>> GetCapacityUtilizationAsync(int horizonDays = 7)
        {
            var centers = await GetWorkCentersAsync();
            var plannedOrders = await _databaseService.Connection.Table<MrpPlannedOrder>()
                .Where(o => o.Status == "Planned" && o.OrderType == "Manufacturing")
                .ToListAsync();

            var cutoff = DateTime.Today.AddDays(horizonDays);
            var lines = new List<MrpCapacityLine>();

            foreach (var center in centers)
            {
                var effectiveHoursPerDay = center.HoursPerDay * (center.EfficiencyPercent / 100.0);
                var availableHours = effectiveHoursPerDay * horizonDays;

                var centerOrders = plannedOrders
                    .Where(o => o.WorkCenterId == center.Id && o.PlannedEndDate <= cutoff)
                    .ToList();

                var scheduledHours = centerOrders.Sum(o => o.Quantity * 0.5);
                var utilization = availableHours <= 0 ? 0 : (scheduledHours / availableHours) * 100.0;

                lines.Add(new MrpCapacityLine
                {
                    WorkCenterId = center.Id,
                    WorkCenterName = center.Name,
                    AvailableHours = availableHours,
                    ScheduledHours = scheduledHours,
                    UtilizationPercent = Math.Round(utilization, 1),
                    IsOverloaded = utilization > 100.0
                });
            }

            return lines;
        }
    }
}
