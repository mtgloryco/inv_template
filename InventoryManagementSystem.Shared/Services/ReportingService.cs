using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using System.Globalization;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class ReportingService
    {
        private readonly DatabaseService _databaseService;
        private readonly SettingsService _settingsService;

        public ReportingService(DatabaseService databaseService, SettingsService settingsService)
        {
            _databaseService = databaseService;
            _settingsService = settingsService;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<string> GeneratePurchaseOrderPdfAsync(int poId)
        {
            var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(poId);
            if (po == null) return string.Empty;

            var supplier = await _databaseService.Connection.FindAsync<Supplier>(po.SupplierId);
            var items = await _databaseService.Connection.Table<PurchaseOrderItem>()
                .Where(i => i.PurchaseOrderId == poId).ToListAsync();
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();

            var filename = $"PO_{po.PONumber}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IMS_Reports", "PurchaseOrders");
            var path = Path.Combine(outputFolder, filename);
            Directory.CreateDirectory(outputFolder);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("PURCHASE ORDER").FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"PO #: {po.PONumber}");
                            col.Item().Text($"Date: {po.OrderDate:d}");
                        });

                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(_settingsService.CurrentSettings.StoreName).AlignRight().Bold();
                            col.Item().Text(_settingsService.CurrentSettings.StoreAddress).AlignRight();
                        });
                    });

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Vendor").Underline().Bold();
                                if (supplier != null)
                                {
                                    c.Item().Text(supplier.Name);
                                    c.Item().Text(supplier.Address);
                                    c.Item().Text(supplier.Phone);
                                }
                            });
                        });

                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Item
                                columns.RelativeColumn(1); // Qty
                                columns.RelativeColumn(1); // Unit Cost
                                columns.RelativeColumn(1); // Total
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).PaddingVertical(5).Text("Item");
                                header.Cell().BorderBottom(1).PaddingVertical(5).AlignRight().Text("Qty");
                                header.Cell().BorderBottom(1).PaddingVertical(5).AlignRight().Text("Unit Cost");
                                header.Cell().BorderBottom(1).PaddingVertical(5).AlignRight().Text("Total");
                            });

                            foreach (var item in items)
                            {
                                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                                table.Cell().PaddingVertical(2).Text(product?.Name ?? $"Product ID: {item.ProductId}");
                                table.Cell().PaddingVertical(2).AlignRight().Text(item.QuantityOrdered.ToString());
                                table.Cell().PaddingVertical(2).AlignRight().Text($"{item.UnitCost:N2}");
                                table.Cell().PaddingVertical(2).AlignRight().Text($"{item.TotalCost:N2}");
                            }
                        });

                        col.Item().AlignRight().PaddingTop(10).Text($"Total Amount: {po.TotalAmount:C}").Bold().FontSize(14);
                    });

                    page.Footer().AlignCenter().Text(x => {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                    });
                });
            }).GeneratePdf(path);

            return path;
        }

        public async Task<string> ExportToCsvAsync<T>(IEnumerable<T> records, string reportName)
        {
            var filename = $"{reportName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IMS_Reports", "Exports");
            var path = Path.Combine(outputFolder, filename);
            Directory.CreateDirectory(outputFolder);

            using (var writer = new StreamWriter(path))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                await csv.WriteRecordsAsync(records);
            }

            return path;
        }
        
        // --- High-level report generation methods ---

        public async Task<string> GenerateStockValuationReportAsync()
        {
            var products = await _databaseService.Connection.Table<Product>().ToListAsync();
            var valuation = products.Select(p => new {
                p.Id, p.Name, p.SKU, p.StockQuantity, p.Cost,
                TotalValue = p.StockQuantity * p.Cost
            });
            return await ExportToCsvAsync(valuation, "StockValuation");
        }
    }
}
