using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class AdvancedAnalyticsViewModel : ViewModelBase
{
    private readonly AdvancedAnalyticsService _analyticsService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _healthScore;
    [ObservableProperty] private decimal _carryingCost;

    public ObservableCollection<ProfitabilityItem> TopProducts { get; } = new();
    public ObservableCollection<ProfitabilityItem> ParetoProducts { get; } = new();

    public AdvancedAnalyticsViewModel(AdvancedAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
        _ = LoadData();
    }

    [RelayCommand]
    public async Task LoadData()
    {
        IsLoading = true;
        HealthScore = await _analyticsService.GetInventoryHealthScoreAsync();
        CarryingCost = await _analyticsService.GetCarryingCostAnalysisAsync();

        TopProducts.Clear();
        var ranking = await _analyticsService.GetProfitabilityRankingAsync();
        foreach (var item in ranking.Take(10)) TopProducts.Add(item);

        ParetoProducts.Clear();
        var pareto = await _analyticsService.GetParetoAnalysisAsync();
        foreach (var item in pareto) ParetoProducts.Add(item);

        IsLoading = false;
    }
}
