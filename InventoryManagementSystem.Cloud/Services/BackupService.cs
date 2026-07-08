using InventoryManagementSystem.Cloud.Data;
using InventoryManagementSystem.Cloud.Models;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace InventoryManagementSystem.Cloud.Services;

public class BackupService
{
    private readonly CloudDatabase _db;
    private readonly string _storageRoot;

    public BackupService(CloudDatabase db, IConfiguration configuration)
    {
        _db = db;
        _storageRoot = configuration["BackupStoragePath"] ?? "backups";
        Directory.CreateDirectory(_storageRoot);
    }

    public async Task SaveBackupAsync(Guid organizationId, Stream compressedDatabase, long sizeBytes)
    {
        var orgFolder = Path.Combine(_storageRoot, organizationId.ToString());
        Directory.CreateDirectory(orgFolder);

        var fileName = "inventory.db.gz";
        var filePath = Path.Combine(orgFolder, fileName);

        await using (var file = File.Create(filePath))
        {
            await compressedDatabase.CopyToAsync(file);
        }

        var uploadedAt = DateTime.UtcNow;

        await _db.WithConnectionAsync(async conn =>
        {
            if (_db.Provider == CloudDatabaseProvider.Postgres)
            {
                var pg = (NpgsqlConnection)conn;
                await using var cmd = new NpgsqlCommand(
                    """
                    INSERT INTO backup_metadata (organization_id, file_name, uploaded_at, size_bytes)
                    VALUES (@org, @file, @uploaded, @size)
                    ON CONFLICT (organization_id)
                    DO UPDATE SET file_name = EXCLUDED.file_name, uploaded_at = EXCLUDED.uploaded_at, size_bytes = EXCLUDED.size_bytes
                    """, pg);
                cmd.Parameters.AddWithValue("org", organizationId);
                cmd.Parameters.AddWithValue("file", fileName);
                cmd.Parameters.AddWithValue("uploaded", uploadedAt);
                cmd.Parameters.AddWithValue("size", sizeBytes);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                var sqlite = (SqliteConnection)conn;
                await using var cmd = sqlite.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO BackupMetadata (OrganizationId, FileName, UploadedAt, SizeBytes)
                    VALUES ($org, $file, $uploaded, $size)
                    ON CONFLICT(OrganizationId)
                    DO UPDATE SET FileName = excluded.FileName, UploadedAt = excluded.UploadedAt, SizeBytes = excluded.SizeBytes
                    """;
                cmd.Parameters.AddWithValue("$org", organizationId.ToString());
                cmd.Parameters.AddWithValue("$file", fileName);
                cmd.Parameters.AddWithValue("$uploaded", uploadedAt.ToString("O"));
                cmd.Parameters.AddWithValue("$size", sizeBytes);
                await cmd.ExecuteNonQueryAsync();
            }
        });
    }

    public async Task<(Stream Stream, string FileName)?> GetBackupAsync(Guid organizationId)
    {
        var orgFolder = Path.Combine(_storageRoot, organizationId.ToString());
        var filePath = Path.Combine(orgFolder, "inventory.db.gz");

        if (!File.Exists(filePath))
        {
            return null;
        }

        var stream = File.OpenRead(filePath);
        return (stream, "inventory.db.gz");
    }

    public async Task<BackupInfoResponse> GetInfoAsync(Guid organizationId)
    {
        var orgFolder = Path.Combine(_storageRoot, organizationId.ToString());
        var filePath = Path.Combine(orgFolder, "inventory.db.gz");

        if (!File.Exists(filePath))
        {
            return new BackupInfoResponse(false, null, 0);
        }

        var fileInfo = new FileInfo(filePath);
        DateTime? uploadedAt = fileInfo.LastWriteTimeUtc;

        await _db.WithConnectionAsync(async conn =>
        {
            if (_db.Provider == CloudDatabaseProvider.Postgres)
            {
                var pg = (NpgsqlConnection)conn;
                await using var cmd = new NpgsqlCommand(
                    "SELECT uploaded_at, size_bytes FROM backup_metadata WHERE organization_id = @org", pg);
                cmd.Parameters.AddWithValue("org", organizationId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    uploadedAt = reader.GetDateTime(0);
                }
            }
            else
            {
                var sqlite = (SqliteConnection)conn;
                await using var cmd = sqlite.CreateCommand();
                cmd.CommandText = "SELECT UploadedAt, SizeBytes FROM BackupMetadata WHERE OrganizationId = $org";
                cmd.Parameters.AddWithValue("$org", organizationId.ToString());
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    uploadedAt = DateTime.Parse(reader.GetString(0));
                }
            }
        });

        return new BackupInfoResponse(true, uploadedAt, fileInfo.Length);
    }
}
