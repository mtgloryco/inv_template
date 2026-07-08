using InventoryManagementSystem.Cloud.Data;
using InventoryManagementSystem.Cloud.Models;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace InventoryManagementSystem.Cloud.Services;

public class SyncService
{
    private readonly CloudDatabase _db;

    public SyncService(CloudDatabase db)
    {
        _db = db;
    }

    public async Task<int> PushAsync(Guid organizationId, SyncPushRequest request)
    {
        if (request.Changes.Count == 0) return 0;

        var accepted = 0;

        await _db.WithConnectionAsync(async conn =>
        {
            foreach (var change in request.Changes)
            {
                if (_db.Provider == CloudDatabaseProvider.Postgres)
                {
                    accepted += await UpsertPostgresAsync((NpgsqlConnection)conn, organizationId, request.DeviceId, change);
                }
                else
                {
                    accepted += await UpsertSqliteAsync((SqliteConnection)conn, organizationId, request.DeviceId, change);
                }
            }
        });

        return accepted;
    }

    public async Task<SyncPullResponse> PullAsync(Guid organizationId, DateTime sinceUtc)
    {
        var changes = new List<SyncChangeRecord>();

        await _db.WithConnectionAsync(async conn =>
        {
            if (_db.Provider == CloudDatabaseProvider.Postgres)
            {
                var pg = (NpgsqlConnection)conn;
                await using var cmd = new NpgsqlCommand(
                    """
                    SELECT entity_type, sync_id, payload_json, updated_at, is_deleted
                    FROM sync_records
                    WHERE organization_id = @org AND updated_at > @since
                    ORDER BY updated_at ASC
                    """, pg);
                cmd.Parameters.AddWithValue("org", organizationId);
                cmd.Parameters.AddWithValue("since", sinceUtc);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    changes.Add(new SyncChangeRecord(
                        reader.GetString(0),
                        reader.GetGuid(1),
                        reader.GetString(2),
                        reader.GetDateTime(3),
                        reader.GetBoolean(4)));
                }
            }
            else
            {
                var sqlite = (SqliteConnection)conn;
                await using var cmd = sqlite.CreateCommand();
                cmd.CommandText = """
                    SELECT EntityType, SyncId, PayloadJson, UpdatedAt, IsDeleted
                    FROM SyncRecords
                    WHERE OrganizationId = $org AND UpdatedAt > $since
                    ORDER BY UpdatedAt ASC
                    """;
                cmd.Parameters.AddWithValue("$org", organizationId.ToString());
                cmd.Parameters.AddWithValue("$since", sinceUtc.ToString("O"));

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    changes.Add(new SyncChangeRecord(
                        reader.GetString(0),
                        Guid.Parse(reader.GetString(1)),
                        reader.GetString(2),
                        DateTime.Parse(reader.GetString(3)),
                        reader.GetInt32(4) == 1));
                }
            }
        });

        return new SyncPullResponse(changes, DateTime.UtcNow);
    }

    private static async Task<int> UpsertPostgresAsync(
        NpgsqlConnection conn, Guid organizationId, string deviceId, SyncChangeRecord change)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO sync_records (id, organization_id, entity_type, sync_id, payload_json, updated_at, is_deleted, device_id)
            VALUES (@id, @org, @type, @syncId, @payload::jsonb, @updated, @deleted, @device)
            ON CONFLICT (organization_id, entity_type, sync_id)
            DO UPDATE SET
                payload_json = EXCLUDED.payload_json,
                updated_at = EXCLUDED.updated_at,
                is_deleted = EXCLUDED.is_deleted,
                device_id = EXCLUDED.device_id
            WHERE sync_records.updated_at <= EXCLUDED.updated_at
            """, conn);

        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("org", organizationId);
        cmd.Parameters.AddWithValue("type", change.EntityType);
        cmd.Parameters.AddWithValue("syncId", change.SyncId);
        cmd.Parameters.AddWithValue("payload", change.PayloadJson);
        cmd.Parameters.AddWithValue("updated", change.UpdatedAt.ToUniversalTime());
        cmd.Parameters.AddWithValue("deleted", change.IsDeleted);
        cmd.Parameters.AddWithValue("device", deviceId);

        return await cmd.ExecuteNonQueryAsync() > 0 ? 1 : 0;
    }

    private static async Task<int> UpsertSqliteAsync(
        SqliteConnection conn, Guid organizationId, string deviceId, SyncChangeRecord change)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SyncRecords (Id, OrganizationId, EntityType, SyncId, PayloadJson, UpdatedAt, IsDeleted, DeviceId)
            VALUES ($id, $org, $type, $syncId, $payload, $updated, $deleted, $device)
            ON CONFLICT(OrganizationId, EntityType, SyncId)
            DO UPDATE SET
                PayloadJson = excluded.PayloadJson,
                UpdatedAt = excluded.UpdatedAt,
                IsDeleted = excluded.IsDeleted,
                DeviceId = excluded.DeviceId
            WHERE SyncRecords.UpdatedAt <= excluded.UpdatedAt
            """;

        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$org", organizationId.ToString());
        cmd.Parameters.AddWithValue("$type", change.EntityType);
        cmd.Parameters.AddWithValue("$syncId", change.SyncId.ToString());
        cmd.Parameters.AddWithValue("$payload", change.PayloadJson);
        cmd.Parameters.AddWithValue("$updated", change.UpdatedAt.ToUniversalTime().ToString("O"));
        cmd.Parameters.AddWithValue("$deleted", change.IsDeleted ? 1 : 0);
        cmd.Parameters.AddWithValue("$device", deviceId);

        return await cmd.ExecuteNonQueryAsync() > 0 ? 1 : 0;
    }
}
