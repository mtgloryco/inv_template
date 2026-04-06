using System;
using System.IO;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using SQLite;

namespace InventoryManagementSystem.Infrastructure
{
    public class DatabaseService
    {
        private readonly string _databasePath;
        private readonly string _legacyDatabasePath;
        private SQLiteAsyncConnection _connection;
        private const int CurrentDatabaseVersion = 1;

        public DatabaseService()
        {
            // Standard User Data Location: %AppData%/InventoryManagementSystem
            // This ensures data survives application updates/reinstalls in the same location
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "InventoryManagementSystem");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            
            _databasePath = Path.Combine(folder, "inventory.db");
            
            // Legacy path check (where the app runs from)
            _legacyDatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventory_v1.db");
            
            _connection = new SQLiteAsyncConnection(_databasePath);
        }

        public async Task InitializeAsync()
        {
            // 1. Check for legacy import
            await ImportLegacyDatabaseIfNeeded();

            // 2. Create Tables (Idempotent - only creates if missing)
            await _connection.CreateTableAsync<Product>();
            await _connection.CreateTableAsync<Category>();
            await _connection.CreateTableAsync<StockMovement>();
            await _connection.CreateTableAsync<PurchaseBatch>();
            await _connection.CreateTableAsync<SaleBatchUsage>();
            await _connection.CreateTableAsync<User>();
            await _connection.CreateTableAsync<LocalLicense>();
            await _connection.CreateTableAsync<Supplier>();
            await _connection.CreateTableAsync<PurchaseOrder>();
            await _connection.CreateTableAsync<PurchaseOrderItem>();
            await _connection.CreateTableAsync<ReorderRule>();
            await _connection.CreateTableAsync<Location>();
            await _connection.CreateTableAsync<LocationStock>();
            await _connection.CreateTableAsync<StockTransfer>();
            await _connection.CreateTableAsync<CustomerReturn>();
            await _connection.CreateTableAsync<SupplierReturn>();
            await _connection.CreateTableAsync<ProductBundle>();
            await _connection.CreateTableAsync<AuditLog>();

            // 3. Perform Schema Migrations
            await PerformMigrationsAsync();

            // 4. Seed Initial Data
            await SeedDataAsync();
        }

        private async Task ImportLegacyDatabaseIfNeeded()
        {
            // If new DB doesn't exist, but legacy one does, copy it to preserver data
            if (!File.Exists(_databasePath) && File.Exists(_legacyDatabasePath))
            {
                try
                {
                   await Task.Run(() => File.Copy(_legacyDatabasePath, _databasePath));
                }
                catch (Exception ex)
                {
                    // Log or handle copy failure silently - starting fresh is better than crashing
                    System.Diagnostics.Debug.WriteLine($"Failed to migrate legacy DB: {ex.Message}");
                }
            }
        }

        private async Task PerformMigrationsAsync()
        {
            // Get current user_version from PRAGMA
            var metaVersion = await _connection.ExecuteScalarAsync<int>("PRAGMA user_version");

            if (metaVersion < CurrentDatabaseVersion)
            {
                // Future migrations will go here
                // if (metaVersion < 2) { await MigrateToV2(); }
                
                // Update version after migrations
                await _connection.ExecuteAsync($"PRAGMA user_version = {CurrentDatabaseVersion}");
            }
        }

        private async Task SeedDataAsync()
        {
            // App starts clean for production. No test categories or products seeded.
            // Check if we need to seed a default admin if none exists
            var userCount = await _connection.Table<User>().CountAsync();
            if (userCount == 0)
            {
                await _connection.InsertAsync(new User 
                { 
                    Username = "admin", 
                    Role = "Admin",
                    PasswordHash = "admin123", // Ideally hashed, but for v1 MVP default
                    IsActive = true
                });
            }
        }

        public SQLiteAsyncConnection Connection => _connection;
    }
}
