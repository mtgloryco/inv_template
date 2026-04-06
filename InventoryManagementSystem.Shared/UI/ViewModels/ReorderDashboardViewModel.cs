using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class ReorderDashboardViewModel : ViewModelBase
    {
        private readonly ForecastingService _forecastingService;
        private readonly PurchaseOrderService _purchaseOrderService;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private ObservableCollection<ForecastingService.ReorderRecommendation> _recommendations = new();

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isRecommendationsEmpty = true;

        public ReorderDashboardViewModel(ForecastingService forecastingService, PurchaseOrderService purchaseOrderService)
        {
            _forecastingService = forecastingService;
            _purchaseOrderService = purchaseOrderService;
            LoadRecommendationsCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadRecommendations()
        {
            IsLoading = true;
            try
            {
                var list = await _forecastingService.GetReorderRecommendationsAsync();
                Recommendations = new ObservableCollection<ForecastingService.ReorderRecommendation>(list);
                IsRecommendationsEmpty = list.Count == 0;
                StatusMessage = list.Count == 0 ? "No items need reordering right now." : string.Empty;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CreatePurchaseOrderForRecommendation(ForecastingService.ReorderRecommendation? recommendation)
        {
            if (recommendation == null) return;
            if (recommendation.RecommendedOrderQuantity <= 0) return;

            var username = UserSession.CurrentUser?.Username ?? "System";

            var po = new PurchaseOrder
            {
                SupplierId = recommendation.Rule.PreferredSupplierId,
                Status = "Draft",
                Notes = $"Auto-created from reorder rules. Generated for {recommendation.Product.Name}",
                CreatedByUsername = username,
                ExpectedDeliveryDate = System.DateTime.Now.AddDays(recommendation.Rule.LeadTimeDays)
            };

            var items = new System.Collections.Generic.List<PurchaseOrderItem>
            {
                new PurchaseOrderItem
                {
                    ProductId = recommendation.Product.Id,
                    QuantityOrdered = recommendation.RecommendedOrderQuantity,
                    QuantityReceived = 0,
                    UnitCost = recommendation.Product.Cost
                }
            };

            await _purchaseOrderService.CreatePurchaseOrderAsync(po, items);

            StatusMessage = $"Created PO {po.PONumber} for {recommendation.Product.Name}.";
            await LoadRecommendations();
        }
    }
}

