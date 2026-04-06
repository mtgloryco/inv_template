using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class BundleViewModel : ViewModelBase
{
    private readonly BundleService _bundleService;
    private readonly InventoryService _inventoryService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Product? _selectedBundle;
    [ObservableProperty] private int _assembleQuantity = 1;
    [ObservableProperty] private int _availableToAssemble;

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<ProductBundle> CurrentComponents { get; } = new();

    public BundleViewModel(BundleService bundleService, InventoryService inventoryService)
    {
        _bundleService = bundleService;
        _inventoryService = inventoryService;
        _ = LoadInitialData();
    }

    public async Task LoadInitialData()
    {
        IsLoading = true;
        Products.Clear();
        var products = await _inventoryService.GetAllProductsAsync();
        foreach (var p in products) Products.Add(p);
        IsLoading = false;
    }

    partial void OnSelectedBundleChanged(Product? value)
    {
        if (value != null) _ = LoadBundleInfo(value.Id);
        else
        {
            CurrentComponents.Clear();
            AvailableToAssemble = 0;
        }
    }

    private async Task LoadBundleInfo(int bundleId)
    {
        CurrentComponents.Clear();
        var components = await _bundleService.GetBundleComponentsAsync(bundleId);
        foreach (var c in components) CurrentComponents.Add(c);
        AvailableToAssemble = await _bundleService.GetAvailableBundleQuantityAsync(bundleId);
    }

    [RelayCommand]
    public async Task Assemble()
    {
        if (SelectedBundle == null || AssembleQuantity <= 0) return;
        
        try
        {
            await _bundleService.AssembleBundleAsync(SelectedBundle.Id, AssembleQuantity, UserSession.CurrentUser?.Username ?? "System");
            await LoadBundleInfo(SelectedBundle.Id); // Refresh stock/availability
            AssembleQuantity = 1;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }

    [RelayCommand]
    public async Task Disassemble()
    {
        if (SelectedBundle == null || AssembleQuantity <= 0) return;
        
        try
        {
            await _bundleService.DisassembleBundleAsync(SelectedBundle.Id, AssembleQuantity, UserSession.CurrentUser?.Username ?? "System");
            await LoadBundleInfo(SelectedBundle.Id); // Refresh
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }
}
