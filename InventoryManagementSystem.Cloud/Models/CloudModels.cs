using System.Security.Claims;

namespace InventoryManagementSystem.Cloud.Models;

public record RegisterRequest(string Email, string Password, string OrganizationName);
public record LoginRequest(string Email, string Password);

public record AuthResponse(
    string Token,
    string OrganizationId,
    string OrganizationName,
    string UserId,
    string Email);

public record SyncChangeRecord(
    string EntityType,
    Guid SyncId,
    string PayloadJson,
    DateTime UpdatedAt,
    bool IsDeleted);

public record SyncPushRequest(string DeviceId, List<SyncChangeRecord> Changes);
public record SyncPushResponse(int Accepted);

public record SyncPullResponse(List<SyncChangeRecord> Changes, DateTime ServerTime);

public record BackupInfoResponse(bool Exists, DateTime? UploadedAt, long SizeBytes);

public class CloudUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SyncRecord
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid SyncId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string? DeviceId { get; set; }
}

public class BackupMetadata
{
    public Guid OrganizationId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public long SizeBytes { get; set; }
}

public static class CloudClaims
{
    public const string OrganizationId = "org_id";
    public const string UserId = "user_id";

    public static Guid GetOrganizationId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(OrganizationId);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public static Guid GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(UserId);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
