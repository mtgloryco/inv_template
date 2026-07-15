using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class CompanyBranchService
    {
        private readonly DatabaseService _databaseService;
        private readonly AuditService? _auditService;

        public CompanyBranchService(DatabaseService databaseService, AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _auditService = auditService;
        }

        public async Task<List<CompanyBranch>> GetAllBranchesAsync()
        {
            return await _databaseService.Connection.Table<CompanyBranch>()
                .Where(b => !b.IsDeleted)
                .OrderBy(b => b.Name)
                .ToListAsync();
        }

        public async Task<CompanyBranch> SaveBranchAsync(CompanyBranch branch, string username)
        {
            branch.UpdatedAt = DateTime.UtcNow;
            var isNew = branch.Id == 0;

            if (isNew)
            {
                await _databaseService.Connection.InsertAsync(branch);
            }
            else
            {
                await _databaseService.Connection.UpdateAsync(branch);
            }

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    username,
                    isNew ? "Create" : "Update",
                    "CompanyBranch",
                    branch.Id,
                    branch);
            }

            return branch;
        }

        public async Task AssignLocationToBranchAsync(int locationId, int branchId, string username)
        {
            var location = await _databaseService.Connection.FindAsync<Location>(locationId)
                           ?? throw new InvalidOperationException("Location not found.");

            location.UpdatedAt = DateTime.UtcNow;
            await _databaseService.Connection.ExecuteAsync(
                "UPDATE Location SET BranchId = ?, UpdatedAt = ? WHERE Id = ?",
                branchId, DateTime.UtcNow.ToString("O"), locationId);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "AssignBranch", "Location", locationId,
                    new { LocationId = locationId, BranchId = branchId, LocationName = location.Name });
            }
        }

        public async Task<List<ConsolidatedBranchLine>> GetConsolidatedReportAsync()
        {
            var branches = await GetAllBranchesAsync();
            if (branches.Count == 0)
            {
                return new List<ConsolidatedBranchLine>();
            }

            var locations = await _databaseService.Connection.Table<Location>()
                .Where(l => !l.IsDeleted)
                .ToListAsync();
            var locationStock = await _databaseService.Connection.Table<LocationStock>()
                .Where(ls => !ls.IsDeleted)
                .ToListAsync();
            var products = await _databaseService.Connection.Table<Product>()
                .Where(p => !p.IsDeleted)
                .ToListAsync();
            var productCosts = products.ToDictionary(p => p.Id, p => p.Cost);

            var salesOrders = await _databaseService.Connection.Table<SalesOrder>()
                .Where(so => !so.IsDeleted && so.Status != "Cancelled")
                .ToListAsync();
            var movements = await _databaseService.Connection.Table<StockMovement>()
                .Where(m => !m.IsDeleted && m.MovementType == "OUT")
                .ToListAsync();

            var lines = new List<ConsolidatedBranchLine>();
            foreach (var branch in branches)
            {
                var branchLocationIds = locations
                    .Where(l => l.BranchId == branch.Id)
                    .Select(l => l.Id)
                    .ToHashSet();

                var stockValue = locationStock
                    .Where(ls => branchLocationIds.Contains(ls.LocationId))
                    .Sum(ls => ls.Quantity * (productCosts.TryGetValue(ls.ProductId, out var cost) ? cost : 0m));

                var branchProductIds = locationStock
                    .Where(ls => branchLocationIds.Contains(ls.LocationId))
                    .Select(ls => ls.ProductId)
                    .ToHashSet();

                var revenue = movements
                    .Where(m => branchProductIds.Contains(m.ProductId))
                    .Sum(m => m.QuantityChanged * m.UnitPrice);

                lines.Add(new ConsolidatedBranchLine
                {
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    BranchCode = branch.Code,
                    Revenue = revenue,
                    StockValue = stockValue,
                    LocationCount = branchLocationIds.Count,
                    OpenSalesOrders = salesOrders.Count(so => so.Status is "Draft" or "Confirmed")
                });
            }

            return lines.OrderByDescending(l => l.Revenue).ToList();
        }
    }
}
