using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class AnalyticsViewModel : ViewModelBase
{
    private readonly AnalyticsService _analyticsService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private DateTimeOffset _startDate = DateTimeOffset.Now.AddDays(-30);

    [ObservableProperty]
    private DateTimeOffset _endDate = DateTimeOffset.Now;

    public ObservableCollection<ProductRecommendation> ReorderList { get; } = new();
    public ObservableCollection<ProductRecommendation> DeadStockList { get; } = new();
    public ObservableCollection<ProductRecommendation> LowMarginList { get; } = new();

    public AnalyticsViewModel(AnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
        LoadDataCommand();
    }

    [RelayCommand]
    public async void LoadDataCommand()
    {
        IsLoading = true;
        ReorderList.Clear();
        DeadStockList.Clear();
        LowMarginList.Clear();

        // Reorders are "Current State" so dates don't apply as much, but we could filter velocity.
        // For now, keep Reorder strictly based on Current Stock vs Threshold.
        var reorders = await _analyticsService.GetReorderRecommendationsAsync();
        foreach (var item in reorders) ReorderList.Add(item);

        // Dead Stock & Margins RESPECT Dates
        var deadStock = await _analyticsService.GetDeadStockAsync(StartDate.DateTime, EndDate.DateTime);
        foreach (var item in deadStock) DeadStockList.Add(item);

        var margins = await _analyticsService.GetLowMarginProductsAsync(StartDate.DateTime, EndDate.DateTime);
        foreach (var item in margins) LowMarginList.Add(item);

        IsLoading = false;
    }
}
