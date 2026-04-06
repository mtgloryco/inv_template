using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class ForecastingViewModel : ViewModelBase
    {
        private readonly ForecastingService _forecastingService;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private ObservableCollection<ForecastingService.ForecastingRow> _rows = new();

        public ForecastingViewModel(ForecastingService forecastingService)
        {
            _forecastingService = forecastingService;
            LoadForecastDataCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadForecastData()
        {
            IsLoading = true;
            try
            {
                var snapshot = await _forecastingService.GetForecastingSnapshotAsync();
                Rows = new ObservableCollection<ForecastingService.ForecastingRow>(snapshot);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

