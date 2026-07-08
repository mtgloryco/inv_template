using System;
using System.Collections.Generic;

namespace InventoryManagementSystem.Services
{
    public class CloudAuthRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? OrganizationName { get; set; }
    }

    public class CloudAuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string OrganizationId { get; set; } = string.Empty;
        public string OrganizationName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class SyncChangeDto
    {
        public string EntityType { get; set; } = string.Empty;
        public Guid SyncId { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class SyncPushRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public List<SyncChangeDto> Changes { get; set; } = new();
    }

    public class SyncPushResponse
    {
        public int Accepted { get; set; }
    }

    public class SyncPullResponse
    {
        public List<SyncChangeDto> Changes { get; set; } = new();
        public DateTime ServerTime { get; set; }
    }

    public class BackupInfoResponse
    {
        public bool Exists { get; set; }
        public DateTime? UploadedAt { get; set; }
        public long SizeBytes { get; set; }
    }

    public class CloudSyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int RecordsSynced { get; set; }

        public static CloudSyncResult Ok(string message, int records = 0) =>
            new() { Success = true, Message = message, RecordsSynced = records };

        public static CloudSyncResult Fail(string message) =>
            new() { Success = false, Message = message };
    }

    public class CloudSyncStatus
    {
        public bool IsConfigured { get; set; }
        public bool IsAuthenticated { get; set; }
        public string? OrganizationName { get; set; }
        public DateTime? LastSyncDate { get; set; }
        public DateTime? LastBackupDate { get; set; }
        public string StatusText { get; set; } = "Not configured";
        public int PendingOutboundCount { get; set; }
    }
}
