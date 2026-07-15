using System;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using InventoryManagementSystem.Services;
using Xunit;

namespace InventoryManagementSystem.Tests;

public class GapPolishTests : IAsyncLifetime
{
    private readonly string _dbPath = TempFile.CreateDbPath();
    private DatabaseService _db = null!;
    private PaymentService _paymentService = null!;
    private ReturnsService _returnsService = null!;
    private CurrencyService _currencyService = null!;

    public async Task InitializeAsync()
    {
        _db = new DatabaseService(_dbPath);
        await _db.InitializeAsync();
        _paymentService = new PaymentService(_db);
        _returnsService = new ReturnsService(_db, new AuditService(_db));
        _currencyService = new CurrencyService(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.CloseConnectionAsync();
        TempFile.DeleteDbFiles(_dbPath);
    }

    [Fact]
    public async Task PaymentService_TracksOpenBalanceForPurchaseOrder()
    {
        var po = new PurchaseOrder
        {
            PONumber = "PO-PAY-1",
            SupplierId = 1,
            Status = "Received",
            BillingStatus = "Billed",
            OrderDate = DateTime.Today,
            TotalAmount = 500,
            Currency = "USD"
        };
        await _db.Connection.InsertAsync(po);

        await _paymentService.RecordInvoicePaymentAsync("PurchaseOrder", po.Id, 200, "Bank", "tester", reference: "CHK-001");

        var paid = await _paymentService.GetAmountPaidAsync("PurchaseOrder", po.Id);
        var open = await _paymentService.GetOpenBalanceAsync("PurchaseOrder", po.Id);

        Assert.Equal(200, paid);
        Assert.Equal(300, open);
    }

    [Fact]
    public async Task ReturnsService_ListsCreditAndDebitNoteRows()
    {
        var customer = new Customer { Name = "Acme Corp" };
        var supplier = new Supplier { Name = "Global Supplies" };
        await _db.Connection.InsertAsync(customer);
        await _db.Connection.InsertAsync(supplier);

        await _db.Connection.InsertAsync(new CreditNote
        {
            CreditNoteNumber = "CN-TEST-001",
            CustomerId = customer.Id,
            Amount = 120,
            Reason = "Damaged goods",
            CreatedByUsername = "admin"
        });

        await _db.Connection.InsertAsync(new DebitNote
        {
            DebitNoteNumber = "DN-TEST-001",
            SupplierId = supplier.Id,
            Amount = 80,
            Reason = "Short shipment",
            CreatedByUsername = "admin"
        });

        var creditRows = await _returnsService.GetCreditNoteDisplayRowsAsync();
        var debitRows = await _returnsService.GetDebitNoteDisplayRowsAsync();

        var credit = creditRows.First(r => r.DocumentNumber == "CN-TEST-001");
        var debit = debitRows.First(r => r.DocumentNumber == "DN-TEST-001");

        Assert.Equal("Acme Corp", credit.CustomerName);
        Assert.Equal("Global Supplies", debit.SupplierName);
    }

    [Fact]
    public async Task CurrencyService_FormatsBaseEquivalentLabel()
    {
        await _currencyService.SaveRateAsync(new ExchangeRate
        {
            FromCurrency = "USD",
            ToCurrency = "RWF",
            Rate = 1300,
            EffectiveDate = DateTime.Today
        });

        var (_, label) = await _currencyService.TryFormatBaseEquivalentAsync(10, "USD", "RWF");

        Assert.Equal("≈ 13,000.00 RWF", label);
    }

    [Fact]
    public async Task CurrencyService_ConvertsFromBaseForPosCheckout()
    {
        await _currencyService.SaveRateAsync(new ExchangeRate
        {
            FromCurrency = "RWF",
            ToCurrency = "USD",
            Rate = 0.00077m,
            EffectiveDate = DateTime.Today
        });

        var (converted, _) = await _currencyService.TryFormatFromBaseAsync(100000, "USD", "RWF");

        Assert.NotNull(converted);
        Assert.Equal(77, converted.Value);
    }
}
