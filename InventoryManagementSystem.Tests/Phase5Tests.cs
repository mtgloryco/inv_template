using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using InventoryManagementSystem.Services;
using Xunit;

namespace InventoryManagementSystem.Tests;

public class Phase5Tests : IAsyncLifetime
{
    private readonly string _dbPath = TempFile.CreateDbPath();
    private DatabaseService _db = null!;
    private AuditService _audit = null!;
    private InventoryService _inventory = null!;
    private SalesOrderService _salesOrderService = null!;
    private PurchaseOrderService _purchaseOrderService = null!;
    private CompanyBranchService _companyBranchService = null!;
    private WorkflowApprovalService _workflowApprovalService = null!;
    private MrpPlanningService _mrpPlanningService = null!;
    private CrmPipelineService _crmPipelineService = null!;
    private MobileFieldService _mobileFieldService = null!;
    private SecurityComplianceService _securityComplianceService = null!;

    public async Task InitializeAsync()
    {
        _db = new DatabaseService(_dbPath);
        await _db.InitializeAsync();
        _audit = new AuditService(_db);
        var license = new LicenseService(_db, new HardwareIdService(), new LicenseCryptoService());
        await license.InitializeAsync();
        _inventory = new InventoryService(_db, license, _audit);
        _salesOrderService = new SalesOrderService(_db, _inventory, _audit);
        _purchaseOrderService = new PurchaseOrderService(_db, _inventory, _audit);
        _companyBranchService = new CompanyBranchService(_db, _audit);
        _workflowApprovalService = new WorkflowApprovalService(_db, _purchaseOrderService, _audit);
        _mrpPlanningService = new MrpPlanningService(_db, _audit);
        _crmPipelineService = new CrmPipelineService(_db, _salesOrderService, _audit);
        _mobileFieldService = new MobileFieldService(_db, _audit);
        _securityComplianceService = new SecurityComplianceService(_db, _audit);
    }

    public async Task DisposeAsync()
    {
        await _db.CloseConnectionAsync();
        TempFile.DeleteDbFiles(_dbPath);
    }

    [Fact]
    public async Task CompanyBranch_CreatesBranchAndConsolidatedReport()
    {
        var branch = await _companyBranchService.SaveBranchAsync(new CompanyBranch
        {
            Name = "Kigali Branch",
            Code = "KGL",
            Currency = "RWF"
        }, "tester");

        var location = await _db.Connection.Table<Location>().FirstAsync();
        await _companyBranchService.AssignLocationToBranchAsync(location.Id, branch.Id, "tester");

        var product = new Product { Name = "Branch Item", Cost = 20, Price = 50, StockQuantity = 5 };
        await _db.Connection.InsertAsync(product);
        await _db.Connection.InsertAsync(new LocationStock
        {
            LocationId = location.Id,
            ProductId = product.Id,
            Quantity = 5
        });

        await _inventory.AddStockMovementAsync(product.Id, 2, "OUT", "Branch sale", "tester", unitPrice: 50, postSalesRevenueJournal: true);

        var report = await _companyBranchService.GetConsolidatedReportAsync();
        var line = report.First(l => l.BranchId == branch.Id);
        Assert.Equal("KGL", line.BranchCode);
        Assert.True(line.StockValue > 0);
        Assert.True(line.Revenue > 0);

        var logs = await _audit.GetAuditTrailAsync("CompanyBranch", branch.Id);
        Assert.Contains(logs, l => l.Action == "Create");
    }

    [Fact]
    public async Task WorkflowApproval_SubmitAndApprovePurchaseOrder()
    {
        var supplier = await _db.Connection.Table<Supplier>().FirstAsync();
        var product = new Product { Name = "PO Approval Item", Cost = 100, Price = 200, StockQuantity = 0 };
        await _db.Connection.InsertAsync(product);

        var po = new PurchaseOrder { SupplierId = supplier.Id, Status = "Draft", TotalAmount = 2500 };
        var poItem = new PurchaseOrderItem { ProductId = product.Id, QuantityOrdered = 25, UnitCost = 100 };
        await _purchaseOrderService.CreatePurchaseOrderAsync(po, new List<PurchaseOrderItem> { poItem });

        var request = await _workflowApprovalService.RequestPurchaseOrderApprovalAsync(po.Id, "buyer");
        Assert.NotNull(request);
        Assert.Equal("Pending", request!.Status);

        po = await _db.Connection.FindAsync<PurchaseOrder>(po.Id);
        Assert.Equal("PendingApproval", po!.Status);

        await _workflowApprovalService.ApproveAsync(request.Id, "manager", "Within budget");
        po = await _db.Connection.FindAsync<PurchaseOrder>(po.Id);
        Assert.Equal("Approved", po!.Status);

        var logs = await _audit.GetAuditTrailAsync("ApprovalRequest", request.Id);
        Assert.Contains(logs, l => l.Action == "Approve");
    }

    [Fact]
    public async Task WorkflowApproval_RejectDiscountRequest()
    {
        var request = await _workflowApprovalService.RequestDiscountApprovalAsync(
            salesOrderId: 1, discountAmount: 150, requestedBy: "cashier", notes: "VIP discount");

        var rejected = await _workflowApprovalService.RejectAsync(request.Id, "manager", "Exceeds limit");
        Assert.Equal("Rejected", rejected.Status);

        var logs = await _audit.GetAuditTrailAsync("ApprovalRequest", request.Id);
        Assert.Contains(logs, l => l.Action == "Reject");
    }

    [Fact]
    public async Task MrpPlanning_GeneratesPlannedOrdersFromShortfall()
    {
        var location = await _db.Connection.Table<Location>().FirstAsync();
        var product = new Product { Name = "MRP Widget", Cost = 10, Price = 25, StockQuantity = 2, ProductType = "Good" };
        await _db.Connection.InsertAsync(product);

        await _db.Connection.InsertAsync(new LocationStock
        {
            LocationId = location.Id,
            ProductId = product.Id,
            Quantity = 2
        });

        await _db.Connection.InsertAsync(new ReorderRule
        {
            ProductId = product.Id,
            LocationId = location.Id,
            PreferredSupplierId = 1,
            ReorderPoint = 10,
            ReorderQuantity = 20,
            LeadTimeDays = 5
        });

        var planned = await _mrpPlanningService.RunMrpAsync("planner");
        Assert.NotEmpty(planned);
        Assert.All(planned, p => Assert.Equal("Planned", p.Status));
        Assert.Contains(planned, p => p.ProductId == product.Id && p.Quantity >= 20);
    }

    [Fact]
    public async Task MrpPlanning_CapacityUtilizationTracksWorkCenter()
    {
        var branch = (await _companyBranchService.GetAllBranchesAsync()).First();
        var center = await _mrpPlanningService.SaveWorkCenterAsync(new WorkCenter
        {
            Name = "Assembly Line 1",
            BranchId = branch.Id,
            HoursPerDay = 8,
            EfficiencyPercent = 90
        }, "tester");

        await _db.Connection.InsertAsync(new MrpPlannedOrder
        {
            ProductId = 1,
            OrderType = "Manufacturing",
            Quantity = 20,
            WorkCenterId = center.Id,
            PlannedStartDate = DateTime.Today,
            PlannedEndDate = DateTime.Today.AddDays(3),
            Status = "Planned"
        });

        var capacity = await _mrpPlanningService.GetCapacityUtilizationAsync();
        var line = Assert.Single(capacity, c => c.WorkCenterId == center.Id);
        Assert.True(line.AvailableHours > 0);
        Assert.True(line.ScheduledHours > 0);
    }

    [Fact]
    public async Task CrmPipeline_ConvertOpportunityToQuotation()
    {
        var customer = new Customer { Name = "CRM Prospect", Email = "prospect@example.com" };
        await _db.Connection.InsertAsync(customer);

        var opportunity = await _crmPipelineService.CreateOpportunityAsync(new CrmOpportunity
        {
            Title = "Enterprise Deal",
            CustomerId = customer.Id,
            ExpectedRevenue = 5000,
            Stage = "Qualified"
        }, "salesrep");

        var product = new Product { Name = "CRM Product", Price = 500, Cost = 200, StockQuantity = 10 };
        await _db.Connection.InsertAsync(product);

        var so = await _crmPipelineService.ConvertToQuotationAsync(
            opportunity.Id,
            new List<SalesOrderItem> { new() { ProductId = product.Id, QuantityOrdered = 10, UnitPrice = 500 } },
            "salesrep");

        Assert.StartsWith("SQ-", so.SONumber);
        Assert.Equal("Draft", so.Status);

        opportunity = await _db.Connection.FindAsync<CrmOpportunity>(opportunity.Id);
        Assert.Equal("Proposal", opportunity!.Stage);
        Assert.Equal(so.Id, opportunity.SalesOrderId);

        var logs = await _audit.GetAuditTrailAsync("CrmOpportunity", opportunity.Id);
        Assert.Contains(logs, l => l.Action == "ConvertToQuotation");
    }

    [Fact]
    public async Task MobileField_RegistersDeviceAndProcessesSyncQueue()
    {
        var branch = (await _companyBranchService.GetAllBranchesAsync()).First();
        var (device, apiKey) = await _mobileFieldService.RegisterDeviceAsync(
            "Scanner-01", "Warehouse", branch.Id, "admin");

        Assert.NotEmpty(apiKey);
        Assert.Equal(MobileFieldService.HashApiKey(apiKey), device.ApiKeyHash);

        await _mobileFieldService.QueueSyncOperationAsync(device.Id, "PickListExport",
            new { Action = "Export" }, "admin");

        var processed = await _mobileFieldService.ProcessPendingSyncAsync(device.Id);
        Assert.Equal(1, processed);

        var pickList = await _mobileFieldService.ExportWarehousePickListAsync(branch.Id);
        Assert.Contains("BranchId", pickList);
    }

    [Fact]
    public async Task SecurityCompliance_RecordsBackupSlaAndPolicy()
    {
        var policy = await _securityComplianceService.GetPolicyAsync();
        policy.MinPasswordLength = 10;
        policy.BackupSlaHours = 12;
        await _securityComplianceService.UpdatePolicyAsync(policy, "admin");

        var started = DateTime.UtcNow.AddHours(-1);
        var completed = DateTime.UtcNow;
        var log = await _securityComplianceService.RecordBackupAsync("Cloud", started, completed, true, 1024);
        Assert.True(log.WithinSla);
        Assert.True(await _securityComplianceService.ValidateLatestBackupWithinSlaAsync());

        await _securityComplianceService.ConfigureSsoAsync("AzureAD", "client-id-123", "admin");
        policy = await _securityComplianceService.GetPolicyAsync();
        Assert.Equal("AzureAD", policy.SsoProvider);
        Assert.Equal("client-id-123", policy.SsoClientId);
        Assert.True(_securityComplianceService.ValidatePasswordAgainstPolicy("longpassword", policy));
    }
}
