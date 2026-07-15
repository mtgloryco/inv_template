using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class ProductHistoryEvent
    {
        public StockMovement Movement { get; set; } = new();
        public string Category { get; set; } = string.Empty;
        public string QuantityDisplay { get; set; } = string.Empty;
        public string DocumentReference { get; set; } = string.Empty;
        public int JournalLineCount { get; set; }
    }

    public class ProductHistoryService
    {
        private readonly DatabaseService _databaseService;

        public ProductHistoryService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<ProductHistoryEvent>> GetProductHistoryAsync(int productId, int limit = 200)
        {
            var movements = await _databaseService.Connection.Table<StockMovement>()
                .Where(m => !m.IsDeleted && m.ProductId == productId)
                .OrderByDescending(m => m.Date)
                .Take(limit)
                .ToListAsync();

            await EnrichBatchTraceAsync(movements);

            var events = new List<ProductHistoryEvent>();
            foreach (var movement in movements)
            {
                var journals = await GetJournalLinesForMovementAsync(movement);
                events.Add(new ProductHistoryEvent
                {
                    Movement = movement,
                    Category = ClassifyCategory(movement),
                    QuantityDisplay = FormatQuantity(movement),
                    DocumentReference = ExtractDocumentReference(movement.Reason),
                    JournalLineCount = journals.Count
                });
            }

            return events;
        }

        public async Task<List<JournalEntryDetailRow>> GetJournalLinesForMovementAsync(StockMovement movement)
        {
            var entries = await _databaseService.Connection.Table<JournalEntry>().ToListAsync();
            var lines = await _databaseService.Connection.Table<JournalLine>().ToListAsync();
            var accounts = await _databaseService.Connection.Table<Account>().ToListAsync();
            var customers = await _databaseService.Connection.Table<Customer>().ToListAsync();
            var suppliers = await _databaseService.Connection.Table<Supplier>().ToListAsync();
            var salesOrders = await _databaseService.Connection.Table<SalesOrder>().ToListAsync();
            var purchaseOrders = await _databaseService.Connection.Table<PurchaseOrder>().ToListAsync();

            var searchTerms = BuildSearchTerms(movement);
            var matchingEntryIds = new HashSet<int>();

            foreach (var entry in entries)
            {
                if (searchTerms.Any(term =>
                        entry.Reference.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    matchingEntryIds.Add(entry.Id);
                }
            }

            foreach (var line in lines.Where(l => l.ProductId == movement.ProductId))
            {
                var entry = entries.FirstOrDefault(e => e.Id == line.JournalEntryId);
                if (entry == null) continue;

                var closeInTime = Math.Abs((entry.Date - movement.Date).TotalHours) <= 48;
                var labelMatches = searchTerms.Any(term =>
                    line.Label.Contains(term, StringComparison.OrdinalIgnoreCase));

                if (closeInTime && (labelMatches || searchTerms.Any(term =>
                        entry.Reference.Contains(term, StringComparison.OrdinalIgnoreCase))))
                {
                    matchingEntryIds.Add(entry.Id);
                }
            }

            var result = new List<JournalEntryDetailRow>();
            foreach (var line in lines.Where(l => matchingEntryIds.Contains(l.JournalEntryId)))
            {
                var entry = entries.FirstOrDefault(e => e.Id == line.JournalEntryId);
                var account = accounts.FirstOrDefault(a => a.Id == line.AccountId);
                var partnerName = ResolvePartnerName(entry?.Reference ?? string.Empty, customers, suppliers, salesOrders, purchaseOrders);

                result.Add(new JournalEntryDetailRow
                {
                    Date = entry?.Date ?? movement.Date,
                    EntryNumber = entry?.EntryNumber ?? "N/A",
                    AccountDisplay = account != null ? $"{account.Code} {account.Name}" : $"Account #{line.AccountId}",
                    PartnerName = partnerName,
                    Label = line.Label,
                    Debit = line.Debit,
                    Credit = line.Credit,
                    Matching = entry?.Reference ?? "-"
                });
            }

            return result.OrderByDescending(r => r.Date).ThenBy(r => r.EntryNumber).ToList();
        }

        private async Task EnrichBatchTraceAsync(List<StockMovement> movements)
        {
            foreach (var m in movements.Where(m => m.MovementType == "OUT"))
            {
                var usages = await _databaseService.Connection.Table<SaleBatchUsage>()
                    .Where(u => u.StockMovementId == m.Id)
                    .ToListAsync();

                if (usages.Count == 0) continue;

                var totalCost = usages.Sum(u => u.QuantityUsed * u.CostPerUnit);
                var totalQty = usages.Sum(u => u.QuantityUsed);
                var avgCost = totalQty > 0 ? totalCost / totalQty : 0;
                var profit = (m.UnitPrice * m.QuantityChanged) - totalCost;
                m.BatchTraceInfo = $"Qty: {m.QuantityChanged} | Avg Cost: {avgCost:N2} | Profit: {profit:N2}";
            }
        }

        private static List<string> BuildSearchTerms(StockMovement movement)
        {
            var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(movement.Reason))
            {
                terms.Add(movement.Reason.Trim());
                terms.Add($"Adjustment: {movement.Reason.Trim()}");

                foreach (Match match in Regex.Matches(movement.Reason, @"(SO|PO|CC|MO|POS|PAY|RET)[-A-Z0-9/]+", RegexOptions.IgnoreCase))
                {
                    terms.Add(match.Value);
                }

                if (movement.Reason.Contains("PO Receipt:", StringComparison.OrdinalIgnoreCase))
                {
                    terms.Add(movement.Reason.Replace("PO Receipt:", "PO Receipt:", StringComparison.OrdinalIgnoreCase).Trim());
                }

                if (movement.Reason.Contains("POS Sale:", StringComparison.OrdinalIgnoreCase))
                {
                    var soNumber = movement.Reason.Split(':').LastOrDefault()?.Trim();
                    if (!string.IsNullOrWhiteSpace(soNumber))
                    {
                        terms.Add(soNumber);
                        terms.Add($"POS Invoice: {soNumber}");
                        terms.Add($"POS Payment: {soNumber}");
                    }
                }

                if (movement.Reason.Contains("Cycle Count", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (Match match in Regex.Matches(movement.Reason, @"CC[-A-Z0-9]+", RegexOptions.IgnoreCase))
                    {
                        terms.Add(match.Value);
                        terms.Add($"Cycle Count: {match.Value}");
                    }
                }
            }

            return terms.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        }

        private static string ClassifyCategory(StockMovement movement)
        {
            var reason = movement.Reason ?? string.Empty;

            if (reason.Contains("POS Sale", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("Sales Order", StringComparison.OrdinalIgnoreCase))
            {
                return "Sale";
            }

            if (reason.Contains("PO Receipt", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("Purchase", StringComparison.OrdinalIgnoreCase))
            {
                return "Purchase";
            }

            if (reason.Contains("Customer Return", StringComparison.OrdinalIgnoreCase))
            {
                return "Customer Return";
            }

            if (reason.Contains("Supplier Return", StringComparison.OrdinalIgnoreCase))
            {
                return "Supplier Return";
            }

            if (reason.Contains("Manufacturing", StringComparison.OrdinalIgnoreCase))
            {
                return reason.Contains("Production", StringComparison.OrdinalIgnoreCase)
                    ? "Manufacturing (Output)"
                    : "Manufacturing (Consumption)";
            }

            if (reason.Contains("Cycle Count", StringComparison.OrdinalIgnoreCase))
            {
                return "Cycle Count";
            }

            if (movement.MovementType == "ADJUST" ||
                reason.Contains("Adjustment", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("Stock Adj", StringComparison.OrdinalIgnoreCase))
            {
                return "Adjustment";
            }

            return movement.MovementType switch
            {
                "IN" => "Stock In",
                "OUT" => "Stock Out",
                _ => "Other"
            };
        }

        private static string FormatQuantity(StockMovement movement)
        {
            var sign = movement.MovementType switch
            {
                "IN" => "+",
                "OUT" => "-",
                "ADJUST" when movement.QuantityChanged > 0 => "+",
                "ADJUST" when movement.QuantityChanged < 0 => "",
                _ => ""
            };

            return $"{sign}{Math.Abs(movement.QuantityChanged)} ({movement.MovementType})";
        }

        private static string ExtractDocumentReference(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return "—";

            var match = Regex.Match(reason, @"(SO|PO|CC|MO|PAY|RET)-[A-Z0-9/-]+", RegexOptions.IgnoreCase);
            if (match.Success) return match.Value.ToUpperInvariant();

            match = Regex.Match(reason, @"POS[-A-Z0-9/]+", RegexOptions.IgnoreCase);
            if (match.Success) return match.Value.ToUpperInvariant();

            return reason.Length > 48 ? reason[..48] + "…" : reason;
        }

        private static string ResolvePartnerName(
            string reference,
            List<Customer> customers,
            List<Supplier> suppliers,
            List<SalesOrder> salesOrders,
            List<PurchaseOrder> purchaseOrders)
        {
            if (string.IsNullOrWhiteSpace(reference)) return "-";

            if (reference.StartsWith("POS Invoice:", StringComparison.OrdinalIgnoreCase) ||
                reference.StartsWith("POS Payment:", StringComparison.OrdinalIgnoreCase) ||
                reference.StartsWith("Invoice ", StringComparison.OrdinalIgnoreCase))
            {
                var docNumber = reference.Contains(':')
                    ? reference.Split(':').Last().Trim()
                    : reference.Split(' ').Last().Trim();
                var so = salesOrders.FirstOrDefault(s =>
                    string.Equals(s.SONumber, docNumber, StringComparison.OrdinalIgnoreCase));
                if (so != null)
                {
                    return customers.FirstOrDefault(c => c.Id == so.CustomerId)?.Name ?? "Customer";
                }
            }

            if (reference.StartsWith("PO Receipt:", StringComparison.OrdinalIgnoreCase))
            {
                var poNumber = reference["PO Receipt:".Length..].Trim();
                var po = purchaseOrders.FirstOrDefault(p =>
                    string.Equals(p.PONumber, poNumber, StringComparison.OrdinalIgnoreCase));
                if (po != null)
                {
                    return suppliers.FirstOrDefault(s => s.Id == po.SupplierId)?.Name ?? "Supplier";
                }
            }

            return "-";
        }
    }
}
