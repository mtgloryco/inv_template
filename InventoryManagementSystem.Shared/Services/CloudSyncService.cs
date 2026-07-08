using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;
using SQLite;

namespace InventoryManagementSystem.Services
{
    public class CloudSyncService
    {
        private readonly DatabaseService _databaseService;
        private readonly CloudSyncApiClient _apiClient;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public CloudSyncService(DatabaseService databaseService, CloudSyncApiClient? apiClient = null)
        {
            _databaseService = databaseService;
            _apiClient = apiClient ?? new CloudSyncApiClient();
        }

        public string DeviceId => GetOrCreateDeviceIdAsync().GetAwaiter().GetResult();

        public async Task<CloudSyncStatus> GetStatusAsync()
        {
            var state = await GetOrCreateSyncStateAsync();
            return new CloudSyncStatus
            {
                IsConfigured = !string.IsNullOrWhiteSpace(state.ApiBaseUrl),
                IsAuthenticated = !string.IsNullOrWhiteSpace(state.AuthToken),
                OrganizationName = state.OrganizationName,
                LastSyncDate = state.LastPullAt ?? state.LastPushAt,
                LastBackupDate = state.LastBackupAt,
                StatusText = state.LastSyncStatus,
                PendingOutboundCount = state.PendingOutboundCount
            };
        }

        public DateTime? GetLastSyncDate()
        {
            var state = _databaseService.Connection.Table<SyncState>().FirstOrDefaultAsync().GetAwaiter().GetResult();
            return state?.LastPullAt ?? state?.LastPushAt;
        }

        public async Task<CloudSyncResult> ConfigureCloudLoginAsync(string email, string password, string? organizationName = null, bool register = false)
        {
            try
            {
                var state = await GetOrCreateSyncStateAsync();
                _apiClient.BaseUrl = state.ApiBaseUrl;

                CloudAuthResponse auth;
                if (register)
                {
                    auth = await _apiClient.RegisterAsync(email, password, organizationName ?? $"{email.Split('@')[0]} Workspace");
                }
                else
                {
                    try
                    {
                        auth = await _apiClient.LoginAsync(email, password);
                    }
                    catch
                    {
                        auth = await _apiClient.RegisterAsync(email, password, organizationName ?? $"{email.Split('@')[0]} Workspace");
                    }
                }

                state.AuthToken = auth.Token;
                state.OrganizationId = auth.OrganizationId;
                state.OrganizationName = auth.OrganizationName;
                state.CloudUserEmail = auth.Email;
                state.LastSyncStatus = register ? "Registered with cloud" : "Connected to cloud";
                await _databaseService.Connection.InsertOrReplaceAsync(state);

                return CloudSyncResult.Ok(state.LastSyncStatus);
            }
            catch (Exception ex)
            {
                return CloudSyncResult.Fail($"Cloud login failed: {ex.Message}");
            }
        }

        public async Task<bool> BackupToCloudAsync(string userId, string authToken)
        {
            try
            {
                var state = await PrepareApiClientAsync(authToken);
                if (state == null) return false;

                await _databaseService.CheckpointWalAsync();

                var dbPath = _databaseService.DatabasePath;
                if (!File.Exists(dbPath)) return false;

                await using var compressed = await CompressFileAsync(dbPath);
                await _apiClient.UploadBackupAsync(compressed);

                state.LastBackupAt = DateTime.UtcNow;
                state.LastSyncStatus = $"Backup completed by {userId}";
                await _databaseService.Connection.UpdateAsync(state);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cloud backup error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RestoreFromCloudAsync(string userId, string authToken)
        {
            try
            {
                var state = await PrepareApiClientAsync(authToken);
                if (state == null) return false;

                await using var remoteStream = await _apiClient.DownloadBackupAsync();
                var tempGz = Path.Combine(Path.GetTempPath(), $"ims-restore-{Guid.NewGuid():N}.db.gz");
                var tempDb = Path.Combine(Path.GetTempPath(), $"ims-restore-{Guid.NewGuid():N}.db");

                await using (var file = File.Create(tempGz))
                {
                    await remoteStream.CopyToAsync(file);
                }

                await DecompressToFileAsync(tempGz, tempDb);

                await _databaseService.CloseConnectionAsync();
                File.Copy(tempDb, _databaseService.DatabasePath, overwrite: true);
                await _databaseService.ReopenConnectionAsync();
                await _databaseService.InitializeAsync();

                try { File.Delete(tempGz); File.Delete(tempDb); } catch { /* ignore cleanup errors */ }

                state.LastSyncStatus = $"Restored from cloud by {userId}";
                state.LastBackupAt = DateTime.UtcNow;
                await _databaseService.Connection.UpdateAsync(state);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cloud restore error: {ex.Message}");
                await _databaseService.ReopenConnectionAsync();
                return false;
            }
        }

        public async Task<int> SyncDeltaAsync()
        {
            var state = await GetOrCreateSyncStateAsync();
            if (string.IsNullOrWhiteSpace(state.AuthToken))
            {
                state.LastSyncStatus = "Not authenticated";
                await _databaseService.Connection.UpdateAsync(state);
                return 0;
            }

            _apiClient.BaseUrl = state.ApiBaseUrl;
            _apiClient.AuthToken = state.AuthToken;
            _apiClient.OrganizationId = state.OrganizationId;

            var since = state.LastPullAt ?? DateTime.MinValue;
            var pushed = await PushLocalChangesAsync(state);
            var pulled = await PullServerChangesAsync(since);

            var total = pushed + pulled;
            var now = DateTime.UtcNow;
            state.LastPushAt = now;
            state.LastPullAt = now;
            state.PendingOutboundCount = 0;
            state.LastSyncStatus = total > 0 ? $"Synced {total} records" : "Already up to date";
            await _databaseService.Connection.UpdateAsync(state);

            return total;
        }

        public async Task<CloudSyncResult> SyncNowAsync(string userId)
        {
            var state = await GetOrCreateSyncStateAsync();
            if (string.IsNullOrWhiteSpace(state.AuthToken))
            {
                return CloudSyncResult.Fail("Connect to cloud first (email/password in Settings or sync panel).");
            }

            try
            {
                var count = await SyncDeltaAsync();
                return CloudSyncResult.Ok(state.LastSyncStatus, count);
            }
            catch (Exception ex)
            {
                state.LastSyncStatus = $"Sync failed: {ex.Message}";
                await _databaseService.Connection.UpdateAsync(state);
                return CloudSyncResult.Fail(state.LastSyncStatus);
            }
        }

        private async Task<SyncState?> PrepareApiClientAsync(string authToken)
        {
            var state = await GetOrCreateSyncStateAsync();
            if (string.IsNullOrWhiteSpace(authToken) && string.IsNullOrWhiteSpace(state.AuthToken))
            {
                return null;
            }

            _apiClient.BaseUrl = state.ApiBaseUrl;
            _apiClient.AuthToken = string.IsNullOrWhiteSpace(authToken) ? state.AuthToken : authToken;
            _apiClient.OrganizationId = state.OrganizationId;
            return state;
        }

        private async Task<int> PushLocalChangesAsync(SyncState state)
        {
            var since = state.LastPushAt ?? DateTime.MinValue;
            var changes = new List<SyncChangeDto>();
            var deviceId = state.DeviceId;

            foreach (var descriptor in SyncEntityRegistry.All)
            {
                var localChanges = await GetLocalChangesAsync(descriptor, since);
                changes.AddRange(localChanges);
            }

            if (changes.Count == 0) return 0;

            var response = await _apiClient.PushChangesAsync(new SyncPushRequest
            {
                DeviceId = deviceId,
                Changes = changes
            });

            return response.Accepted;
        }

        private async Task<int> PullServerChangesAsync(DateTime sinceUtc)
        {
            var pull = await _apiClient.PullChangesAsync(sinceUtc);
            var applied = 0;

            await _databaseService.Connection.RunInTransactionAsync(conn =>
            {
                foreach (var change in pull.Changes.OrderBy(c => c.UpdatedAt))
                {
                    var descriptor = SyncEntityRegistry.All.FirstOrDefault(d => d.EntityType == change.EntityType);
                    if (descriptor == null) continue;

                    if (ApplyServerChange(conn, descriptor, change))
                    {
                        applied++;
                    }
                }
            });

            return applied;
        }

        private async Task<List<SyncChangeDto>> GetLocalChangesAsync(SyncEntityDescriptor descriptor, DateTime sinceUtc)
        {
            var results = new List<SyncChangeDto>();
            var items = await QuerySyncableEntitiesAsync(descriptor, sinceUtc);
            foreach (var item in items)
            {
                results.Add(new SyncChangeDto
                {
                    EntityType = descriptor.EntityType,
                    SyncId = item.SyncId,
                    PayloadJson = JsonSerializer.Serialize(item, descriptor.ClrType, _jsonOptions),
                    UpdatedAt = item.UpdatedAt.ToUniversalTime(),
                    IsDeleted = item.IsDeleted
                });
            }

            return results;
        }

        private async Task<List<ISyncableEntity>> QuerySyncableEntitiesAsync(SyncEntityDescriptor descriptor, DateTime sinceUtc)
        {
            return descriptor.ClrType.Name switch
            {
                nameof(Product) => (await _databaseService.Connection.Table<Product>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(StockMovement) => (await _databaseService.Connection.Table<StockMovement>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(Supplier) => (await _databaseService.Connection.Table<Supplier>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(PurchaseOrder) => (await _databaseService.Connection.Table<PurchaseOrder>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(PurchaseOrderItem) => (await _databaseService.Connection.Table<PurchaseOrderItem>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(SalesOrder) => (await _databaseService.Connection.Table<SalesOrder>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(SalesOrderItem) => (await _databaseService.Connection.Table<SalesOrderItem>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(Category) => (await _databaseService.Connection.Table<Category>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(Tax) => (await _databaseService.Connection.Table<Tax>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(SupplierProduct) => (await _databaseService.Connection.Table<SupplierProduct>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(Account) => (await _databaseService.Connection.Table<Account>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(Journal) => (await _databaseService.Connection.Table<Journal>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(JournalEntry) => (await _databaseService.Connection.Table<JournalEntry>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(JournalLine) => (await _databaseService.Connection.Table<JournalLine>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(ProductBundle) => (await _databaseService.Connection.Table<ProductBundle>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(BillOfMaterial) => (await _databaseService.Connection.Table<BillOfMaterial>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(BillOfMaterialLine) => (await _databaseService.Connection.Table<BillOfMaterialLine>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(ManufacturingOrder) => (await _databaseService.Connection.Table<ManufacturingOrder>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(ManufacturingOrderLine) => (await _databaseService.Connection.Table<ManufacturingOrderLine>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(CustomerReturn) => (await _databaseService.Connection.Table<CustomerReturn>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(SupplierReturn) => (await _databaseService.Connection.Table<SupplierReturn>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(Location) => (await _databaseService.Connection.Table<Location>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(LocationStock) => (await _databaseService.Connection.Table<LocationStock>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                nameof(StockTransfer) => (await _databaseService.Connection.Table<StockTransfer>().Where(p => p.UpdatedAt > sinceUtc).ToListAsync()).Cast<ISyncableEntity>().ToList(),
                _ => new List<ISyncableEntity>()
            };
        }

        private bool ApplyServerChange(SQLiteConnection conn, SyncEntityDescriptor descriptor, SyncChangeDto change)
        {
            if (change.IsDeleted)
            {
                return SoftDeleteLocalBySyncId(conn, descriptor, change.SyncId, change.UpdatedAt);
            }

            var entity = JsonSerializer.Deserialize(change.PayloadJson, descriptor.ClrType, _jsonOptions);
            if (entity is not ISyncableEntity syncable) return false;

            syncable.SyncId = change.SyncId;
            syncable.UpdatedAt = change.UpdatedAt.ToUniversalTime();
            syncable.IsDeleted = false;

            var existing = FindLocalBySyncId(conn, descriptor, change.SyncId);
            if (existing == null)
            {
                ResetAutoIncrementId(entity);
                conn.Insert(entity);
                return true;
            }

            CopyLocalId(existing, syncable);
            if (GetUpdatedAt(existing) <= change.UpdatedAt.ToUniversalTime())
            {
                conn.Update(entity);
                return true;
            }

            return false;
        }

        private static ISyncableEntity? FindLocalBySyncId(SQLiteConnection conn, SyncEntityDescriptor descriptor, Guid syncId)
        {
            return descriptor.EntityType switch
            {
                "Product" => conn.Table<Product>().FirstOrDefault(x => x.SyncId == syncId),
                "StockMovement" => conn.Table<StockMovement>().FirstOrDefault(x => x.SyncId == syncId),
                "Supplier" => conn.Table<Supplier>().FirstOrDefault(x => x.SyncId == syncId),
                "PurchaseOrder" => conn.Table<PurchaseOrder>().FirstOrDefault(x => x.SyncId == syncId),
                "PurchaseOrderItem" => conn.Table<PurchaseOrderItem>().FirstOrDefault(x => x.SyncId == syncId),
                "SalesOrder" => conn.Table<SalesOrder>().FirstOrDefault(x => x.SyncId == syncId),
                "SalesOrderItem" => conn.Table<SalesOrderItem>().FirstOrDefault(x => x.SyncId == syncId),
                "Category" => conn.Table<Category>().FirstOrDefault(x => x.SyncId == syncId),
                "Tax" => conn.Table<Tax>().FirstOrDefault(x => x.SyncId == syncId),
                "SupplierProduct" => conn.Table<SupplierProduct>().FirstOrDefault(x => x.SyncId == syncId),
                "Account" => conn.Table<Account>().FirstOrDefault(x => x.SyncId == syncId),
                "Journal" => conn.Table<Journal>().FirstOrDefault(x => x.SyncId == syncId),
                "JournalEntry" => conn.Table<JournalEntry>().FirstOrDefault(x => x.SyncId == syncId),
                "JournalLine" => conn.Table<JournalLine>().FirstOrDefault(x => x.SyncId == syncId),
                "ProductBundle" => conn.Table<ProductBundle>().FirstOrDefault(x => x.SyncId == syncId),
                "BillOfMaterial" => conn.Table<BillOfMaterial>().FirstOrDefault(x => x.SyncId == syncId),
                "BillOfMaterialLine" => conn.Table<BillOfMaterialLine>().FirstOrDefault(x => x.SyncId == syncId),
                "ManufacturingOrder" => conn.Table<ManufacturingOrder>().FirstOrDefault(x => x.SyncId == syncId),
                "ManufacturingOrderLine" => conn.Table<ManufacturingOrderLine>().FirstOrDefault(x => x.SyncId == syncId),
                "CustomerReturn" => conn.Table<CustomerReturn>().FirstOrDefault(x => x.SyncId == syncId),
                "SupplierReturn" => conn.Table<SupplierReturn>().FirstOrDefault(x => x.SyncId == syncId),
                "Location" => conn.Table<Location>().FirstOrDefault(x => x.SyncId == syncId),
                "LocationStock" => conn.Table<LocationStock>().FirstOrDefault(x => x.SyncId == syncId),
                "StockTransfer" => conn.Table<StockTransfer>().FirstOrDefault(x => x.SyncId == syncId),
                _ => null
            };
        }

        private static bool SoftDeleteLocalBySyncId(SQLiteConnection conn, SyncEntityDescriptor descriptor, Guid syncId, DateTime updatedAt)
        {
            var existing = FindLocalBySyncId(conn, descriptor, syncId);
            if (existing == null) return false;

            existing.IsDeleted = true;
            existing.UpdatedAt = updatedAt.ToUniversalTime();
            conn.Update(existing, existing.GetType());
            return true;
        }

        private static DateTime GetUpdatedAt(ISyncableEntity entity) => entity.UpdatedAt.ToUniversalTime();

        private static void ResetAutoIncrementId(object entity)
        {
            var idProp = entity.GetType().GetProperty("Id");
            if (idProp != null && idProp.CanWrite && idProp.PropertyType == typeof(int))
            {
                idProp.SetValue(entity, 0);
            }
        }

        private static void CopyLocalId(ISyncableEntity target, ISyncableEntity source)
        {
            var idProp = target.GetType().GetProperty("Id");
            var sourceIdProp = source.GetType().GetProperty("Id");
            if (idProp != null && sourceIdProp != null && idProp.CanWrite)
            {
                idProp.SetValue(source, idProp.GetValue(target));
            }
        }

        private async Task<SyncState> GetOrCreateSyncStateAsync()
        {
            var deviceId = await GetOrCreateDeviceIdAsync();
            var state = await _databaseService.Connection.Table<SyncState>().FirstOrDefaultAsync(s => s.DeviceId == deviceId);
            if (state != null) return state;

            state = new SyncState
            {
                DeviceId = deviceId,
                ApiBaseUrl = CloudSyncDefaults.ResolveApiBaseUrl(),
                LastSyncStatus = "Never synced"
            };
            await _databaseService.Connection.InsertAsync(state);
            return state;
        }

        private async Task<string> GetOrCreateDeviceIdAsync()
        {
            var existing = await _databaseService.Connection.Table<SyncState>().FirstOrDefaultAsync();
            if (existing != null && !string.IsNullOrWhiteSpace(existing.DeviceId))
            {
                return existing.DeviceId;
            }

            return Environment.MachineName + "-" + Guid.NewGuid().ToString("N")[..8];
        }

        private static async Task<MemoryStream> CompressFileAsync(string sourcePath)
        {
            var output = new MemoryStream();
            await using (var input = File.OpenRead(sourcePath))
            await using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                await input.CopyToAsync(gzip);
            }

            output.Position = 0;
            return output;
        }

        private static async Task DecompressToFileAsync(string gzipPath, string outputPath)
        {
            await using var input = File.OpenRead(gzipPath);
            await using var gzip = new GZipStream(input, CompressionMode.Decompress);
            await using var output = File.Create(outputPath);
            await gzip.CopyToAsync(output);
        }
    }
}
