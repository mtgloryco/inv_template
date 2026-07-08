using InventoryManagementSystem.Cloud.Models;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace InventoryManagementSystem.Cloud.Data;

public enum CloudDatabaseProvider
{
    Sqlite,
    Postgres
}

public class CloudDatabase
{
    private readonly string _connectionString;
    public CloudDatabaseProvider Provider { get; }

    public CloudDatabase(IConfiguration configuration)
    {
        var postgresUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? configuration.GetConnectionString("Postgres");

        if (!string.IsNullOrWhiteSpace(postgresUrl))
        {
            _connectionString = NormalizePostgresConnectionString(postgresUrl);
            Provider = CloudDatabaseProvider.Postgres;
        }
        else
        {
            _connectionString = configuration.GetConnectionString("Default") ?? "Data Source=cloud.db";
            Provider = CloudDatabaseProvider.Sqlite;
        }
    }

    public async Task InitializeAsync()
    {
        if (Provider == CloudDatabaseProvider.Postgres)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await ExecutePostgresSchemaAsync(conn);
        }
        else
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            await ExecuteSqliteSchemaAsync(conn);
        }
    }

    public async Task<T> WithConnectionAsync<T>(Func<object, Task<T>> action)
    {
        if (Provider == CloudDatabaseProvider.Postgres)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            return await action(conn);
        }

        await using var sqlite = new SqliteConnection(_connectionString);
        await sqlite.OpenAsync();
        return await action(sqlite);
    }

    public async Task WithConnectionAsync(Func<object, Task> action)
    {
        await WithConnectionAsync<object>(async conn =>
        {
            await action(conn);
            return null!;
        });
    }

    private static string NormalizePostgresConnectionString(string url)
    {
        if (url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            var user = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
            var database = uri.AbsolutePath.TrimStart('/');
            return $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={database};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true";
        }

        return url;
    }

    private static async Task ExecuteSqliteSchemaAsync(SqliteConnection conn)
    {
        var sql = """
            CREATE TABLE IF NOT EXISTS Organizations (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Email TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                OrganizationId TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS SyncRecords (
                Id TEXT PRIMARY KEY,
                OrganizationId TEXT NOT NULL,
                EntityType TEXT NOT NULL,
                SyncId TEXT NOT NULL,
                PayloadJson TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeviceId TEXT,
                UNIQUE(OrganizationId, EntityType, SyncId)
            );

            CREATE INDEX IF NOT EXISTS IX_SyncRecords_OrgUpdated
                ON SyncRecords(OrganizationId, UpdatedAt);

            CREATE TABLE IF NOT EXISTS BackupMetadata (
                OrganizationId TEXT PRIMARY KEY,
                FileName TEXT NOT NULL,
                UploadedAt TEXT NOT NULL,
                SizeBytes INTEGER NOT NULL
            );
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task ExecutePostgresSchemaAsync(NpgsqlConnection conn)
    {
        var sql = """
            CREATE TABLE IF NOT EXISTS organizations (
                id UUID PRIMARY KEY,
                name TEXT NOT NULL,
                created_at TIMESTAMPTZ NOT NULL
            );

            CREATE TABLE IF NOT EXISTS users (
                id UUID PRIMARY KEY,
                email TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                organization_id UUID NOT NULL REFERENCES organizations(id),
                created_at TIMESTAMPTZ NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sync_records (
                id UUID PRIMARY KEY,
                organization_id UUID NOT NULL REFERENCES organizations(id),
                entity_type TEXT NOT NULL,
                sync_id UUID NOT NULL,
                payload_json JSONB NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL,
                is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                device_id TEXT,
                UNIQUE(organization_id, entity_type, sync_id)
            );

            CREATE INDEX IF NOT EXISTS ix_sync_records_org_updated
                ON sync_records(organization_id, updated_at);

            CREATE TABLE IF NOT EXISTS backup_metadata (
                organization_id UUID PRIMARY KEY REFERENCES organizations(id),
                file_name TEXT NOT NULL,
                uploaded_at TIMESTAMPTZ NOT NULL,
                size_bytes BIGINT NOT NULL
            );
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
