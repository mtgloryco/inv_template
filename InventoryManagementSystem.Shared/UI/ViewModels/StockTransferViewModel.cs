using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class StockTransferViewModel : ViewModelBase
{
    private readonly LocationService _locationService;
    private readonly InventoryService _inventoryService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Location? _sourceLocation;
    [ObservableProperty] private Location? _destLocation;
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private int _quantity;
    [ObservableProperty] private string _notes = "";

    public ObservableCollection<Location> Locations { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();

    public StockTransferViewModel(LocationService locationService, InventoryService inventoryService)
    {
        _locationService = locationService;
        _inventoryService = inventoryService;
        _ = LoadInitialData();
    }

    [RelayCommand]
    public async Task LoadInitialData()
    {
        IsLoading = true;
        Locations.Clear();
        var locations = await _locationService.GetAllLocationsAsync();
        foreach (var loc in locations) Locations.Add(loc);

        Products.Clear();
        var products = await _inventoryService.GetAllProductsAsync();
        foreach (var p in products) Products.Add(p);
        IsLoading = false;
    }

    [RelayCommand]
    public async Task PerformTransfer()
    {
        if (SourceLocation == null || DestLocation == null || SelectedProduct == null || Quantity <= 0) return;
        if (SourceLocation.Id == DestLocation.Id) return;

        var transfer = new StockTransfer
        {
            FromLocationId = SourceLocation.Id,
            ToLocationId = DestLocation.Id,
            ProductId = SelectedProduct.Id,
            Quantity = Quantity,
            RequestedByUsername = UserSession.CurrentUser?.Username ?? "System",
            Notes = Notes
        };

        try
        {
            await _locationService.TransferStockAsync(transfer);
            Quantity = 0;
            Notes = "";
            // Optionally navigate back or show success message
        }
        catch (System.Exception ex)
        {
            // Handle error (insufficient stock)
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }
}
