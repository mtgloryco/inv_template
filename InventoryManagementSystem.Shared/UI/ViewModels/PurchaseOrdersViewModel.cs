using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class PurchaseOrdersViewModel : ViewModelBase
    {
        private readonly PurchaseOrderService _purchaseOrderService;

        [ObservableProperty]
        private ObservableCollection<PurchaseOrderListItem> _purchaseOrders = new();

        [ObservableProperty]
        private PurchaseOrder? _selectedPurchaseOrder;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public PurchaseOrdersViewModel(PurchaseOrderService purchaseOrderService)
        {
            _purchaseOrderService = purchaseOrderService;
            LoadPurchaseOrdersCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadPurchaseOrders()
        {
            var list = await _purchaseOrderService.GetAllPurchaseOrdersAsync();
            PurchaseOrders = new ObservableCollection<PurchaseOrderListItem>(list);
        }

        [RelayCommand]
        private async Task CreateQuickDraft()
        {
            var po = new PurchaseOrder
            {
                SupplierId = 0,
                Status = "Draft",
                Notes = "Created from quick draft",
                CreatedByUsername = UserSession.CurrentUser?.Username ?? "System",
                ExpectedDeliveryDate = DateTime.Now.AddDays(7)
            };
            await _purchaseOrderService.CreatePurchaseOrderAsync(po, new System.Collections.Generic.List<PurchaseOrderItem>());
            StatusMessage = $"Created {po.PONumber}";
            await LoadPurchaseOrders();
        }

        [RelayCommand]
        private async Task ApprovePurchaseOrder(PurchaseOrderListItem item)
        {
            await _purchaseOrderService.ApprovePurchaseOrderAsync(item.PurchaseOrder.Id, UserSession.CurrentUser?.Username ?? "System");
            await LoadPurchaseOrders();
        }

        [RelayCommand]
        private async Task MarkAsShipped(PurchaseOrderListItem item)
        {
            await _purchaseOrderService.MarkAsShippedAsync(item.PurchaseOrder.Id);
            await LoadPurchaseOrders();
        }

        [RelayCommand]
        private async Task CancelPurchaseOrder(PurchaseOrderListItem item)
        {
            await _purchaseOrderService.CancelPurchaseOrderAsync(item.PurchaseOrder.Id);
            await LoadPurchaseOrders();
        }
    }
}
