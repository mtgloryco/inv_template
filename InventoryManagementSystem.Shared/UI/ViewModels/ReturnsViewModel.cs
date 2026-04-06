using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels;

public partial class ReturnsViewModel : ViewModelBase
{
    private readonly ReturnsService _returnsService;
    private readonly InventoryService _inventoryService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private int _quantity;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private string _condition = "Resaleable";
    [ObservableProperty] private decimal _refundAmount;

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<CustomerReturn> RecentReturns { get; } = new();

    public ReturnsViewModel(ReturnsService returnsService, InventoryService inventoryService)
    {
        _returnsService = returnsService;
        _inventoryService = inventoryService;
        _ = LoadInitialData();
    }

    public async Task LoadInitialData()
    {
        IsLoading = true;
        Products.Clear();
        var products = await _inventoryService.GetAllProductsAsync();
        foreach (var p in products) Products.Add(p);

        RecentReturns.Clear();
        var returns = await _returnsService.GetCustomerReturnsAsync(DateTime.Now.AddDays(-30), DateTime.Now);
        foreach (var r in returns) RecentReturns.Add(r);
        IsLoading = false;
    }

    [RelayCommand]
    public async Task ProcessReturn()
    {
        if (SelectedProduct == null || Quantity <= 0) return;

        var ret = new CustomerReturn
        {
            ProductId = SelectedProduct.Id,
            Quantity = Quantity,
            Reason = Reason,
            Condition = Condition,
            RefundAmount = RefundAmount,
            ProcessedByUsername = UserSession.CurrentUser?.Username ?? "System",
            ReturnDate = DateTime.Now,
            ReturnNumber = $"RET-{DateTime.Now:yyyyMMddHHmmss}"
        };

        await _returnsService.ProcessCustomerReturnAsync(ret);
        Quantity = 0;
        Reason = "";
        RefundAmount = 0;
        await LoadInitialData();
    }
}
