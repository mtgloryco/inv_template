using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class MobileFieldService
    {
        private readonly DatabaseService _databaseService;
        private readonly AuditService? _auditService;

        public MobileFieldService(DatabaseService databaseService, AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _auditService = auditService;
        }

        public async Task<(MobileDeviceRegistration Device, string ApiKey)> RegisterDeviceAsync(
            string deviceName, string deviceType, int branchId, string username)
        {
            var apiKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var hash = HashApiKey(apiKey);

            var device = new MobileDeviceRegistration
            {
                DeviceName = deviceName,
                DeviceType = deviceType,
                ApiKeyHash = hash,
                BranchId = branchId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _databaseService.Connection.InsertAsync(device);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "Register", "MobileDeviceRegistration", device.Id,
                    new { device.DeviceName, device.DeviceType, device.BranchId });
            }

            return (device, apiKey);
        }

        public async Task<List<MobileDeviceRegistration>> GetActiveDevicesAsync()
        {
            return await _databaseService.Connection.Table<MobileDeviceRegistration>()
                .Where(d => d.IsActive)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<MobileSyncQueue> QueueSyncOperationAsync(
            int deviceId, string operationType, object payload, string username)
        {
            var device = await _databaseService.Connection.FindAsync<MobileDeviceRegistration>(deviceId)
                         ?? throw new InvalidOperationException("Device not found.");

            var entry = new MobileSyncQueue
            {
                DeviceId = deviceId,
                OperationType = operationType,
                PayloadJson = JsonSerializer.Serialize(payload),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _databaseService.Connection.InsertAsync(entry);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "QueueSync", "MobileSyncQueue", entry.Id, entry);
            }

            return entry;
        }

        public async Task<int> ProcessPendingSyncAsync(int deviceId)
        {
            var pending = await _databaseService.Connection.Table<MobileSyncQueue>()
                .Where(q => q.DeviceId == deviceId && q.Status == "Pending")
                .ToListAsync();

            foreach (var item in pending)
            {
                item.Status = "Processed";
                item.ProcessedAt = DateTime.UtcNow;
                await _databaseService.Connection.UpdateAsync(item);
            }

            if (pending.Count > 0)
            {
                var device = await _databaseService.Connection.FindAsync<MobileDeviceRegistration>(deviceId);
                if (device != null)
                {
                    device.LastSyncAt = DateTime.UtcNow;
                    await _databaseService.Connection.UpdateAsync(device);
                }
            }

            return pending.Count;
        }

        public async Task<string> ExportWarehousePickListAsync(int branchId)
        {
            var locations = await _databaseService.Connection.Table<Location>()
                .Where(l => l.BranchId == branchId && !l.IsDeleted)
                .ToListAsync();
            var locationIds = locations.Select(l => l.Id).ToHashSet();

            var stock = await _databaseService.Connection.Table<LocationStock>()
                .Where(ls => !ls.IsDeleted)
                .ToListAsync();
            var products = await _databaseService.Connection.Table<Product>()
                .Where(p => !p.IsDeleted)
                .ToListAsync();
            var productDict = products.ToDictionary(p => p.Id);

            var pickLines = stock
                .Where(ls => locationIds.Contains(ls.LocationId) && ls.Quantity > 0)
                .Select(ls =>
                {
                    productDict.TryGetValue(ls.ProductId, out var product);
                    return new
                    {
                        ProductId = ls.ProductId,
                        SKU = product?.SKU ?? string.Empty,
                        Name = product?.Name ?? "Unknown",
                        LocationId = ls.LocationId,
                        Quantity = ls.Quantity
                    };
                })
                .OrderBy(l => l.Name)
                .ToList();

            return JsonSerializer.Serialize(new { BranchId = branchId, GeneratedAt = DateTime.UtcNow, Lines = pickLines });
        }

        public static string HashApiKey(string apiKey)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
            return Convert.ToHexString(bytes);
        }
    }
}
