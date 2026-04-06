using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class ExpiryDashboardViewModel : ViewModelBase
    {
        private readonly ExpiryService _expiryService;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private int _daysAhead = 30;

        [ObservableProperty]
        private int _redCount;

        [ObservableProperty]
        private int _orangeCount;

        [ObservableProperty]
        private int _greenCount;

        [ObservableProperty]
        private ObservableCollection<ExpiryService.BatchExpiryInfo> _batches = new();

        [ObservableProperty]
        private string _recallReason = "Expired stock recall";

        [ObservableProperty]
        private DateTime _wasteFrom = DateTime.Now.AddDays(-30);

        [ObservableProperty]
        private DateTime _wasteTo = DateTime.Now;

        [ObservableProperty]
        private decimal _wasteTotalCost;

        [ObservableProperty]
        private ObservableCollection<ExpiryService.WasteReportRow> _wasteRows = new();

        [ObservableProperty]
        private bool _hasBatches;

        [ObservableProperty]
        private bool _hasWasteRows;

        [ObservableProperty]
        private bool _isBatchesEmpty;

        [ObservableProperty]
        private bool _isWasteRowsEmpty;

        public ExpiryDashboardViewModel(ExpiryService expiryService)
        {
            _expiryService = expiryService;
            LoadCommandCommand.Execute(null);
            LoadWasteReportCommandCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadCommand()
        {
            IsLoading = true;
            try
            {
                var expired = await _expiryService.GetExpiredProductsAsync();
                var expiring = await _expiryService.GetExpiringProductsAsync(DaysAhead);

                Batches = new ObservableCollection<ExpiryService.BatchExpiryInfo>(expired);
                foreach (var item in expiring)
                {
                    Batches.Add(item);
                }

                RedCount = expired.Count;
                OrangeCount = expiring.Count;

                var trafficCounts = await _expiryService.GetExpiryTrafficCountsAsync(DaysAhead);
                GreenCount = trafficCounts.GreenCount;

                HasBatches = Batches.Count > 0;
                IsBatchesEmpty = Batches.Count == 0;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RecallBatchAsync(ExpiryService.BatchExpiryInfo? batch)
        {
            if (batch == null) return;
            if (!batch.CanRecall) return;
            if (string.IsNullOrWhiteSpace(batch.Batch.BatchNumber)) return;

            await _expiryService.RecallBatchAsync(batch.Batch.BatchNumber, RecallReason);
            await LoadCommand();
            await LoadWasteReportCommand();
        }

        [RelayCommand]
        private async Task LoadWasteReportCommand()
        {
            var result = await _expiryService.GetWasteReportAsync(WasteFrom, WasteTo);
            WasteTotalCost = result.TotalWasteCost;
            WasteRows = new ObservableCollection<ExpiryService.WasteReportRow>(result.Rows);
            HasWasteRows = WasteRows.Count > 0;
            IsWasteRowsEmpty = WasteRows.Count == 0;
        }
    }
}

