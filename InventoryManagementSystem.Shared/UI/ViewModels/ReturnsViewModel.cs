using System;
using System.Collections.ObjectModel;
using System.IO;
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
    [ObservableProperty] private CreditNoteDisplayRow? _selectedCreditNote;
    [ObservableProperty] private DebitNoteDisplayRow? _selectedDebitNote;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<CustomerReturn> RecentReturns { get; } = new();
    public ObservableCollection<SupplierReturn> RecentSupplierReturns { get; } = new();
    public ObservableCollection<CreditNoteDisplayRow> CreditNotes { get; } = new();
    public ObservableCollection<DebitNoteDisplayRow> DebitNotes { get; } = new();

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

        RecentSupplierReturns.Clear();
        var supplierReturns = await _returnsService.GetSupplierReturnsAsync(DateTime.Now.AddDays(-30), DateTime.Now);
        foreach (var r in supplierReturns) RecentSupplierReturns.Add(r);

        CreditNotes.Clear();
        var creditRows = await _returnsService.GetCreditNoteDisplayRowsAsync();
        foreach (var row in creditRows) CreditNotes.Add(row);

        DebitNotes.Clear();
        var debitRows = await _returnsService.GetDebitNoteDisplayRowsAsync();
        foreach (var row in debitRows) DebitNotes.Add(row);

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
        StatusMessage = $"Return {ret.ReturnNumber} processed.";
        await LoadInitialData();
    }

    [RelayCommand]
    private async Task PrintCreditNote(CreditNoteDisplayRow? row)
    {
        row ??= SelectedCreditNote;
        if (row == null) return;

        var path = await WriteNoteFileAsync(
            row.DocumentNumber,
            $"Credit Note: {row.DocumentNumber}",
            $"Customer: {row.CustomerName}",
            $"Linked: {row.LinkedDocument}",
            $"Return: {row.LinkedReturnNumber}",
            $"Amount: {row.Amount:N2}",
            $"Date: {row.IssueDate:yyyy-MM-dd}",
            $"Reason: {row.Reason}",
            $"Status: {row.Status}",
            $"Created by: {row.CreatedBy}");

        StatusMessage = $"Saved credit note to {path}";
    }

    [RelayCommand]
    private async Task PrintDebitNote(DebitNoteDisplayRow? row)
    {
        row ??= SelectedDebitNote;
        if (row == null) return;

        var path = await WriteNoteFileAsync(
            row.DocumentNumber,
            $"Debit Note: {row.DocumentNumber}",
            $"Supplier: {row.SupplierName}",
            $"Linked: {row.LinkedDocument}",
            $"Return: {row.LinkedReturnNumber}",
            $"Amount: {row.Amount:N2}",
            $"Date: {row.IssueDate:yyyy-MM-dd}",
            $"Reason: {row.Reason}",
            $"Status: {row.Status}",
            $"Created by: {row.CreatedBy}");

        StatusMessage = $"Saved debit note to {path}";
    }

    private static async Task<string> WriteNoteFileAsync(string docNumber, params string[] lines)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "InventoryManagementSystem", "Notes");
        Directory.CreateDirectory(dir);
        var safeName = docNumber.Replace('/', '-').Replace('\\', '-');
        var path = Path.Combine(dir, $"{safeName}.txt");
        await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, lines));
        return path;
    }
}
