using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class EnterpriseViewModel : ViewModelBase
{
    private readonly CompanyBranchService _companyBranchService;
    private readonly WorkflowApprovalService _workflowApprovalService;
    private readonly MrpPlanningService _mrpPlanningService;
    private readonly CrmPipelineService _crmPipelineService;
    private readonly MobileFieldService _mobileFieldService;
    private readonly SecurityComplianceService _securityComplianceService;
    private readonly InventoryService _inventoryService;

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<CompanyBranch> Branches { get; } = new();
    public ObservableCollection<ConsolidatedBranchLine> ConsolidatedLines { get; } = new();
    public ObservableCollection<ApprovalRequest> PendingApprovals { get; } = new();
    public ObservableCollection<CrmOpportunityListItem> Pipeline { get; } = new();
    public ObservableCollection<MrpPlannedOrder> PlannedOrders { get; } = new();
    public ObservableCollection<MrpCapacityLine> CapacityLines { get; } = new();
    public ObservableCollection<MobileDeviceRegistration> MobileDevices { get; } = new();
    public ObservableCollection<WorkCenter> WorkCenters { get; } = new();

    [ObservableProperty] private CompanyBranch? _selectedBranch;
    [ObservableProperty] private ApprovalRequest? _selectedApproval;
    [ObservableProperty] private CrmOpportunityListItem? _selectedOpportunity;
    [ObservableProperty] private SecurityPolicy? _securityPolicy;

    [ObservableProperty] private string _newBranchName = string.Empty;
    [ObservableProperty] private string _newBranchCode = string.Empty;
    [ObservableProperty] private string _newOpportunityTitle = string.Empty;
    [ObservableProperty] private int _newOpportunityCustomerId = 1;
    [ObservableProperty] private decimal _newOpportunityRevenue;
    [ObservableProperty] private string _newDeviceName = string.Empty;
    [ObservableProperty] private string _newDeviceType = "Warehouse";
    [ObservableProperty] private string _generatedApiKey = string.Empty;
    [ObservableProperty] private string _ssoProvider = string.Empty;
    [ObservableProperty] private string _ssoClientId = string.Empty;
    [ObservableProperty] private bool _backupWithinSla;

    public string[] DeviceTypes { get; } = { "Warehouse", "FieldSales" };
    public string[] PipelineStages { get; } = CrmPipelineService.PipelineStages;

    public EnterpriseViewModel(
        CompanyBranchService companyBranchService,
        WorkflowApprovalService workflowApprovalService,
        MrpPlanningService mrpPlanningService,
        CrmPipelineService crmPipelineService,
        MobileFieldService mobileFieldService,
        SecurityComplianceService securityComplianceService,
        InventoryService inventoryService)
    {
        _companyBranchService = companyBranchService;
        _workflowApprovalService = workflowApprovalService;
        _mrpPlanningService = mrpPlanningService;
        _crmPipelineService = crmPipelineService;
        _mobileFieldService = mobileFieldService;
        _securityComplianceService = securityComplianceService;
        _inventoryService = inventoryService;
        _ = LoadAllAsync();
    }

    [RelayCommand]
    public async Task LoadAllAsync()
    {
        await LoadBranchesAsync();
        await LoadApprovalsAsync();
        await LoadPipelineAsync();
        await LoadMrpAsync();
        await LoadMobileDevicesAsync();
        await LoadSecurityAsync();
    }

    [RelayCommand]
    public async Task LoadBranchesAsync()
    {
        Branches.Clear();
        ConsolidatedLines.Clear();
        foreach (var b in await _companyBranchService.GetAllBranchesAsync()) Branches.Add(b);
        foreach (var line in await _companyBranchService.GetConsolidatedReportAsync()) ConsolidatedLines.Add(line);
    }

    [RelayCommand]
    public async Task SaveBranchAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBranchName))
        {
            StatusMessage = "Branch name is required.";
            return;
        }

        var username = UserSession.CurrentUser?.Username ?? "System";
        await _companyBranchService.SaveBranchAsync(new CompanyBranch
        {
            Name = NewBranchName.Trim(),
            Code = string.IsNullOrWhiteSpace(NewBranchCode) ? NewBranchName.Trim()[..Math.Min(3, NewBranchName.Trim().Length)].ToUpperInvariant() : NewBranchCode.Trim().ToUpperInvariant()
        }, username);

        NewBranchName = string.Empty;
        NewBranchCode = string.Empty;
        StatusMessage = "Branch saved.";
        await LoadBranchesAsync();
    }

    [RelayCommand]
    public async Task LoadApprovalsAsync()
    {
        PendingApprovals.Clear();
        foreach (var a in await _workflowApprovalService.GetPendingApprovalsAsync()) PendingApprovals.Add(a);
    }

    [RelayCommand]
    public async Task ApproveSelectedAsync()
    {
        if (SelectedApproval == null) return;
        var username = UserSession.CurrentUser?.Username ?? "System";
        await _workflowApprovalService.ApproveAsync(SelectedApproval.Id, username, "Approved from enterprise inbox");
        StatusMessage = $"Approved {SelectedApproval.RequestType} request.";
        await LoadApprovalsAsync();
    }

    [RelayCommand]
    public async Task RejectSelectedAsync()
    {
        if (SelectedApproval == null) return;
        var username = UserSession.CurrentUser?.Username ?? "System";
        await _workflowApprovalService.RejectAsync(SelectedApproval.Id, username, "Rejected from enterprise inbox");
        StatusMessage = $"Rejected {SelectedApproval.RequestType} request.";
        await LoadApprovalsAsync();
    }

    [RelayCommand]
    public async Task LoadPipelineAsync()
    {
        Pipeline.Clear();
        foreach (var item in await _crmPipelineService.GetPipelineAsync()) Pipeline.Add(item);
    }

    [RelayCommand]
    public async Task CreateOpportunityAsync()
    {
        if (string.IsNullOrWhiteSpace(NewOpportunityTitle))
        {
            StatusMessage = "Opportunity title is required.";
            return;
        }

        var username = UserSession.CurrentUser?.Username ?? "System";
        await _crmPipelineService.CreateOpportunityAsync(new CrmOpportunity
        {
            Title = NewOpportunityTitle.Trim(),
            CustomerId = NewOpportunityCustomerId,
            ExpectedRevenue = NewOpportunityRevenue,
            Stage = "Lead",
            ExpectedCloseDate = DateTime.Today.AddDays(30)
        }, username);

        NewOpportunityTitle = string.Empty;
        NewOpportunityRevenue = 0;
        StatusMessage = "Opportunity created.";
        await LoadPipelineAsync();
    }

    [RelayCommand]
    public async Task ConvertOpportunityToQuotationAsync()
    {
        if (SelectedOpportunity == null)
        {
            StatusMessage = "Select an opportunity first.";
            return;
        }

        var products = await _inventoryService.GetAllProductsAsync();
        var product = products.FirstOrDefault();
        if (product == null)
        {
            StatusMessage = "No products available for quotation.";
            return;
        }

        var username = UserSession.CurrentUser?.Username ?? "System";
        var qty = Math.Max(1, (int)Math.Ceiling(SelectedOpportunity.Opportunity.ExpectedRevenue / Math.Max(product.Price, 1m)));
        var so = await _crmPipelineService.ConvertToQuotationAsync(
            SelectedOpportunity.Opportunity.Id,
            new System.Collections.Generic.List<SalesOrderItem>
            {
                new() { ProductId = product.Id, QuantityOrdered = qty, UnitPrice = product.Price }
            },
            username);

        StatusMessage = $"Quotation {so.SONumber} created.";
        await LoadPipelineAsync();
    }

    [RelayCommand]
    public async Task LoadMrpAsync()
    {
        PlannedOrders.Clear();
        CapacityLines.Clear();
        WorkCenters.Clear();
        foreach (var w in await _mrpPlanningService.GetWorkCentersAsync()) WorkCenters.Add(w);
        foreach (var o in await _mrpPlanningService.GetPlannedOrdersAsync()) PlannedOrders.Add(o);
        foreach (var c in await _mrpPlanningService.GetCapacityUtilizationAsync()) CapacityLines.Add(c);
    }

    [RelayCommand]
    public async Task RunMrpAsync()
    {
        var username = UserSession.CurrentUser?.Username ?? "System";
        var planned = await _mrpPlanningService.RunMrpAsync(username);
        StatusMessage = $"MRP run complete — {planned.Count} planned order(s).";
        await LoadMrpAsync();
    }

    [RelayCommand]
    public async Task LoadMobileDevicesAsync()
    {
        MobileDevices.Clear();
        foreach (var d in await _mobileFieldService.GetActiveDevicesAsync()) MobileDevices.Add(d);
    }

    [RelayCommand]
    public async Task RegisterMobileDeviceAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDeviceName) || SelectedBranch == null)
        {
            StatusMessage = "Device name and branch are required.";
            return;
        }

        var username = UserSession.CurrentUser?.Username ?? "System";
        var (device, apiKey) = await _mobileFieldService.RegisterDeviceAsync(
            NewDeviceName.Trim(), NewDeviceType, SelectedBranch.Id, username);
        GeneratedApiKey = apiKey;
        NewDeviceName = string.Empty;
        StatusMessage = $"Device '{device.DeviceName}' registered.";
        await LoadMobileDevicesAsync();
    }

    [RelayCommand]
    public async Task LoadSecurityAsync()
    {
        SecurityPolicy = await _securityComplianceService.GetPolicyAsync();
        BackupWithinSla = await _securityComplianceService.ValidateLatestBackupWithinSlaAsync();
    }

    [RelayCommand]
    public async Task SaveSecurityPolicyAsync()
    {
        if (SecurityPolicy == null) return;
        var username = UserSession.CurrentUser?.Username ?? "System";
        await _securityComplianceService.UpdatePolicyAsync(SecurityPolicy, username);
        StatusMessage = "Security policy updated.";
        await LoadSecurityAsync();
    }

    [RelayCommand]
    public async Task ConfigureSsoAsync()
    {
        if (string.IsNullOrWhiteSpace(SsoProvider)) return;
        var username = UserSession.CurrentUser?.Username ?? "System";
        await _securityComplianceService.ConfigureSsoAsync(SsoProvider.Trim(), SsoClientId.Trim(), username);
        StatusMessage = $"SSO configured for {SsoProvider}.";
        await LoadSecurityAsync();
    }
}
