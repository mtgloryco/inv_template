using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InventoryManagementSystem.Domain;

namespace InventoryManagementSystem.Services
{
    public class PurchaseOrderPdfService
    {
        private readonly SettingsService _settingsService;

        public PurchaseOrderPdfService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            // Set QuestPDF Community license
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public string GeneratePurchaseOrderPdf(
            PurchaseOrder po, 
            List<PurchaseOrderItem> items, 
            List<Product> allProducts, 
            List<Tax> allTaxes, 
            Supplier? supplier,
            bool asBill = false)
        {
            var dateStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var cleanRef = po.PONumber.Replace("-", "_").Replace(" ", "_");
            var docTypePrefix = asBill ? "BILL" : (po.Status == "Draft" ? "RFQ" : "PO");
            var filename = $"{docTypePrefix}_{cleanRef}_{dateStr}.pdf";
            
            var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IMS_Purchases");
            var path = Path.Combine(outputFolder, filename);

            // Ensure directory exists
            Directory.CreateDirectory(outputFolder);

            var companyName = !string.IsNullOrWhiteSpace(po.Company) ? po.Company : _settingsService.CurrentSettings.StoreName;
            var companyAddress = _settingsService.CurrentSettings.StoreAddress;
            var currency = po.Currency ?? _settingsService.CurrentSettings.CurrencySymbol;

            // To track tax details for breakdown
            var taxTotals = new Dictionary<int, (Tax Tax, decimal Amount)>();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    // Header part
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            // Left part: Company Title & Details
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(companyName).FontSize(18).Bold().FontColor(Colors.Blue.Darken4);
                                
                                // Mock some details to match the professional design if it is Terrassa
                                if (companyName.Contains("TERRASSA", StringComparison.OrdinalIgnoreCase))
                                {
                                    c.Item().Text("TIN: 102684863").FontSize(9).FontColor(Colors.Grey.Darken2);
                                    c.Item().Text("Gishushu, Kacyiru, Gasabo").FontSize(9).FontColor(Colors.Grey.Darken2);
                                    c.Item().Text("P.O. Box: 4431").FontSize(9).FontColor(Colors.Grey.Darken2);
                                    c.Item().Text("Phone: +250 785 823 214").FontSize(9).FontColor(Colors.Grey.Darken2);
                                    c.Item().Text("E-mail: operations-manager@terrassawines.com").FontSize(9).FontColor(Colors.Grey.Darken2);
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(companyAddress))
                                    {
                                        c.Item().Text(companyAddress).FontSize(9).FontColor(Colors.Grey.Darken2);
                                    }
                                    if (!string.IsNullOrEmpty(po.Buyer))
                                    {
                                        c.Item().Text($"Buyer: {po.Buyer}").FontSize(9).FontColor(Colors.Grey.Darken2);
                                    }
                                }
                            });

                            // Right part: Document Title & Reference
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                var docTitle = po.Status == "Draft" ? "Request for Quotation" : (asBill ? "Vendor Bill" : "Purchase Order");
                                c.Item().Text(docTitle).FontSize(24).Bold().FontColor(Colors.Grey.Darken4);
                                c.Item().Text($"# {po.PONumber}").FontSize(14).SemiBold().FontColor(Colors.Blue.Darken2);
                                
                                c.Item().PaddingTop(10).Column(details =>
                                {
                                    details.Spacing(2);
                                    details.Item().Text($"Date: {po.OrderDate:yyyy-MM-dd}").FontSize(9);
                                    details.Item().Text($"Terms: {po.PaymentTerms}").FontSize(9);
                                    details.Item().Text($"Order Deadline: {po.OrderDeadline:yyyy-MM-dd}").FontSize(9);
                                    if (po.ExpectedDeliveryDate.HasValue)
                                    {
                                        details.Item().Text($"Expected Arrival: {po.ExpectedDeliveryDate.Value:yyyy-MM-dd}").FontSize(9);
                                    }
                                });
                            });
                        });

                        col.Item().PaddingTop(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    // Content part
                    page.Content().Column(col =>
                    {
                        col.Spacing(15);

                        // Vendor Details / Bill To
                        col.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Vendor").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                                if (supplier != null)
                                {
                                    c.Item().Text(supplier.Name).FontSize(12).Bold();
                                    if (!string.IsNullOrWhiteSpace(supplier.Phone))
                                        c.Item().Text($"Phone: {supplier.Phone}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    if (!string.IsNullOrWhiteSpace(supplier.Email))
                                        c.Item().Text($"Email: {supplier.Email}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    c.Item().Text("Unspecified Vendor").FontSize(11).Italic();
                                }
                            });
                        });

                        // Items Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);  // #
                                columns.RelativeColumn(4);   // Item & Description
                                columns.RelativeColumn(1.2f); // Qty
                                columns.RelativeColumn(1.8f); // Rate
                                columns.RelativeColumn(1.8f); // Tax
                                columns.RelativeColumn(2);   // Amount
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderStyle).Text("#").Bold().FontColor(Colors.White);
                                header.Cell().Element(HeaderStyle).Text("Item & Description").Bold().FontColor(Colors.White);
                                header.Cell().Element(HeaderStyle).AlignRight().Text(asBill ? "Billed Qty" : "Qty").Bold().FontColor(Colors.White);
                                header.Cell().Element(HeaderStyle).AlignRight().Text("Rate").Bold().FontColor(Colors.White);
                                header.Cell().Element(HeaderStyle).AlignRight().Text("Tax").Bold().FontColor(Colors.White);
                                header.Cell().Element(HeaderStyle).AlignRight().Text("Amount").Bold().FontColor(Colors.White);

                                static IContainer HeaderStyle(IContainer container)
                                {
                                    return container.Background("#37474F").Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Darken3);
                                }
                            });

                            int index = 1;
                            decimal subTotalBeforeTax = 0;
                            
                            // taxTotals is declared in parent scope

                            foreach (var it in items)
                            {
                                var product = allProducts.FirstOrDefault(p => p.Id == it.ProductId);
                                var productName = product != null ? product.Name : $"Product ID: {it.ProductId}";
                                
                                var unitCost = it.UnitCost;
                                var quantity = asBill ? it.QuantityBilled : it.QuantityOrdered;
                                var rowSubtotal = quantity * unitCost;

                                // Tax calculation
                                var tax = allTaxes.FirstOrDefault(t => t.Id == it.TaxId);
                                string taxLabel = "None";
                                decimal rowTaxAmount = 0;
                                decimal itemFinalSubtotal = rowSubtotal;

                                if (tax != null)
                                {
                                    taxLabel = $"{tax.Name} ({tax.Amount}%)";
                                    
                                    // Tax calculation logic
                                    if (tax.IncludedInPrice == "Include")
                                    {
                                        taxLabel += " Incl.";
                                        decimal basePrice = 0;
                                        if (tax.Computation == "Percentage")
                                        {
                                            basePrice = rowSubtotal / (1 + (tax.Amount / 100));
                                        }
                                        else
                                        {
                                            basePrice = Math.Max(0, rowSubtotal - (quantity * tax.Amount));
                                        }
                                        rowTaxAmount = rowSubtotal - basePrice;
                                        subTotalBeforeTax += basePrice;
                                        itemFinalSubtotal = rowSubtotal;
                                    }
                                    else
                                    {
                                        // Exclude
                                        subTotalBeforeTax += rowSubtotal;
                                        if (tax.Computation == "Percentage")
                                        {
                                            rowTaxAmount = rowSubtotal * (tax.Amount / 100);
                                        }
                                        else
                                        {
                                            rowTaxAmount = quantity * tax.Amount;
                                        }
                                        itemFinalSubtotal = rowSubtotal + rowTaxAmount;
                                    }

                                    if (rowTaxAmount > 0)
                                    {
                                        if (taxTotals.ContainsKey(tax.Id))
                                        {
                                            var existing = taxTotals[tax.Id];
                                            taxTotals[tax.Id] = (tax, existing.Amount + rowTaxAmount);
                                        }
                                        else
                                        {
                                            taxTotals[tax.Id] = (tax, rowTaxAmount);
                                        }
                                    }
                                }
                                else
                                {
                                    subTotalBeforeTax += rowSubtotal;
                                }

                                table.Cell().Element(CellStyle).Text(index.ToString());
                                table.Cell().Element(CellStyle).Text(productName);
                                table.Cell().Element(CellStyle).AlignRight().Text(quantity.ToString("N0"));
                                table.Cell().Element(CellStyle).AlignRight().Text($"{unitCost:N2}");
                                table.Cell().Element(CellStyle).AlignRight().Text(taxLabel).FontSize(8).FontColor(Colors.Grey.Darken1);
                                table.Cell().Element(CellStyle).AlignRight().Text($"{itemFinalSubtotal:N2}");

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.Padding(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
                                }

                                index++;
                            }
                        });

                        // Summary Section (Subtotal, Taxes, Total, Balance Due)
                        col.Item().AlignRight().Width(300).Column(summary =>
                        {
                            summary.Spacing(4);

                            // Calculate subtotal and final total
                            decimal finalTotal = items.Sum(it =>
                            {
                                var qty = asBill ? it.QuantityBilled : it.QuantityOrdered;
                                var rowSubtotal = qty * it.UnitCost;
                                var tax = allTaxes.FirstOrDefault(t => t.Id == it.TaxId);
                                if (tax != null && tax.IncludedInPrice == "Exclude")
                                {
                                    decimal taxAmount = 0;
                                    if (tax.Computation == "Percentage")
                                    {
                                        taxAmount = rowSubtotal * (tax.Amount / 100);
                                    }
                                    else
                                    {
                                        taxAmount = qty * tax.Amount;
                                    }
                                    return rowSubtotal + taxAmount;
                                }
                                return rowSubtotal;
                            });

                            // Calculate subtotal before taxes
                            decimal totalExclusiveTaxAmount = taxTotals.Values.Where(t => t.Tax.IncludedInPrice == "Exclude").Sum(t => t.Amount);
                            decimal computedSubtotal = finalTotal - totalExclusiveTaxAmount;

                            summary.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Sub Total").FontColor(Colors.Grey.Darken2);
                                r.RelativeItem().AlignRight().Text($"{computedSubtotal:N2}");
                            });

                            // Render each tax breakdown line
                            bool hasInclusiveTax = false;
                            foreach (var taxVal in taxTotals.Values)
                            {
                                var inclusiveIndicator = "";
                                if (taxVal.Tax.IncludedInPrice == "Include")
                                {
                                    inclusiveIndicator = " (Tax Inclusive)";
                                    hasInclusiveTax = true;
                                }
                                summary.Item().Row(r =>
                                {
                                    r.RelativeItem().Text($"{taxVal.Tax.Name} ({taxVal.Tax.Amount}%){inclusiveIndicator}").FontColor(Colors.Grey.Darken2);
                                    r.RelativeItem().AlignRight().Text($"{taxVal.Amount:N2}");
                                });
                            }

                            summary.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

                            // Total
                            summary.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Total").Bold().FontSize(12);
                                r.RelativeItem().AlignRight().Text($"{finalTotal:N2} {currency}").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                            });

                            // Balance Due Card (styled box)
                            summary.Item().PaddingTop(5).Background(Colors.Grey.Lighten4).Padding(8).Row(r =>
                            {
                                r.RelativeItem().Text("Balance Due").Bold().FontSize(11);
                                r.RelativeItem().AlignRight().Text($"{finalTotal:N2} {currency}").Bold().FontSize(11).FontColor(Colors.Grey.Darken4);
                            });
                        });

                        // Notes section
                        if (!string.IsNullOrWhiteSpace(po.Notes))
                        {
                            col.Item().PaddingTop(15).Column(notes =>
                            {
                                notes.Spacing(3);
                                notes.Item().Text("Notes").Bold().FontSize(9).FontColor(Colors.Grey.Darken3);
                                notes.Item().Text(po.Notes).FontSize(9).Italic().FontColor(Colors.Grey.Darken2);
                            });
                        }
                        else
                        {
                            col.Item().PaddingTop(15).Column(notes =>
                            {
                                notes.Spacing(3);
                                notes.Item().Text("Notes").Bold().FontSize(9).FontColor(Colors.Grey.Darken3);
                                notes.Item().Text("Thanks for your business.").FontSize(9).Italic().FontColor(Colors.Grey.Darken2);
                            });
                        }
                    });

                    // Footer part
                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Text("Generated by IMS Application").FontSize(8).FontColor(Colors.Grey.Darken1);
                            row.RelativeItem().AlignRight().Text(x =>
                            {
                                x.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                                x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                                x.Span(" of ").FontSize(8).FontColor(Colors.Grey.Darken1);
                                x.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                            });
                        });
                    });
                });
            })
            .GeneratePdf(path);

            return path;
        }
    }
}
