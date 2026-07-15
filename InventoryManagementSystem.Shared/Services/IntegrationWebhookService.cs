using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class IntegrationWebhookService
    {
        private readonly DatabaseService _databaseService;
        private readonly AuditService? _auditService;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

        public IntegrationWebhookService(DatabaseService databaseService, AuditService? auditService = null, HttpClient? httpClient = null)
        {
            _databaseService = databaseService;
            _auditService = auditService;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        public async Task<List<WebhookEndpoint>> GetEndpointsAsync()
        {
            return await _databaseService.Connection.Table<WebhookEndpoint>()
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();
        }

        public async Task<WebhookEndpoint> RegisterEndpointAsync(WebhookEndpoint endpoint, string username)
        {
            endpoint.CreatedAt = DateTime.UtcNow;
            await _databaseService.Connection.InsertAsync(endpoint);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "Create", "WebhookEndpoint", endpoint.Id, endpoint);
            }

            return endpoint;
        }

        public async Task UpdateEndpointAsync(WebhookEndpoint endpoint, string username)
        {
            var old = await _databaseService.Connection.FindAsync<WebhookEndpoint>(endpoint.Id);
            await _databaseService.Connection.UpdateAsync(endpoint);

            if (_auditService != null && old != null)
            {
                await _auditService.LogActionAsync(username, "Update", "WebhookEndpoint", endpoint.Id, endpoint, old);
            }
        }

        public async Task<int> DispatchEventAsync(string eventType, object payload)
        {
            var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
            var endpoints = await _databaseService.Connection.Table<WebhookEndpoint>()
                .Where(w => w.IsActive)
                .ToListAsync();

            var matching = endpoints.Where(w =>
                string.IsNullOrWhiteSpace(w.EventTypes) ||
                w.EventTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(e => e.Equals(eventType, StringComparison.OrdinalIgnoreCase) || e == "*"))
                .ToList();

            var delivered = 0;
            foreach (var endpoint in matching)
            {
                var success = await DeliverToEndpointAsync(endpoint, eventType, payloadJson);
                if (success) delivered++;
            }

            await WriteLocalOutboxAsync(eventType, payloadJson);
            return delivered;
        }

        public async Task<string> ExportJournalEntriesForQuickBooksAsync(DateTime from, DateTime to)
        {
            var lines = await GetJournalExportLinesAsync(from, to);
            var sb = new StringBuilder();
            sb.AppendLine("Date,Account,Debit,Credit,Memo,Name");
            foreach (var line in lines)
            {
                sb.AppendLine($"{line.Date:yyyy-MM-dd},{EscapeCsv(line.AccountCode + " " + line.AccountName)},{line.Debit:F2},{line.Credit:F2},{EscapeCsv(line.Label)},{EscapeCsv(line.Reference)}");
            }

            return sb.ToString();
        }

        public async Task<string> ExportJournalEntriesForXeroAsync(DateTime from, DateTime to)
        {
            var lines = await GetJournalExportLinesAsync(from, to);
            var sb = new StringBuilder();
            sb.AppendLine("*JournalDate,*AccountCode,Description,*Debit,*Credit,Reference");
            foreach (var line in lines)
            {
                sb.AppendLine($"{line.Date:dd/MM/yyyy},{EscapeCsv(line.AccountCode)},{EscapeCsv(line.Label)},{line.Debit:F2},{line.Credit:F2},{EscapeCsv(line.Reference)}");
            }

            return sb.ToString();
        }

        private async Task<List<JournalExportLine>> GetJournalExportLinesAsync(DateTime from, DateTime to)
        {
            var entries = await _databaseService.Connection.Table<JournalEntry>()
                .Where(e => e.Date >= from && e.Date <= to && e.State == "Posted")
                .ToListAsync();
            var accounts = await _databaseService.Connection.Table<Account>().ToListAsync();
            var accountMap = accounts.ToDictionary(a => a.Id);

            var result = new List<JournalExportLine>();
            foreach (var entry in entries)
            {
                var journalLines = await _databaseService.Connection.Table<JournalLine>()
                    .Where(l => l.JournalEntryId == entry.Id)
                    .ToListAsync();

                foreach (var jl in journalLines)
                {
                    accountMap.TryGetValue(jl.AccountId, out var account);
                    result.Add(new JournalExportLine
                    {
                        Date = entry.Date,
                        Reference = entry.Reference,
                        Label = jl.Label,
                        AccountCode = account?.Code ?? jl.AccountId.ToString(),
                        AccountName = account?.Name ?? string.Empty,
                        Debit = jl.Debit,
                        Credit = jl.Credit
                    });
                }
            }

            return result.OrderBy(l => l.Date).ThenBy(l => l.Reference).ToList();
        }

        private async Task<bool> DeliverToEndpointAsync(WebhookEndpoint endpoint, string eventType, string payloadJson)
        {
            var log = new WebhookDeliveryLog
            {
                WebhookEndpointId = endpoint.Id,
                EventType = eventType,
                PayloadJson = payloadJson,
                DeliveredAt = DateTime.UtcNow
            };

            if (string.IsNullOrWhiteSpace(endpoint.TargetUrl))
            {
                log.Success = false;
                log.ResponseBody = "Missing target URL";
                await _databaseService.Connection.InsertAsync(log);
                return false;
            }

            try
            {
                using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                if (!string.IsNullOrWhiteSpace(endpoint.Secret))
                {
                    content.Headers.Add("X-Webhook-Secret", endpoint.Secret);
                }

                content.Headers.Add("X-Event-Type", eventType);
                var response = await _httpClient.PostAsync(endpoint.TargetUrl, content);
                log.HttpStatusCode = (int)response.StatusCode;
                log.ResponseBody = await response.Content.ReadAsStringAsync();
                log.Success = response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                log.Success = false;
                log.ResponseBody = ex.Message;
            }

            await _databaseService.Connection.InsertAsync(log);
            return log.Success;
        }

        private async Task WriteLocalOutboxAsync(string eventType, string payloadJson)
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "InventoryManagementSystem",
                "Webhooks");
            Directory.CreateDirectory(folder);

            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{eventType.Replace('.', '_')}.json";
            var path = Path.Combine(folder, fileName);
            await File.WriteAllTextAsync(path, payloadJson);
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private class JournalExportLine
        {
            public DateTime Date { get; set; }
            public string Reference { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public string AccountCode { get; set; } = string.Empty;
            public string AccountName { get; set; } = string.Empty;
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
        }
    }
}
