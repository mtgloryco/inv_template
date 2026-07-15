using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;
using SQLite;

namespace InventoryManagementSystem.Infrastructure
{
    public class DatabaseService
    {
        private readonly string _databasePath;
        private readonly string _legacyDatabasePath;
        private SQLiteAsyncConnection _connection;
        private const int CurrentDatabaseVersion = 6;

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

        /// <summary>
        /// Testability seam: points the connection at an explicit database file (e.g. a temp file in unit tests)
        /// instead of the fixed AppData location used by the parameterless constructor. Production code paths
        /// (App.axaml.cs) continue to use <see cref="DatabaseService()"/> and are unaffected by this overload.
        /// </summary>
        public DatabaseService(string customDatabasePath)
        {
            _databasePath = customDatabasePath;
            var folder = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            _legacyDatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventory_v1.db");
            _connection = new SQLiteAsyncConnection(_databasePath);
        }

        public async Task InitializeAsync(string defaultCurrency = "RWF")
        {
            // 0. Enable WAL journal mode and optimize synchronicity settings for performance
            try
            {
                await _connection.ExecuteAsync("PRAGMA journal_mode = WAL;");
                await _connection.ExecuteAsync("PRAGMA synchronous = NORMAL;");
            }
            catch {}

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
            await _connection.CreateTableAsync<SupplierProduct>();
            await _connection.CreateTableAsync<PurchaseOrder>();
            await _connection.CreateTableAsync<PurchaseOrderItem>();
            await _connection.CreateTableAsync<ReorderRule>();
            await _connection.CreateTableAsync<SalesOrder>();
            await _connection.CreateTableAsync<SalesOrderItem>();
            await _connection.CreateTableAsync<PaymentTerm>();
            await _connection.CreateTableAsync<Location>();
            await _connection.CreateTableAsync<LocationStock>();
            await _connection.CreateTableAsync<StockTransfer>();
            await _connection.CreateTableAsync<CustomerReturn>();
            await _connection.CreateTableAsync<SupplierReturn>();
            await _connection.CreateTableAsync<ProductBundle>();
            await _connection.CreateTableAsync<AuditLog>();
            await _connection.CreateTableAsync<Tax>();
            await _connection.CreateTableAsync<ProductUnit>();
            await _connection.CreateTableAsync<Account>();
            await _connection.CreateTableAsync<Journal>();
            await _connection.CreateTableAsync<JournalEntry>();
            await _connection.CreateTableAsync<JournalLine>();
            await _connection.CreateTableAsync<AccountingReport>();
            await _connection.CreateTableAsync<ReportLine>();
            await _connection.CreateTableAsync<ReportLineComputation>();
            await _connection.CreateTableAsync<BillOfMaterial>();
            await _connection.CreateTableAsync<BillOfMaterialLine>();
            await _connection.CreateTableAsync<ManufacturingOrder>();
            await _connection.CreateTableAsync<ManufacturingOrderLine>();
            await _connection.CreateTableAsync<Bank>();
            await _connection.CreateTableAsync<BankAccount>();
            await _connection.CreateTableAsync<PosPaymentMethod>();
            await _connection.CreateTableAsync<SyncState>();
            await _connection.CreateTableAsync<CustomFieldDefinition>();
            await _connection.CreateTableAsync<CustomFieldValue>();
            await _connection.CreateTableAsync<Customer>();
            await _connection.CreateTableAsync<CreditNote>();
            await _connection.CreateTableAsync<DebitNote>();
            await _connection.CreateTableAsync<InvoicePayment>();
            await _connection.CreateTableAsync<BankStatement>();
            await _connection.CreateTableAsync<BankStatementLine>();
            await _connection.CreateTableAsync<ExchangeRate>();
            await _connection.CreateTableAsync<BudgetLine>();
            await _connection.CreateTableAsync<LandedCostCharge>();
            await _connection.CreateTableAsync<CycleCount>();
            await _connection.CreateTableAsync<CycleCountLine>();

            // 3. Perform Schema Migrations
            await PerformMigrationsAsync();

            // 4. Seed Initial Data
            await SeedDataAsync(defaultCurrency);
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
                if (metaVersion < 2)
                {
                    await MigrateToV2Async();
                }

                if (metaVersion < 3)
                {
                    await MigrateToV3Async();
                }

                if (metaVersion < 4)
                {
                    await MigrateToV4Async();
                }

                if (metaVersion < 5)
                {
                    await MigrateToV5Async();
                }

                if (metaVersion < 6)
                {
                    await MigrateToV6Async();
                }

                await _connection.ExecuteAsync($"PRAGMA user_version = {CurrentDatabaseVersion}");
            }
        }

        private async Task MigrateToV2Async()
        {
            var syncTables = new[]
            {
                "Product", "Category", "StockMovement", "Supplier", "SupplierProduct",
                "PurchaseOrder", "PurchaseOrderItem", "SalesOrder", "SalesOrderItem",
                "Tax", "Account", "Journal", "JournalEntry", "JournalLine",
                "ProductBundle", "BillOfMaterial", "BillOfMaterialLine",
                "ManufacturingOrder", "ManufacturingOrderLine",
                "CustomerReturn", "SupplierReturn", "Location", "LocationStock", "StockTransfer"
            };

            foreach (var table in syncTables)
            {
                await AddColumnIfNotExistsAsync(table, "SyncId", "TEXT");
                await AddColumnIfNotExistsAsync(table, "UpdatedAt", "TEXT");
                await AddColumnIfNotExistsAsync(table, "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
            }

            foreach (var table in syncTables)
            {
                await _connection.ExecuteAsync(
                    $"UPDATE {table} SET SyncId = lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab',abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6))) WHERE SyncId IS NULL OR SyncId = ''");
                await _connection.ExecuteAsync(
                    $"UPDATE {table} SET UpdatedAt = datetime('now') WHERE UpdatedAt IS NULL OR UpdatedAt = ''");
            }
        }

        private async Task MigrateToV3Async()
        {
            // Split Customer out of Supplier: every Supplier referenced by SalesOrder.CustomerId
            // gets a mirrored Customer row, and SalesOrder.CustomerId is remapped to it.
            // The Supplier table itself is never mutated or deleted from.
            var existingCustomerCount = await _connection.Table<Customer>().CountAsync();
            if (existingCustomerCount > 0)
            {
                return; // Already migrated (or a fresh install seeded customers) - guard against double-insert.
            }

            var salesOrders = await _connection.Table<SalesOrder>().ToListAsync();
            var supplierIds = salesOrders.Select(so => so.CustomerId).Distinct().ToList();
            if (supplierIds.Count == 0)
            {
                return;
            }

            var supplierToCustomerId = new System.Collections.Generic.Dictionary<int, int>();

            foreach (var supplierId in supplierIds)
            {
                var supplier = await _connection.FindAsync<Supplier>(supplierId);
                if (supplier == null)
                {
                    continue;
                }

                var customer = new Customer
                {
                    Name = supplier.Name,
                    ContactPerson = supplier.ContactPerson,
                    Phone = supplier.Phone,
                    Email = supplier.Email,
                    Address = supplier.Address,
                    PaymentTerms = supplier.PaymentTerms,
                    TinNumber = supplier.TinNumber,
                    WebsiteUrl = supplier.WebsiteUrl,
                    IsActive = supplier.IsActive,
                    CreatedAt = supplier.CreatedAt
                };
                await _connection.InsertAsync(customer);
                supplierToCustomerId[supplierId] = customer.Id;
            }

            foreach (var salesOrder in salesOrders)
            {
                if (supplierToCustomerId.TryGetValue(salesOrder.CustomerId, out var newCustomerId))
                {
                    salesOrder.CustomerId = newCustomerId;
                    await _connection.UpdateAsync(salesOrder);
                }
            }
        }

        private async Task MigrateToV4Async()
        {
            await AddColumnIfNotExistsAsync("User", "PermissionsJson", "TEXT NOT NULL DEFAULT ''");
            await AddColumnIfNotExistsAsync("User", "LastLoginAt", "TEXT");
        }

        private async Task MigrateToV6Async()
        {
            await AddColumnIfNotExistsAsync("PurchaseBatch", "SerialNumber", "TEXT NOT NULL DEFAULT ''");
            await AddColumnIfNotExistsAsync("ReorderRule", "LocationId", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfNotExistsAsync("BillOfMaterial", "YieldPercent", "REAL NOT NULL DEFAULT 100");
            await AddColumnIfNotExistsAsync("BillOfMaterial", "ScrapPercent", "REAL NOT NULL DEFAULT 0");
            await AddColumnIfNotExistsAsync("BillOfMaterialLine", "ScrapPercent", "REAL NOT NULL DEFAULT 0");
        }

        private async Task MigrateToV5Async()
        {
            await AddColumnIfNotExistsAsync("PaymentTerm", "DueDays", "INTEGER NOT NULL DEFAULT 0");

            var terms = await _connection.Table<PaymentTerm>().ToListAsync();
            foreach (var term in terms)
            {
                if (term.DueDays == 0 && !string.IsNullOrWhiteSpace(term.Name))
                {
                    term.DueDays = AgingReportService.ParseDueDays(term.Name);
                    await _connection.UpdateAsync(term);
                }
            }
        }

        private async Task AddColumnIfNotExistsAsync(string table, string column, string definition)
        {
            var columns = await _connection.QueryAsync<TableColumnInfo>($"PRAGMA table_info({table})");
            if (columns.All(c => !string.Equals(c.name, column, StringComparison.OrdinalIgnoreCase)))
            {
                await _connection.ExecuteAsync($"ALTER TABLE {table} ADD COLUMN {column} {definition}");
            }
        }

        private class TableColumnInfo
        {
            public string name { get; set; } = string.Empty;
        }

        public string DatabasePath => _databasePath;

        public async Task CheckpointWalAsync()
        {
            try
            {
                await _connection.ExecuteAsync("PRAGMA wal_checkpoint(FULL);");
            }
            catch
            {
                // Best effort before backup
            }
        }

        public async Task CloseConnectionAsync()
        {
            var connection = _connection;
            _connection = null!;
            if (connection != null)
            {
                await connection.CloseAsync();
            }
        }

        public async Task ReopenConnectionAsync()
        {
            if (_connection == null)
            {
                _connection = new SQLiteAsyncConnection(_databasePath);
                try
                {
                    await _connection.ExecuteAsync("PRAGMA journal_mode = WAL;");
                    await _connection.ExecuteAsync("PRAGMA synchronous = NORMAL;");
                }
                catch { }
            }
        }

        private async Task SeedDataAsync(string defaultCurrency = "RWF")
        {
            // App starts clean for production. No test categories or products seeded.
            // Default admin user is created by UserService.InitializeAsync (hashed password).

            var unitCount = await _connection.Table<ProductUnit>().CountAsync();
            if (unitCount == 0)
            {
                await _connection.InsertAllAsync(new[]
                {
                    new ProductUnit { Name = "Per Unit", Quantity = 1.0, GroupInPOS = false, ReferenceUnit = "" },
                    new ProductUnit { Name = "Pcs", Quantity = 1.0, GroupInPOS = false, ReferenceUnit = "" },
                    new ProductUnit { Name = "Box", Quantity = 12.0, GroupInPOS = false, ReferenceUnit = "Pcs" },
                    new ProductUnit { Name = "g", Quantity = 0.001, GroupInPOS = false, ReferenceUnit = "kg" },
                    new ProductUnit { Name = "kg", Quantity = 1.0, GroupInPOS = false, ReferenceUnit = "" },
                    new ProductUnit { Name = "l", Quantity = 1.0, GroupInPOS = false, ReferenceUnit = "" }
                });
            }

            var locationCount = await _connection.Table<Location>().CountAsync();
            if (locationCount == 0)
            {
                var defaultLocation = new Location
                {
                    Name = "Main Warehouse",
                    Type = "Warehouse",
                    IsActive = true
                };
                await _connection.InsertAsync(defaultLocation);

                var productsWithStock = await _connection.Table<Product>()
                    .Where(p => p.StockQuantity > 0)
                    .ToListAsync();
                foreach (var product in productsWithStock)
                {
                    await _connection.InsertAsync(new LocationStock
                    {
                        LocationId = defaultLocation.Id,
                        ProductId = product.Id,
                        Quantity = product.StockQuantity
                    });
                }
            }

            var accountCount = await _connection.Table<Account>().CountAsync();
            if (accountCount == 0)
            {
                await _connection.InsertAllAsync(new[]
                {
                    // 1. Assets
                    new Account { Code = "101000", Name = "Cash on Hand", Type = "Asset: Bank and Cash", Currency = defaultCurrency, IsActive = true, Description = "Cash register and on-hand currency", PaymentReconciliation = true },
                    new Account { Code = "102000", Name = "Bank Account", Type = "Asset: Bank and Cash", Currency = defaultCurrency, IsActive = true, Description = "Main operating bank account", PaymentReconciliation = true },
                    new Account { Code = "111000", Name = "Accounts Receivable", Type = "Asset: Receivable", Currency = defaultCurrency, IsActive = true, Description = "Unpaid customer invoices", PaymentReconciliation = true },
                    new Account { Code = "120000", Name = "Inventory Asset", Type = "Asset: Current Asset", Currency = defaultCurrency, IsActive = true, Description = "Asset value of products in stock" },
                    new Account { Code = "125000", Name = "VAT Receivable (Input Tax)", Type = "Asset: Current Asset", Currency = defaultCurrency, IsActive = true, Description = "VAT paid on purchases, reclaimable from tax authority" },
                    new Account { Code = "130000", Name = "Prepayments", Type = "Asset: Pre Payments", Currency = defaultCurrency, IsActive = true, Description = "Prepaid vendor expenses" },
                    new Account { Code = "140000", Name = "Fixed Assets", Type = "Asset: Fixed Asset", Currency = defaultCurrency, IsActive = true, Description = "Long-term tangible assets" },
                    
                    // 2. Liabilities
                    new Account { Code = "201000", Name = "Accounts Payable", Type = "Liability: Payable", Currency = defaultCurrency, IsActive = true, Description = "Unpaid vendor bills", PaymentReconciliation = true },
                    new Account { Code = "211000", Name = "Credit Card", Type = "Liability: Credit Card", Currency = defaultCurrency, IsActive = true, Description = "Corporate credit cards", PaymentReconciliation = true },
                    new Account { Code = "220000", Name = "VAT Payable", Type = "Liability: Current Liability", Currency = defaultCurrency, IsActive = true, Description = "Collected sales taxes minus paid purchase taxes" },
                    
                    // 3. Equity
                    new Account { Code = "301000", Name = "Share Capital", Type = "Equity: Equity", Currency = defaultCurrency, IsActive = true, Description = "Initial owner contributions" },
                    new Account { Code = "390000", Name = "Retained Earnings", Type = "Equity: Equity", Currency = defaultCurrency, IsActive = true, Description = "Accumulated earnings from prior periods" },
                    new Account { Code = "399000", Name = "Current Year Earnings", Type = "Equity: Current Year Earnings", Currency = defaultCurrency, IsActive = true, Description = "Earnings/Profit from current financial year" },
                    
                    // 4. Income
                    new Account { Code = "401000", Name = "Product Sales Revenue", Type = "Income: Income", Currency = defaultCurrency, IsActive = true, Description = "Revenue from product sales" },
                    new Account { Code = "402000", Name = "Service Revenue", Type = "Income: Income", Currency = defaultCurrency, IsActive = true, Description = "Revenue from service contracts" },
                    new Account { Code = "490000", Name = "Other Income", Type = "Income: Other Incomes", Currency = defaultCurrency, IsActive = true, Description = "Non-operating revenues" },
                    
                    // 5. Expense
                    new Account { Code = "501000", Name = "Cost of Goods Sold (COGS)", Type = "Expense: Cost of Revenue", Currency = defaultCurrency, IsActive = true, Description = "Direct costs of goods sold to customers" },
                    new Account { Code = "511000", Name = "General Expenses", Type = "Expense: Expenses", Currency = defaultCurrency, IsActive = true, Description = "Operational overhead expenses" },
                    new Account { Code = "520000", Name = "Inventory Adjustment Expense", Type = "Expense: Expenses", Currency = defaultCurrency, IsActive = true, Description = "Expenses from inventory write-offs or adjustments" },
                    new Account { Code = "590000", Name = "Other Expenses", Type = "Expense: Other Expenses", Currency = defaultCurrency, IsActive = true, Description = "Non-operating expenses" }
                });
            }

            var journalCount = await _connection.Table<Journal>().CountAsync();
            if (journalCount == 0)
            {
                var salesAcc  = await _connection.Table<Account>().Where(a => a.Code == "401000").FirstOrDefaultAsync();
                var cogsAcc   = await _connection.Table<Account>().Where(a => a.Code == "501000").FirstOrDefaultAsync();
                var bankAcc   = await _connection.Table<Account>().Where(a => a.Code == "102000").FirstOrDefaultAsync();
                var cashAcc   = await _connection.Table<Account>().Where(a => a.Code == "101000").FirstOrDefaultAsync();

                await _connection.InsertAllAsync(new Journal[]
                {
                    new() { Name = "Sales",                    Type = "Sales",         SequencePrefix = "INV",  DefaultAccountId = salesAcc?.Id, Currency = defaultCurrency },
                    new() { Name = "Purchases",                Type = "Purchase",      SequencePrefix = "BILL", DefaultAccountId = cogsAcc?.Id,  Currency = defaultCurrency },
                    new() { Name = "Bank",                     Type = "Bank",          SequencePrefix = "BNK1", DefaultAccountId = bankAcc?.Id,  Currency = defaultCurrency },
                    new() { Name = "Miscellaneous Operations", Type = "Miscellaneous", SequencePrefix = "MISC",                                  Currency = defaultCurrency },
                    new() { Name = "Exchange Difference",      Type = "Miscellaneous", SequencePrefix = "EXCH",                                  Currency = defaultCurrency },
                    new() { Name = "Cash Basis Taxes",         Type = "Miscellaneous", SequencePrefix = "CABA",                                  Currency = defaultCurrency },
                    new() { Name = "Tax Returns",              Type = "Miscellaneous", SequencePrefix = "TAX",                                   Currency = defaultCurrency },
                    new() { Name = "Inventory Valuation",      Type = "Miscellaneous", SequencePrefix = "STJ",                                   Currency = defaultCurrency },
                    new() { Name = "Point of Sale",            Type = "Miscellaneous", SequencePrefix = "POSS",                                  Currency = defaultCurrency },
                    new() { Name = "Cash Register",            Type = "Cash",          SequencePrefix = "CSH1", DefaultAccountId = cashAcc?.Id,  Currency = defaultCurrency },
                    new() { Name = "Salaries",                 Type = "Miscellaneous", SequencePrefix = "SLR",                                   Currency = defaultCurrency },
                });
            }

            var reportCount = await _connection.Table<AccountingReport>().CountAsync();
            if (reportCount == 0)
            {
                await _connection.InsertAllAsync(new AccountingReport[]
                {
                    new() { Name = "Balance Sheet", RootReport = "Financial Statements" },
                    new() { Name = "Profit and Loss", RootReport = "Financial Statements" },
                    new() { Name = "Cash Flow Statement", RootReport = "Financial Statements" },
                    new() { Name = "Executive Summary", RootReport = "Management Reports" },
                    new() { Name = "Journals Report", RootReport = "Audit Reports" },
                    new() { Name = "Trial Balance", RootReport = "Financial Statements" },
                    new() { Name = "Tax Report", RootReport = "Tax Filings" },
                    new() { Name = "Annual Statement", RootReport = "Annual Filings" }
                });

                // Seed Balance Sheet default configuration
                var bsReport = await _connection.Table<AccountingReport>().Where(r => r.Name == "Balance Sheet").FirstOrDefaultAsync();
                if (bsReport != null)
                {
                    // 1. Assets
                    var assetsLine = new ReportLine { ReportId = bsReport.Id, Name = "Assets", Code = "assets", Level = 1, Foldability = "Foldable" };
                    await _connection.InsertAsync(assetsLine);

                    var currentAssetsLine = new ReportLine { ReportId = bsReport.Id, Name = "Current Assets", Code = "assets_current", Level = 2, Foldability = "Foldable" };
                    await _connection.InsertAsync(currentAssetsLine);

                    var cashLine = new ReportLine { ReportId = bsReport.Id, Name = "Cash and Cash Equivalents", Code = "cash_equiv", Level = 3, Foldability = "Foldable" };
                    await _connection.InsertAsync(cashLine);
                    await _connection.InsertAsync(new ReportLineComputation { ReportLineId = cashLine.Id, Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "101,102" });

                    var arLine = new ReportLine { ReportId = bsReport.Id, Name = "Accounts Receivable", Code = "ar", Level = 3, Foldability = "Foldable" };
                    await _connection.InsertAsync(arLine);
                    await _connection.InsertAsync(new ReportLineComputation { ReportLineId = arLine.Id, Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "111" });

                    var invLine = new ReportLine { ReportId = bsReport.Id, Name = "Inventory Asset", Code = "inventory", Level = 3, Foldability = "Foldable" };
                    await _connection.InsertAsync(invLine);
                    await _connection.InsertAsync(new ReportLineComputation { ReportLineId = invLine.Id, Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "120" });

                    // Sum current assets
                    await _connection.InsertAsync(new ReportLineComputation { ReportLineId = currentAssetsLine.Id, Label = "balance", ComputationEngine = "Sum of other lines", Formula = "cash_equiv+ar+inventory" });

                    // Total Assets Line
                    var totalAssetsLine = new ReportLine { ReportId = bsReport.Id, Name = "Total Assets", Code = "total_assets", Level = 1, Foldability = "Never Foldable" };
                    await _connection.InsertAsync(totalAssetsLine);
                    await _connection.InsertAsync(new ReportLineComputation { ReportLineId = totalAssetsLine.Id, Label = "balance", ComputationEngine = "Sum of other lines", Formula = "assets_current" });

                    // 2. Liabilities
                    var liabilitiesLine = new ReportLine { ReportId = bsReport.Id, Name = "Liabilities", Code = "liabilities", Level = 1, Foldability = "Foldable" };
                    await _connection.InsertAsync(liabilitiesLine);

                    var currentLiabilitiesLine = new ReportLine { ReportId = bsReport.Id, Name = "Current Liabilities", Code = "liabilities_current", Level = 2, Foldability = "Foldable" };
                    await _connection.InsertAsync(currentLiabilitiesLine);

                    var apLine = new ReportLine { ReportId = bsReport.Id, Name = "Accounts Payable", Code = "ap", Level = 3, Foldability = "Foldable" };
                    await _connection.InsertAsync(apLine);
                    await _connection.InsertAsync(new ReportLineComputation { ReportLineId = apLine.Id, Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "201" });

                    var vatLine = new ReportLine { ReportId = bsReport.Id, Name = "VAT Payable", Code = "vat_payable", Level = 3, Foldability = "Foldable" };
                    await _connection.InsertAsync(vatLine);
                    await _connection.InsertAsync(new ReportLineComputation { ReportLineId = vatLine.Id, Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "220" });

                    await _connection.InsertAsync(new ReportLineComputation { ReportLineId = currentLiabilitiesLine.Id, Label = "balance", ComputationEngine = "Sum of other lines", Formula = "ap+vat_payable" });

                    var totalLiabilitiesLine = new ReportLine { ReportId = bsReport.Id, Name = "Total Liabilities", Code = "total_liabilities", Level = 1, Foldability = "Never Foldable" };
                    await _connection.InsertAsync(totalLiabilitiesLine);
                    await _connection.InsertAsync(new ReportLineComputation { ReportLineId = totalLiabilitiesLine.Id, Label = "balance", ComputationEngine = "Sum of other lines", Formula = "liabilities_current" });

                    // 3. Equity
                    var equityLine = new ReportLine { ReportId = bsReport.Id, Name = "Equity", Code = "equity", Level = 1, Foldability = "Foldable" };
                    await _connection.InsertAsync(equityLine);

                    var retainedLine = new ReportLine { ReportId = bsReport.Id, Name = "Retained Earnings", Code = "retained_earnings", Level = 2, Foldability = "Foldable" };
                    await _connection.InsertAsync(retainedLine);
                    await _connection.InsertAsync(new ReportLineComputation { ReportLineId = retainedLine.Id, Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "390,399" });

                    var totalEquityLine = new ReportLine { ReportId = bsReport.Id, Name = "Total Equity", Code = "total_equity", Level = 1, Foldability = "Never Foldable" };
                    await _connection.InsertAsync(totalEquityLine);
                    await _connection.InsertAsync(new ReportLineComputation { ReportLineId = totalEquityLine.Id, Label = "balance", ComputationEngine = "Sum of other lines", Formula = "retained_earnings" });
                }
            } // Close if (reportCount == 0)

            // Seed Profit and Loss default configuration
                var pnlReport = await _connection.Table<AccountingReport>().Where(r => r.Name == "Profit and Loss").FirstOrDefaultAsync();
                if (pnlReport != null)
                {
                    var existingPnlLines = await _connection.Table<ReportLine>().Where(l => l.ReportId == pnlReport.Id).CountAsync();
                    if (existingPnlLines == 0)
                    {
                        // 1. Operating profit
                        var operatingProfitLine = new ReportLine { ReportId = pnlReport.Id, Name = "Operating profit", Code = "pnl_operating_profit", Level = 1, Foldability = "Foldable" };
                        await _connection.InsertAsync(operatingProfitLine);

                        var revenueLine = new ReportLine { ReportId = pnlReport.Id, Name = "Revenue", Code = "pnl_revenue", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(revenueLine);
                        await _connection.InsertAsync(new ReportLineComputation { ReportLineId = revenueLine.Id, Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "401,402" });

                        var otherIncomeLine = new ReportLine { ReportId = pnlReport.Id, Name = "Other income", Code = "pnl_other_income", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(otherIncomeLine);
                        await _connection.InsertAsync(new ReportLineComputation { ReportLineId = otherIncomeLine.Id, Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "490" });

                        var changeInvLine = new ReportLine { ReportId = pnlReport.Id, Name = "Change in inventories", Code = "pnl_change_in_inventories", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(changeInvLine);

                        var cogsLine = new ReportLine { ReportId = pnlReport.Id, Name = "Costs of material", Code = "pnl_cogs", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(cogsLine);
                        await _connection.InsertAsync(new ReportLineComputation { ReportLineId = cogsLine.Id, Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "501" });

                        var employeeLine = new ReportLine { ReportId = pnlReport.Id, Name = "Employee benefits expense", Code = "pnl_employee_benefits", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(employeeLine);

                        var propLine = new ReportLine { ReportId = pnlReport.Id, Name = "Change in fair value of investment property", Code = "pnl_fair_value_property", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(propLine);

                        var deprLine = new ReportLine { ReportId = pnlReport.Id, Name = "Depreciation, amortisation and impairment of non-financial assets", Code = "pnl_depreciation", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(deprLine);

                        var impairmentLine = new ReportLine { ReportId = pnlReport.Id, Name = "Impairment losses of financial assets", Code = "pnl_impairment_financial", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(impairmentLine);

                        var otherExpLine = new ReportLine { ReportId = pnlReport.Id, Name = "Other expenses", Code = "pnl_other_expenses", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(otherExpLine);
                        await _connection.InsertAsync(new ReportLineComputation { ReportLineId = otherExpLine.Id, Label = "balance", ComputationEngine = "Prefix of Account Codes", Formula = "511,520,590" });

                        // Compute Operating profit
                        await _connection.InsertAsync(new ReportLineComputation 
                        { 
                            ReportLineId = operatingProfitLine.Id, 
                            Label = "balance", 
                            ComputationEngine = "Sum of other lines", 
                            Formula = "pnl_revenue+pnl_other_income+pnl_change_in_inventories-pnl_cogs-pnl_employee_benefits-pnl_fair_value_property-pnl_depreciation-pnl_impairment_financial-pnl_other_expenses" 
                        });

                        // 2. Profit before tax
                        var profitBeforeTaxLine = new ReportLine { ReportId = pnlReport.Id, Name = "Profit before tax", Code = "pnl_profit_before_tax", Level = 1, Foldability = "Foldable" };
                        await _connection.InsertAsync(profitBeforeTaxLine);

                        var equityProfitLine = new ReportLine { ReportId = pnlReport.Id, Name = "Share of profit from equity accounted investments", Code = "pnl_equity_profit", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(equityProfitLine);

                        var financeCostsLine = new ReportLine { ReportId = pnlReport.Id, Name = "Finance costs", Code = "pnl_finance_costs", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(financeCostsLine);

                        var financeIncomeLine = new ReportLine { ReportId = pnlReport.Id, Name = "Finance income", Code = "pnl_finance_income", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(financeIncomeLine);

                        var otherFinancialLine = new ReportLine { ReportId = pnlReport.Id, Name = "Other financial items", Code = "pnl_other_financial", Level = 2, Foldability = "Foldable" };
                        await _connection.InsertAsync(otherFinancialLine);

                        // Compute Profit before tax
                        await _connection.InsertAsync(new ReportLineComputation 
                        { 
                            ReportLineId = profitBeforeTaxLine.Id, 
                            Label = "balance", 
                            ComputationEngine = "Sum of other lines", 
                            Formula = "pnl_operating_profit+pnl_equity_profit-pnl_finance_costs+pnl_finance_income+pnl_other_financial" 
                        });

                        // 3. Tax expense
                        var taxExpenseLine = new ReportLine { ReportId = pnlReport.Id, Name = "Tax expense", Code = "pnl_tax_expense", Level = 1, Foldability = "Foldable" };
                        await _connection.InsertAsync(taxExpenseLine);

                        // 4. Profit for the year from continuing operations
                        var continuingOpsProfitLine = new ReportLine { ReportId = pnlReport.Id, Name = "Profit for the year from continuing operations", Code = "pnl_continuing_ops_profit", Level = 1, Foldability = "Foldable" };
                        await _connection.InsertAsync(continuingOpsProfitLine);
                        await _connection.InsertAsync(new ReportLineComputation { ReportLineId = continuingOpsProfitLine.Id, Label = "balance", ComputationEngine = "Sum of other lines", Formula = "pnl_profit_before_tax-pnl_tax_expense" });

                        // 5. Loss for the year from discontinued operations
                        var discontinuedLossLine = new ReportLine { ReportId = pnlReport.Id, Name = "Loss for the year from discontinued operations", Code = "pnl_discontinued_ops_loss", Level = 1, Foldability = "Foldable" };
                        await _connection.InsertAsync(discontinuedLossLine);

                        // 6. Profit/Loss for the year
                        var netProfitYearLine = new ReportLine { ReportId = pnlReport.Id, Name = "Profit/Loss for the year", Code = "pnl_net_profit_year", Level = 1, Foldability = "Never Foldable" };
                        await _connection.InsertAsync(netProfitYearLine);
                        await _connection.InsertAsync(new ReportLineComputation { ReportLineId = netProfitYearLine.Id, Label = "balance", ComputationEngine = "Sum of other lines", Formula = "pnl_continuing_ops_profit-pnl_discontinued_ops_loss" });
                    }
                }

                var termCount = await _connection.Table<PaymentTerm>().CountAsync();
                if (termCount == 0)
                {
                    await _connection.InsertAllAsync(new PaymentTerm[]
                    {
                        new() { Name = "Immediate Payment", Description = "Payment is due immediately", DueDays = 0 },
                        new() { Name = "15 days", Description = "Payment is due within 15 days of invoice date", DueDays = 15 },
                        new() { Name = "21 days", Description = "Payment is due within 21 days of invoice date", DueDays = 21 },
                        new() { Name = "30 days", Description = "Payment is due within 30 days of invoice date", DueDays = 30 }
                    });
                }

                await DemoDataSeeder.SeedIfEmptyAsync(_connection, defaultCurrency);
            }

            public SQLiteAsyncConnection Connection => _connection;
        }
    }
