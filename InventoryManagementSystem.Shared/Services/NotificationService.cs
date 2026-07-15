using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class NotificationService
    {
        private readonly DatabaseService _databaseService;
        private readonly PaymentService _paymentService;
        private readonly AuditService? _auditService;

        public NotificationService(DatabaseService databaseService, PaymentService paymentService, AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _paymentService = paymentService;
            _auditService = auditService;
        }

        public async Task<NotificationOutbox> QueueInvoiceDeliveryAsync(int salesOrderId, string channel = "Email")
        {
            var order = await _databaseService.Connection.FindAsync<SalesOrder>(salesOrderId);
            if (order == null)
            {
                throw new InvalidOperationException($"Sales order {salesOrderId} not found.");
            }

            var customer = order.CustomerId > 0
                ? await _databaseService.Connection.FindAsync<Customer>(order.CustomerId)
                : null;

            var recipient = channel.Equals("SMS", StringComparison.OrdinalIgnoreCase)
                ? customer?.Phone ?? string.Empty
                : customer?.Email ?? string.Empty;

            if (string.IsNullOrWhiteSpace(recipient))
            {
                throw new InvalidOperationException("Customer has no contact details for the selected channel.");
            }

            var notification = new NotificationOutbox
            {
                Channel = channel,
                Recipient = recipient,
                Subject = $"Invoice {order.SONumber}",
                Body = BuildInvoiceBody(order, customer?.Name ?? "Customer"),
                ReferenceType = "SalesOrder",
                ReferenceId = salesOrderId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _databaseService.Connection.InsertAsync(notification);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    UserSession.CurrentUser?.Username ?? "System",
                    "QueueNotification",
                    "NotificationOutbox",
                    notification.Id,
                    notification);
            }

            return notification;
        }

        public async Task<List<NotificationOutbox>> QueuePaymentRemindersAsync(int overdueDays = 7)
        {
            var cutoff = DateTime.Today.AddDays(-overdueDays);
            var orders = await _databaseService.Connection.Table<SalesOrder>()
                .Where(so => so.BillingStatus == "Invoiced" && so.OrderDate <= cutoff)
                .ToListAsync();

            var queued = new List<NotificationOutbox>();
            foreach (var order in orders)
            {
                var open = await _paymentService.GetOpenBalanceAsync("SalesOrder", order.Id);
                if (open <= 0.01m) continue;

                var customer = order.CustomerId > 0
                    ? await _databaseService.Connection.FindAsync<Customer>(order.CustomerId)
                    : null;

                var recipient = customer?.Email ?? customer?.Phone ?? string.Empty;
                if (string.IsNullOrWhiteSpace(recipient)) continue;

                var channel = recipient.Contains('@') ? "Email" : "SMS";
                var notification = new NotificationOutbox
                {
                    Channel = channel,
                    Recipient = recipient,
                    Subject = $"Payment reminder: {order.SONumber}",
                    Body = $"Reminder: invoice {order.SONumber} has an open balance of {open:N2}. Please arrange payment.",
                    ReferenceType = "SalesOrder",
                    ReferenceId = order.Id,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                await _databaseService.Connection.InsertAsync(notification);
                queued.Add(notification);

                if (_auditService != null)
                {
                    await _auditService.LogActionAsync(
                        UserSession.CurrentUser?.Username ?? "System",
                        "QueueReminder",
                        "NotificationOutbox",
                        notification.Id,
                        notification);
                }
            }

            return queued;
        }

        public async Task<int> ProcessPendingNotificationsAsync()
        {
            var pending = await _databaseService.Connection.Table<NotificationOutbox>()
                .Where(n => n.Status == "Pending")
                .ToListAsync();

            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "InventoryManagementSystem",
                "Notifications");
            Directory.CreateDirectory(folder);

            var sent = 0;
            foreach (var notification in pending)
            {
                try
                {
                    var extension = notification.Channel.Equals("SMS", StringComparison.OrdinalIgnoreCase) ? ".sms.txt" : ".eml";
                    var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{notification.Id}{extension}";
                    var path = Path.Combine(folder, fileName);

                    var content = notification.Channel.Equals("SMS", StringComparison.OrdinalIgnoreCase)
                        ? $"TO: {notification.Recipient}\n{notification.Body}"
                        : $"To: {notification.Recipient}\nSubject: {notification.Subject}\n\n{notification.Body}";

                    await File.WriteAllTextAsync(path, content);

                    notification.Status = "Sent";
                    notification.SentAt = DateTime.UtcNow;
                    notification.ErrorMessage = string.Empty;
                    sent++;
                }
                catch (Exception ex)
                {
                    notification.Status = "Failed";
                    notification.ErrorMessage = ex.Message;
                }

                await _databaseService.Connection.UpdateAsync(notification);
            }

            return sent;
        }

        private static string BuildInvoiceBody(SalesOrder order, string customerName)
        {
            return $"Dear {customerName},\n\nPlease find your invoice {order.SONumber} dated {order.OrderDate:yyyy-MM-dd} for {order.TotalAmount:N2} {order.Currency}.\n\nThank you for your business.";
        }
    }
}
