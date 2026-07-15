using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class CrmPipelineService
    {
        private readonly DatabaseService _databaseService;
        private readonly SalesOrderService _salesOrderService;
        private readonly AuditService? _auditService;

        public static readonly string[] PipelineStages = { "Lead", "Qualified", "Proposal", "Won", "Lost" };

        public CrmPipelineService(
            DatabaseService databaseService,
            SalesOrderService salesOrderService,
            AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _salesOrderService = salesOrderService;
            _auditService = auditService;
        }

        public async Task<List<CrmOpportunityListItem>> GetPipelineAsync()
        {
            var opportunities = await _databaseService.Connection.Table<CrmOpportunity>()
                .Where(o => !o.IsDeleted)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var customers = await _databaseService.Connection.Table<Customer>().ToListAsync();
            var customerDict = customers.ToDictionary(c => c.Id, c => c.Name);

            var salesOrders = await _databaseService.Connection.Table<SalesOrder>().ToListAsync();
            var soDict = salesOrders.ToDictionary(so => so.Id, so => so.SONumber);

            return opportunities.Select(o => new CrmOpportunityListItem
            {
                Opportunity = o,
                CustomerName = customerDict.TryGetValue(o.CustomerId, out var name) ? name : "Unknown",
                QuotationNumber = o.SalesOrderId.HasValue && soDict.TryGetValue(o.SalesOrderId.Value, out var num)
                    ? num
                    : string.Empty
            }).ToList();
        }

        public async Task<CrmOpportunity> CreateOpportunityAsync(CrmOpportunity opportunity, string username)
        {
            opportunity.CreatedAt = DateTime.UtcNow;
            opportunity.UpdatedAt = DateTime.UtcNow;
            opportunity.AssignedToUsername = string.IsNullOrWhiteSpace(opportunity.AssignedToUsername)
                ? username
                : opportunity.AssignedToUsername;

            await _databaseService.Connection.InsertAsync(opportunity);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "Create", "CrmOpportunity", opportunity.Id, opportunity);
            }

            return opportunity;
        }

        public async Task<CrmOpportunity> UpdateStageAsync(int opportunityId, string stage, string username)
        {
            if (!PipelineStages.Contains(stage))
            {
                throw new ArgumentException($"Invalid stage: {stage}");
            }

            var opportunity = await _databaseService.Connection.FindAsync<CrmOpportunity>(opportunityId)
                              ?? throw new InvalidOperationException("Opportunity not found.");

            var oldStage = opportunity.Stage;
            opportunity.Stage = stage;
            opportunity.UpdatedAt = DateTime.UtcNow;
            await _databaseService.Connection.UpdateAsync(opportunity);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "UpdateStage", "CrmOpportunity", opportunity.Id,
                    new { OldStage = oldStage, NewStage = stage });
            }

            return opportunity;
        }

        public async Task<SalesOrder> ConvertToQuotationAsync(int opportunityId, List<SalesOrderItem> items, string username)
        {
            var opportunity = await _databaseService.Connection.FindAsync<CrmOpportunity>(opportunityId)
                              ?? throw new InvalidOperationException("Opportunity not found.");

            var so = new SalesOrder
            {
                CustomerId = opportunity.CustomerId,
                Status = "Draft",
                Notes = $"Converted from CRM opportunity: {opportunity.Title}",
                CreatedByUsername = username,
                Company = "My Company"
            };

            await _salesOrderService.CreateSalesQuotationAsync(so, items);

            opportunity.SalesOrderId = so.Id;
            opportunity.Stage = "Proposal";
            opportunity.UpdatedAt = DateTime.UtcNow;
            await _databaseService.Connection.UpdateAsync(opportunity);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "ConvertToQuotation", "CrmOpportunity", opportunity.Id,
                    new { SalesOrderId = so.Id, SONumber = so.SONumber });
            }

            return so;
        }
    }
}
