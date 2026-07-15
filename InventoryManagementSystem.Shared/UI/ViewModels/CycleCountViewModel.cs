using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class CycleCountViewModel : ViewModelBase
    {
        private readonly CycleCountService _cycleCountService;
        private readonly LocationService _locationService;
        private readonly InventoryService _inventoryService;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private ObservableCollection<CycleCountListItem> _counts = new();
        [ObservableProperty] private CycleCountListItem? _selectedCount;
        [ObservableProperty] private ObservableCollection<CycleCountLineRow> _lines = new();
        [ObservableProperty] private ObservableCollection<Location> _locations = new();
        [ObservableProperty] private Location? _selectedLocation;
        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private Product? _selectedProductToAdd;

        public bool CanEditSelectedCount => SelectedCount?.CycleCount.Status == "Draft";

        public CycleCountViewModel(CycleCountService cycleCountService, LocationService locationService, InventoryService inventoryService)
        {
            _cycleCountService = cycleCountService;
            _locationService = locationService;
            _inventoryService = inventoryService;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var locations = await _locationService.GetAllLocationsAsync();
            Locations = new ObservableCollection<Location>(locations);
            SelectedLocation = locations.FirstOrDefault();
            await LoadCountsAsync();
        }

        [RelayCommand]
        private async Task LoadCounts()
        {
            await LoadCountsAsync();
        }

        private async Task LoadCountsAsync()
        {
            IsLoading = true;
            try
            {
                var list = await _cycleCountService.GetAllAsync();
                Counts = new ObservableCollection<CycleCountListItem>(list);
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSelectedCountChanged(CycleCountListItem? value)
        {
            OnPropertyChanged(nameof(CanEditSelectedCount));
            _ = LoadLinesAsync(value?.CycleCount.Id ?? 0);
        }

        private async Task LoadLinesAsync(int countId)
        {
            Lines.Clear();
            AvailableProducts.Clear();
            SelectedProductToAdd = null;

            if (countId <= 0) return;

            var lines = await _cycleCountService.GetLinesAsync(countId);
            var products = await _inventoryService.GetAllProductsAsync();
            var rows = lines.Select(l =>
            {
                var product = products.FirstOrDefault(p => p.Id == l.ProductId);
                return new CycleCountLineRow
                {
                    LineId = l.Id,
                    ProductName = product?.Name ?? $"Product #{l.ProductId}",
                    SystemQuantity = l.SystemQuantity,
                    CountedQuantity = l.CountedQuantity
                };
            });
            Lines = new ObservableCollection<CycleCountLineRow>(rows);

            var includedIds = lines.Select(l => l.ProductId).ToHashSet();
            var addable = products
                .Where(p => p.ProductType == "Good" && !includedIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .ToList();
            AvailableProducts = new ObservableCollection<Product>(addable);
            SelectedProductToAdd = addable.FirstOrDefault();
        }

        [RelayCommand]
        private async Task CreateDraft()
        {
            if (SelectedLocation == null)
            {
                StatusMessage = "Select a location first.";
                return;
            }

            var username = UserSession.CurrentUser?.Username ?? "System";
            var count = await _cycleCountService.CreateDraftAsync(SelectedLocation.Id, username);
            StatusMessage = $"Created draft cycle count {count.CountNumber}. All stockable products were added — edit Counted, then Save Counts, then Post Variances.";
            await LoadCountsAsync();
            SelectedCount = Counts.FirstOrDefault(c => c.CycleCount.Id == count.Id);
        }

        [RelayCommand]
        private async Task AddProductLine()
        {
            if (SelectedCount == null)
            {
                StatusMessage = "Select a draft cycle count first.";
                return;
            }

            if (SelectedProductToAdd == null)
            {
                StatusMessage = "Select a product to add.";
                return;
            }

            try
            {
                await _cycleCountService.AddProductLineAsync(SelectedCount.CycleCount.Id, SelectedProductToAdd.Id);
                StatusMessage = $"Added {SelectedProductToAdd.Name} to the count.";
                await LoadLinesAsync(SelectedCount.CycleCount.Id);
            }
            catch (System.Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        [RelayCommand]
        private async Task SaveLineCounts()
        {
            if (SelectedCount == null) return;
            if (SelectedCount.CycleCount.Status != "Draft")
            {
                StatusMessage = "Only draft cycle counts can be edited.";
                return;
            }

            foreach (var line in Lines)
            {
                await _cycleCountService.UpdateCountedQuantityAsync(line.LineId, line.CountedQuantity);
            }
            StatusMessage = "Physical counts saved.";
            await LoadCountsAsync();
        }

        [RelayCommand]
        private async Task PostVariances()
        {
            if (SelectedCount == null)
            {
                StatusMessage = "Select a cycle count first.";
                return;
            }

            if (SelectedCount.CycleCount.Status == "Posted")
            {
                StatusMessage = "This cycle count is already posted.";
                return;
            }

            var countId = SelectedCount.CycleCount.Id;
            var countNumber = SelectedCount.CycleCount.CountNumber;

            try
            {
                foreach (var line in Lines)
                {
                    await _cycleCountService.UpdateCountedQuantityAsync(line.LineId, line.CountedQuantity);
                }

                var username = UserSession.CurrentUser?.Username ?? "System";
                await _cycleCountService.PostVariancesAsync(countId, username);
                StatusMessage = $"Posted variances for {countNumber}. Stock levels were updated.";
                await LoadCountsAsync();
                SelectedCount = Counts.FirstOrDefault(c => c.CycleCount?.Id == countId);
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Could not post variances: {ex.Message}";
            }
        }
    }

    public partial class CycleCountLineRow : ObservableObject
    {
        public int LineId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int SystemQuantity { get; set; }

        [ObservableProperty]
        private int _countedQuantity;

        public int Variance => CountedQuantity - SystemQuantity;

        partial void OnCountedQuantityChanged(int value) => OnPropertyChanged(nameof(Variance));
    }
}
