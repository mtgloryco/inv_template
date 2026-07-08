using System;
using SQLite;

namespace InventoryManagementSystem.Domain
{
    /// <summary>
    /// Local sync cursor and cloud session state (one row per device).
    /// </summary>
    public class SyncState
    {
        [PrimaryKey]
        public string DeviceId { get; set; } = string.Empty;

        public string? OrganizationId { get; set; }
        public string? OrganizationName { get; set; }
        public string? CloudUserEmail { get; set; }
        public string? AuthToken { get; set; }
        public string ApiBaseUrl { get; set; } = "http://localhost:5080";

        public DateTime? LastPullAt { get; set; }
        public DateTime? LastPushAt { get; set; }
        public DateTime? LastBackupAt { get; set; }
        public int PendingOutboundCount { get; set; }
        public string LastSyncStatus { get; set; } = "Never synced";
    }

    /// <summary>
    /// Sync metadata shared by entities that participate in cloud delta sync.
    /// </summary>
    public interface ISyncableEntity
    {
        Guid SyncId { get; set; }
        DateTime UpdatedAt { get; set; }
        bool IsDeleted { get; set; }
    }
}
