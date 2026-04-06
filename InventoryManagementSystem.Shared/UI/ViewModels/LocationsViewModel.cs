using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class LocationsViewModel : ViewModelBase
{
    private readonly LocationService _locationService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Location? _selectedLocation;
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _newType = "Warehouse";
    [ObservableProperty] private string _newAddress = "";

    public ObservableCollection<Location> Locations { get; } = new();
    public ObservableCollection<LocationStock> CurrentLocationStock { get; } = new();

    public LocationsViewModel(LocationService locationService)
    {
        _locationService = locationService;
        _ = LoadLocations();
    }

    [RelayCommand]
    public async Task LoadLocations()
    {
        IsLoading = true;
        Locations.Clear();
        var locations = await _locationService.GetAllLocationsAsync();
        foreach (var loc in locations) Locations.Add(loc);
        IsLoading = false;
    }

    [RelayCommand]
    public async Task AddLocation()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return;

        var loc = new Location { Name = NewName, Type = NewType, Address = NewAddress };
        await _locationService.AddLocationAsync(loc);
        NewName = "";
        NewAddress = "";
        await LoadLocations();
    }

    partial void OnSelectedLocationChanged(Location? value)
    {
        if (value != null) _ = LoadStock(value.Id);
        else CurrentLocationStock.Clear();
    }

    private async Task LoadStock(int locationId)
    {
        CurrentLocationStock.Clear();
        var stocks = await _locationService.GetStockByLocationAsync(locationId);
        foreach (var s in stocks) CurrentLocationStock.Add(s);
    }
}
