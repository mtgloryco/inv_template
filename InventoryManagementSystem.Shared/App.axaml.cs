using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using InventoryManagementSystem.Infrastructure;
using InventoryManagementSystem.Services;
using InventoryManagementSystem.UI.ViewModels;
using InventoryManagementSystem.UI.Views;

namespace InventoryManagementSystem;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Shared Service Initialization
        var services = InitializeServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(
                    services.inventory, services.user, services.license, services.hardware,
                    services.analytics, services.receipt, services.language, services.update,
                    services.settings, services.supplier, services.purchaseOrder, services.salesOrder, services.forecasting,
                    services.expiry, services.location, services.returns, services.advancedAnalytics,
                    services.bundle, services.audit, services.reporting, services.cloudSync,
                    services.briefing, services.tax, services.account, services.journal, services.accountingReport,
                    services.manufacturing, services.payment, services.industryTemplateService,
                    services.customFieldService, services.customerService, services.barcodeService, services.agingReportService,
                    services.vatExportService, services.budgetReportService, services.currencyService, services.cycleCountService),
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel(
                    services.inventory, services.user, services.license, services.hardware,
                    services.analytics, services.receipt, services.language, services.update,
                    services.settings, services.supplier, services.purchaseOrder, services.salesOrder, services.forecasting,
                    services.expiry, services.location, services.returns, services.advancedAnalytics,
                    services.bundle, services.audit, services.reporting, services.cloudSync,
                    services.briefing, services.tax, services.account, services.journal, services.accountingReport,
                    services.manufacturing, services.payment, services.industryTemplateService,
                    services.customFieldService, services.customerService, services.barcodeService, services.agingReportService,
                    services.vatExportService, services.budgetReportService, services.currencyService, services.cycleCountService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private (
        InventoryService inventory, UserService user, LicenseService license, HardwareIdService hardware,
        AnalyticsService analytics, ReceiptService receipt, LanguageService language, UpdateService update,
        SettingsService settings, SupplierService supplier, PurchaseOrderService purchaseOrder, SalesOrderService salesOrder,
        ForecastingService forecasting, ExpiryService expiry, LocationService location, ReturnsService returns,
        AdvancedAnalyticsService advancedAnalytics, BundleService bundle, AuditService audit,
        ReportingService reporting, CloudSyncService cloudSync, DailyBriefingService briefing,
        TaxService tax, AccountService account, JournalService journal, AccountingReportService accountingReport,
        ManufacturingService manufacturing, PaymentService payment, IndustryTemplateService industryTemplateService,
        CustomFieldService customFieldService, CustomerService customerService,
        BarcodeService barcodeService, AgingReportService agingReportService,
        VatExportService vatExportService, BudgetReportService budgetReportService,
        CurrencyService currencyService, CycleCountService cycleCountService) InitializeServices()
    {
        // Initialize Database
        var dbService = new DatabaseService();
        var userService = new UserService(dbService);
        var hardwareService = new HardwareIdService();
        var cryptoService = new LicenseCryptoService();
        var licenseService = new LicenseService(dbService, hardwareService, cryptoService);
        var auditService = new AuditService(dbService);
        var inventoryService = new InventoryService(dbService, licenseService, auditService);
        var analyticsService = new AnalyticsService(dbService);
        var languageService = new LanguageService();
        var updateService = new UpdateService();
        var settingsService = new SettingsService();
        var receiptService = new ReceiptService(settingsService);
        var supplierService = new SupplierService(dbService);
        var purchaseOrderService = new PurchaseOrderService(dbService, inventoryService);
        var salesOrderService = new SalesOrderService(dbService, inventoryService);
        var forecastingService = new ForecastingService(dbService);
        var expiryService = new ExpiryService(dbService);
        
        var locationService = new LocationService(dbService);
        var returnsService = new ReturnsService(dbService, auditService);
        var advancedAnalyticsService = new AdvancedAnalyticsService(dbService);
        var bundleService = new BundleService(dbService);
        var reportingService = new ReportingService(dbService, settingsService);
        var cloudApiClient = new CloudSyncApiClient();
        var cloudSyncService = new CloudSyncService(dbService, cloudApiClient);
        var dailyBriefingService = new DailyBriefingService(dbService);
        var taxService = new TaxService(dbService);
        var accountService = new AccountService(dbService);
        var journalService = new JournalService(dbService);
        var accountingReportService = new AccountingReportService(dbService);
        var manufacturingService = new ManufacturingService(dbService);
        var paymentService = new PaymentService(dbService, auditService);
        var customFieldService = new CustomFieldService(dbService);
        var customerService = new CustomerService(dbService);
        var industryTemplateService = new IndustryTemplateService(dbService);
        var barcodeService = new BarcodeService(dbService);
        var agingReportService = new AgingReportService(dbService);
        var vatExportService = new VatExportService(dbService);
        var budgetReportService = new BudgetReportService(dbService);
        var currencyService = new CurrencyService(dbService);
        var cycleCountService = new CycleCountService(dbService);

        // Apply any previously-saved terminology overrides immediately so the UI reflects them from startup
        languageService.SetTerminologyOverrides(settingsService.CurrentSettings.TerminologyOverrides);

        // Initialize services on a background thread to prevent UI thread deadlock
        Task.Run(async () =>
        {
            await dbService.InitializeAsync(settingsService.CurrentSettings.CurrencySymbol);
            await userService.InitializeAsync();
            await licenseService.InitializeAsync();
        }).Wait();

        return (
            inventoryService, userService, licenseService, hardwareService,
            analyticsService, receiptService, languageService, updateService,
            settingsService, supplierService, purchaseOrderService, salesOrderService, forecastingService,
            expiryService, locationService, returnsService, advancedAnalyticsService,
            bundleService, auditService, reportingService, cloudSyncService, dailyBriefingService,
            taxService, accountService, journalService, accountingReportService, manufacturingService, paymentService,
            industryTemplateService, customFieldService, customerService, barcodeService, agingReportService,
            vatExportService, budgetReportService, currencyService, cycleCountService);
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}