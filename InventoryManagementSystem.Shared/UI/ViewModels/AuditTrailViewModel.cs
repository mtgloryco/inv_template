using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class AuditTrailViewModel : ViewModelBase
{
    private readonly AuditService _auditService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private DateTimeOffset _startDate = DateTimeOffset.Now.AddDays(-7);
    [ObservableProperty] private DateTimeOffset _endDate = DateTimeOffset.Now;

    public ObservableCollection<AuditLog> Logs { get; } = new();

    public AuditTrailViewModel(AuditService auditService)
    {
        _auditService = auditService;
        _ = LoadLogs();
    }

    [RelayCommand]
    public async Task LoadLogs()
    {
        IsLoading = true;
        Logs.Clear();
        var logs = await _auditService.GetAuditReportAsync(StartDate.DateTime, EndDate.DateTime);
        foreach (var l in logs) Logs.Add(l);
        IsLoading = false;
    }
}
