using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class ReceiptService
    {
        public ReceiptService()
        {
            // QuestPDF Community License (Free for individuals and small businesses < $1M revenue)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public string GenerateReceiptPdf(string cashierName, IEnumerable<Domain.StockMovement> items, decimal totalAmount)
        {
            var date = DateTime.Now;
            var filename = $"Receipt_{date:yyyyMMdd_HHmmss}.pdf";
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IMS_Receipts", filename);

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5); // A5 is standard for receipts
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header()
                        .Text("INVENTORY SYSTEM")
                        .SemiBold().FontSize(16).AlignCenter();

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(x =>
                        {
                            x.Spacing(5);

                            x.Item().Text($"Date: {date:g}");
                            x.Item().Text($"Cashier: {cashierName}");
                            x.Item().Text($"Receipt #: {Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}");
                            
                            x.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                            x.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3); // Item
                                    columns.RelativeColumn(1); // Qty
                                    columns.RelativeColumn(1); // Price
                                    columns.RelativeColumn(1); // Total
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("Item").Bold();
                                    header.Cell().Text("Qty").Bold().AlignRight();
                                    header.Cell().Text("Price").Bold().AlignRight();
                                    header.Cell().Text("Total").Bold().AlignRight();
                                    
                                    header.Cell().ColumnSpan(4)
                                        .PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Black);
                                });

                                foreach (var item in items)
                                {
                                    // Hack: We are misusing StockMovement here slightly because we don't have a Sales Model yet.
                                    // In a real app, we'd pass a "SalesReceiptModel".
                                    // For now, we assume QuantityChanged is positive for display, and unit price is passed somehow or calculated.
                                    
                                    // Since StockMovement doesn't strictly have "Price" stored in a standard way for receipts (we just added it to the DB but maybe not the entity property we have access to easily here without a join),
                                    // We will rely on what is passed. 
                                    // ACTUALLY: The loop in POSViewModel creates StockMovements.
                                    // We'll trust the caller to pass something meaningful.
                                    
                                    // Let's make this method accept a custom minimal model to be safe.
                                }
                            });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                });
            })
            .GeneratePdf(path);

            return path;
        }

        // Overload to accept Cart Items directly from POS
        public string GenerateReceiptFromCart(string cashierName, IEnumerable<UI.ViewModels.CartItem> cartItems, decimal totalAmount, decimal amountPaid, decimal changeDue)
        {
            var date = DateTime.Now;
            var filename = $"Receipt_{date:yyyyMMdd_HHmmss}.pdf";
            var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IMS_Receipts");
            var path = Path.Combine(outputFolder, filename);

            Directory.CreateDirectory(outputFolder);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header()
                        .Column(col =>
                        {
                            col.Item().Text("IMS STORE").FontSize(18).SemiBold().AlignCenter();
                            col.Item().Text("Kigali, Rwanda").FontSize(10).AlignCenter().FontColor(Colors.Grey.Medium);
                        });

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(5);

                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text($"Date: {date:yyyy-MM-dd HH:mm}");
                                    c.Item().Text($"Cashier: {cashierName}");
                                    c.Item().Text($"Rec ID: {Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}");
                                });
                            });

                            column.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3); // Name
                                    columns.RelativeColumn(1); // Qty
                                    columns.RelativeColumn(1.5f); // Price
                                    columns.RelativeColumn(1.5f); // Total
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Item").Bold();
                                    header.Cell().Element(CellStyle).AlignRight().Text("Qty").Bold();
                                    header.Cell().Element(CellStyle).AlignRight().Text("Price").Bold();
                                    header.Cell().Element(CellStyle).AlignRight().Text("Total").Bold();

                                    static IContainer CellStyle(IContainer container)
                                    {
                                        return container.PaddingBottom(5).BorderBottom(1).BorderColor(Colors.Black);
                                    }
                                });

                                foreach (var item in cartItems)
                                {
                                    table.Cell().Element(CellStyle).Text(item.Product.Name);
                                    table.Cell().Element(CellStyle).AlignRight().Text(item.Quantity.ToString());
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.UnitPrice:N0}"); // N0 for simpler currency in receipts
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.Subtotal:N0}");

                                    static IContainer CellStyle(IContainer container)
                                    {
                                        return container.PaddingVertical(2).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);
                                    }
                                }
                            });

                            column.Item().PaddingTop(10).Column(c =>
                            {
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("TOTAL").FontSize(14).Bold();
                                    r.RelativeItem().AlignRight().Text($"{totalAmount:N0} RWF").FontSize(14).Bold();
                                });
                                
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Cash").FontSize(10);
                                    r.RelativeItem().AlignRight().Text($"{amountPaid:N0}");
                                });

                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Change").FontSize(10);
                                    r.RelativeItem().AlignRight().Text($"{changeDue:N0}");
                                });
                            });
                        });

                    page.Footer()
                        .Column(c =>
                        {
                            c.Item().AlignCenter().Text("Thank you for your business!").FontSize(12).Italic();
                            c.Item().AlignCenter().Text("Murakoze cyane!").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });
                });
            })
            .GeneratePdf(path);

            return path;
        }
    }
}
