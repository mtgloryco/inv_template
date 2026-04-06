using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class AuditService
    {
        private readonly DatabaseService _databaseService;

        public AuditService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task LogActionAsync(string username, string action, string entityName, int entityId, object? newValue = null, object? oldValue = null)
        {
            var log = new AuditLog
            {
                EntityType = entityName,
                EntityId = entityId,
                Action = action,
                ChangedByUsername = username,
                Timestamp = DateTime.Now,
                NewValues = newValue != null ? JsonSerializer.Serialize(newValue) : string.Empty,
                OldValues = oldValue != null ? JsonSerializer.Serialize(oldValue) : string.Empty
            };

            await _databaseService.Connection.InsertAsync(log);
        }

        public async Task LogAsync(string entityType, int entityId, string action, string username, object oldValue, object newValue)
        {
            await LogActionAsync(username, action, entityType, entityId, newValue, oldValue);
        }

        public async Task<List<AuditLog>> GetAuditTrailAsync(string entityType, int entityId)
        {
            return await _databaseService.Connection.Table<AuditLog>()
                .Where(l => l.EntityType == entityType && l.EntityId == entityId)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetUserActivityAsync(string username, DateTime from, DateTime to)
        {
            return await _databaseService.Connection.Table<AuditLog>()
                .Where(l => l.ChangedByUsername == username && l.Timestamp >= from && l.Timestamp <= to)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetAuditReportAsync(DateTime from, DateTime to)
        {
            return await _databaseService.Connection.Table<AuditLog>()
                .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }
    }
}
