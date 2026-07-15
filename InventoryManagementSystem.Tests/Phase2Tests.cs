using System;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using InventoryManagementSystem.Services;
using Xunit;

namespace InventoryManagementSystem.Tests;

public class Phase2Tests : IAsyncLifetime
{
    private readonly string _dbPath = TempFile.CreateDbPath();
    private DatabaseService _db = null!;
    private PaymentService _paymentService = null!;
    private VatExportService _vatExportService = null!;
    private CurrencyService _currencyService = null!;
    private BudgetReportService _budgetReportService = null!;

    public async Task InitializeAsync()
    {
        _db = new DatabaseService(_dbPath);
        await _db.InitializeAsync();
        _paymentService = new PaymentService(_db);
        _vatExportService = new VatExportService(_db);
        _currencyService = new CurrencyService(_db);
        _budgetReportService = new BudgetReportService(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.CloseConnectionAsync();
        TempFile.DeleteDbFiles(_dbPath);
    }

    [Fact]
    public async Task PaymentService_TracksOpenBalanceAfterPartialPayment()
    {
        var so = new SalesOrder
        {
            SONumber = "SO-PAY-1",
            CustomerId = 1,
            Status = "Delivered",
            BillingStatus = "Invoiced",
            OrderDate = DateTime.Today,
            TotalAmount = 1000,
            Currency = "RWF"
        };
        await _db.Connection.InsertAsync(so);

        await _paymentService.RecordInvoicePaymentAsync("SalesOrder", so.Id, 400, "Bank", "tester", reference: "TRF-001");

        var paid = await _paymentService.GetAmountPaidAsync("SalesOrder", so.Id);
        var open = await _paymentService.GetOpenBalanceAsync("SalesOrder", so.Id);

        Assert.Equal(400, paid);
        Assert.Equal(600, open);
    }

    [Fact]
    public async Task VatExportService_ComputesOutputAndInputVat()
    {
        var vatPayable = await _db.Connection.Table<Account>().Where(a => a.Code == "220000").FirstAsync();
        var vatReceivable = await _db.Connection.Table<Account>().Where(a => a.Code == "125000").FirstAsync();
        var journal = await _db.Connection.Table<Journal>().FirstAsync();

        var entry = new JournalEntry
        {
            EntryNumber = "TEST/VAT/001",
            JournalId = journal.Id,
            Date = DateTime.Today,
            Reference = "VAT test",
            State = "Posted"
        };
        await _db.Connection.InsertAsync(entry);

        await _db.Connection.InsertAsync(new JournalLine
        {
            JournalEntryId = entry.Id,
            AccountId = vatPayable.Id,
            Label = "Output VAT",
            Debit = 0,
            Credit = 180
        });
        await _db.Connection.InsertAsync(new JournalLine
        {
            JournalEntryId = entry.Id,
            AccountId = vatReceivable.Id,
            Label = "Input VAT",
            Debit = 50,
            Credit = 0
        });

        await _db.Connection.InsertAsync(new SalesOrder
        {
            SONumber = "SO-VAT-1",
            CustomerId = 1,
            Status = "Delivered",
            BillingStatus = "Invoiced",
            OrderDate = DateTime.Today,
            TotalAmount = 1180
        });

        var summary = await _vatExportService.ComputeVatReturnAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        Assert.Equal(180, summary.OutputVat);
        Assert.Equal(50, summary.InputVat);
        Assert.Equal(130, summary.NetVatPayable);
        Assert.Contains("Output VAT", _vatExportService.BuildVatReturnCsv(summary));
    }

    [Fact]
    public async Task CurrencyService_ConvertsUsingStoredRate()
    {
        await _currencyService.SaveRateAsync(new ExchangeRate
        {
            FromCurrency = "USD",
            ToCurrency = "RWF",
            Rate = 1300m,
            EffectiveDate = DateTime.Today
        });

        var converted = await _currencyService.ConvertAsync(10, "USD", "RWF");
        Assert.Equal(13000, converted);
    }

    [Fact]
    public async Task BudgetReportService_ComputesVarianceAgainstActual()
    {
        var expenseAccount = await _db.Connection.Table<Account>()
            .Where(a => a.Type.StartsWith("Expense"))
            .FirstAsync();
        var journal = await _db.Connection.Table<Journal>().FirstAsync();

        await _budgetReportService.SaveBudgetLineAsync(new BudgetLine
        {
            FiscalYear = DateTime.Today.Year,
            AccountId = expenseAccount.Id,
            PeriodMonth = 0,
            BudgetAmount = 10000
        });

        var entry = new JournalEntry
        {
            EntryNumber = "TEST/BUD/001",
            JournalId = journal.Id,
            Date = new DateTime(DateTime.Today.Year, 6, 15),
            Reference = "Expense posting",
            State = "Posted"
        };
        await _db.Connection.InsertAsync(entry);
        await _db.Connection.InsertAsync(new JournalLine
        {
            JournalEntryId = entry.Id,
            AccountId = expenseAccount.Id,
            Label = "Test expense",
            Debit = 7500,
            Credit = 0
        });

        var lines = await _budgetReportService.GetBudgetVsActualAsync(DateTime.Today.Year);
        var line = Assert.Single(lines, l => l.AccountCode == expenseAccount.Code);

        Assert.Equal(10000, line.BudgetAmount);
        Assert.Equal(7500, line.ActualAmount);
        Assert.Equal(-2500, line.Variance);
    }
}
