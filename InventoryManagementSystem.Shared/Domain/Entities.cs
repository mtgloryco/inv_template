using System;
using SQLite;

namespace InventoryManagementSystem.Domain
{
    public class Product : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public string Unit { get; set; } = "Pcs";
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int StockQuantity { get; set; }
        public string Category { get; set; } = "General";

        // New properties for detailed workflow constraints
        public bool CanBeSold { get; set; } = true;
        public bool CanBePurchased { get; set; } = true;
        public bool AvailableInPOS { get; set; } = false;
        public string ProductType { get; set; } = "Good"; // Good, Service, Combo
        public string InvoicingPolicy { get; set; } = "Ordered quantities"; // Ordered quantities, Delivered quantities
        public string Tracking { get; set; } = "by quantity"; // by quantity, lots, by unique serial number
        public int? SalesTaxId { get; set; }
        public int? IncomeAccountId { get; set; }
        public int? ExpenseAccountId { get; set; }
    }

    public class Category : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class StockMovement : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public int ProductId { get; set; }
        public int QuantityChanged { get; set; }
        public string MovementType { get; set; } = "IN"; // IN, OUT, ADJUST
        public DateTime Date { get; set; } = DateTime.Now;
        public string Reason { get; set; } = string.Empty;
        public string Username { get; set; } = "System";
        public decimal UnitPrice { get; set; } // Selling Price for OUT movements

        [Ignore]
        public string BatchTraceInfo { get; set; } = string.Empty;
    }

    public class PurchaseBatch
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int ProductId { get; set; }
        public int QuantityPurchased { get; set; }
        public int QuantityRemaining { get; set; }
        public decimal CostPerUnit { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ExpiryDate { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string QualityStatus { get; set; } = "Good";
    }

    public class LandedCostCharge
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public string CostType { get; set; } = "Freight";
        public decimal Amount { get; set; }
        public DateTime AppliedDate { get; set; } = DateTime.Now;
        public string Reference { get; set; } = string.Empty;
    }

    public class CycleCount : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string CountNumber { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public DateTime CountDate { get; set; } = DateTime.Today;
        public string Status { get; set; } = "Draft";
        public string CreatedByUsername { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class CycleCountLine
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int CycleCountId { get; set; }
        public int ProductId { get; set; }
        public int SystemQuantity { get; set; }
        public int CountedQuantity { get; set; }

        [Ignore]
        public int Variance => CountedQuantity - SystemQuantity;
    }

    public class CycleCountListItem
    {
        public CycleCount CycleCount { get; set; } = new();
        public string LocationName { get; set; } = string.Empty;
        public int LineCount { get; set; }
        public int TotalVarianceUnits { get; set; }
    }

    public class SaleBatchUsage
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int StockMovementId { get; set; }
        public string PurchaseBatchId { get; set; } = string.Empty;
        public int QuantityUsed { get; set; }
        public decimal CostPerUnit { get; set; }
    }

    public class User
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Staff"; // Admin, Staff, Manager, Accountant, Cashier, Guest
        public bool IsActive { get; set; } = true;
        /// <summary>JSON array of permission keys. When set, overrides role defaults for module access.</summary>
        public string PermissionsJson { get; set; } = string.Empty;
        public DateTime? LastLoginAt { get; set; }
    }

    public class LocalLicense
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string LicenseToken { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
        public DateTime ExpirationDate { get; set; }
        public DateTime LastValidatedAt { get; set; }
        public DateTime LastKnownValidDate { get; set; } // To detect clock manipulation
        public string Status { get; set; } = "Free"; // Free, Active, Expired, HardwareMismatch, InvalidSignature
        public string Type { get; set; } = "Free"; // Free, Premium
    }

    public class Supplier : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int DefaultLeadTimeDays { get; set; }
        public string PaymentTerms { get; set; } = "Direct Payment";
        public string SupplierType { get; set; } = "Company";
        public string TinNumber { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public string LogoFileName { get; set; } = string.Empty;
        public decimal Rating { get; set; } = 3m;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class SupplierProduct : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public int SupplierId { get; set; }
        public int ProductId { get; set; }
    }

    public class PurchaseOrder : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string PONumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string Status { get; set; } = "Draft"; // "Draft" (RFQ), "Approved" (PO), etc.
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? ExpectedDeliveryDate { get; set; }
        public DateTime? ActualDeliveryDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string CreatedByUsername { get; set; } = string.Empty;
        public string ApprovedByUsername { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }

        // New properties for RFQ / Purchasing flow
        public string Currency { get; set; } = "USD";
        public string PaymentTerms { get; set; } = "Immediate Payment";
        public DateTime? OrderDeadline { get; set; }
        public string Buyer { get; set; } = string.Empty;
        public string Company { get; set; } = "My Company";

        // New properties for Billing & Receipt status
        public string BillingStatus { get; set; } = "Waiting Bill"; // "Waiting Bill", "Billed"
        public string ReceiptStatus { get; set; } = "Pending"; // "Pending", "Received"
        public bool IsArchived { get; set; } = false;
    }

    public class PurchaseOrderItem : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public int PurchaseOrderId { get; set; }
        public int ProductId { get; set; }
        public int QuantityOrdered { get; set; }
        public int QuantityReceived { get; set; }
        public int QuantityBilled { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost => QuantityOrdered * UnitCost;
        
        // Associated Purchase tax
        public int? TaxId { get; set; }
    }

    public class PurchaseOrderListItem
    {
        public PurchaseOrder PurchaseOrder { get; set; } = new();
        public string SupplierName { get; set; } = string.Empty;
    }

    public class SupplierPerformance
    {
        public int SupplierId { get; set; }
        public int TotalOrders { get; set; }
        public decimal OnTimeDeliveryPercent { get; set; }
        public double AverageLeadTimeDays { get; set; }
    }

    public class ReorderRule
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int ProductId { get; set; }
        public int LocationId { get; set; }
        public int PreferredSupplierId { get; set; }

        public int ReorderPoint { get; set; }
        public int ReorderQuantity { get; set; }
        public int LeadTimeDays { get; set; }
        public int SafetyStockDays { get; set; }
        public bool AutoCreatePO { get; set; } = false;
    }

    // --- PHASE 2: MULTI-LOCATION ---

    public class Location : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Warehouse"; // Warehouse, Store, Vehicle, External
        public string Address { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class LocationStock : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public int LocationId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public int ReorderPoint { get; set; }
    }

    public class StockTransfer : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public int FromLocationId { get; set; }
        public int ToLocationId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, InTransit, Completed, Cancelled
        public DateTime RequestedDate { get; set; } = DateTime.Now;
        public DateTime? CompletedDate { get; set; }
        public string RequestedByUsername { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    // --- PHASE 5: RETURNS & REFUNDS ---
    // (Adding these since they weren't found despite user's claim)

    public class CustomerReturn : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Condition { get; set; } = "Resaleable"; // Resaleable, Damaged, Destroyed
        public decimal RefundAmount { get; set; }
        public string ProcessedByUsername { get; set; } = string.Empty;
        public DateTime ReturnDate { get; set; } = DateTime.Now;
        public string OriginalReceiptId { get; set; } = string.Empty;
        public string Resolution { get; set; } = "Restocked"; // Restocked, Returned to Supplier, Written Off
    }

    public class SupplierReturn : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Shipped, Credited
        public decimal CreditAmount { get; set; }
        public string ProcessedByUsername { get; set; } = string.Empty;
        public string OriginalReceiptId { get; set; } = string.Empty;
        public DateTime ReturnDate { get; set; } = DateTime.Now;
    }

    // --- PHASE 7: KITTING & BUNDLES ---

    public class ProductBundle : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public int ParentProductId { get; set; }       // The bundle SKU
        public int ComponentProductId { get; set; }    // A component inside the bundle
        public int QuantityRequired { get; set; }      // How many of this component per bundle
    }

    // --- PHASE 8: AUDIT TRAIL ---

    public class AuditLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty; // "Product", "User", "PurchaseOrder", etc.
        public int EntityId { get; set; }
        public string Action { get; set; } = string.Empty;     // Created, Updated, Deleted, Approved, etc.
        public string ChangedByUsername { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string OldValues { get; set; } = string.Empty;  // JSON snapshot of before
        public string NewValues { get; set; } = string.Empty;  // JSON snapshot of after
        public string IpAddress { get; set; } = string.Empty;
    }

    public class Tax : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Computation { get; set; } = "Percentage"; // Percentage, Fixed
        public decimal Amount { get; set; }
        public string TaxType { get; set; } = "Sales"; // Sales, Purchases
        public string Description { get; set; } = string.Empty;
        public string LabelOnInvoice { get; set; } = string.Empty;
        public string Scope { get; set; } = "Goods"; // Goods, Services
        public string IncludedInPrice { get; set; } = "Exclude"; // Include, Exclude
        public bool IsActive { get; set; } = true;
        
        // Associated ledger account for tax recording
        public int? AccountId { get; set; }
    }

    public class ProductUnit
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Quantity { get; set; } = 1.0;
        public bool GroupInPOS { get; set; } = false;
        public string ReferenceUnit { get; set; } = string.Empty;
    }

    public class Account : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        [Unique]
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty; // e.g. "Asset: Receivable", "Asset: Bank and Cash"

        public int? DefaultTaxId { get; set; }

        public string Currency { get; set; } = "RWF";

        public bool IsActive { get; set; } = true;

        public string Description { get; set; } = string.Empty;

        public bool PaymentReconciliation { get; set; } = false;
    }

    public class Journal : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = "Miscellaneous"; // Sales, Purchase, Cash, Bank, Credit Card, Miscellaneous

        public string SequencePrefix { get; set; } = string.Empty;

        public int? DefaultAccountId { get; set; }
        
        // Bank Journal specific clearing accounts (linked to CoA Accounts)
        public int? BankAccountId { get; set; }
        public int? SuspenseAccountId { get; set; }
        public int? ProfitAccountId { get; set; }
        public int? LossAccountId { get; set; }
        public int? OutstandingReceiptsAccountId { get; set; }
        public int? OutstandingPaymentsAccountId { get; set; }

        // Linked real-world Bank Account (from settings)
        public int? LinkedBankAccountId { get; set; }

        public string Currency { get; set; } = "RWF";

        public string PaymentCommunicationType { get; set; } = "Based on Invoice"; // Based on Invoice, Based on Customer

        public string PaymentCommunicationStandard { get; set; } = string.Empty;
    }

    public class JournalListItem
    {
        public Journal Journal { get; set; } = new();
        public string DefaultAccountDisplay { get; set; } = string.Empty;
        public string LinkedBankName { get; set; } = string.Empty;
        public string LinkedBankAccountNumber { get; set; } = string.Empty;
    }

    public class JournalEntry : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public int JournalId { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public string Reference { get; set; } = string.Empty;
        public string State { get; set; } = "Posted"; // Draft, Posted
    }

    public class JournalLine : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public int JournalEntryId { get; set; }
        public int AccountId { get; set; }
        public int? ProductId { get; set; } // Track transaction per product
        public string Label { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class AccountingReport
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RootReport { get; set; } = string.Empty;
    }

    public class ReportLine
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int ReportId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int Level { get; set; } = 1;
        public string Foldability { get; set; } = "Foldable"; // Foldable, Always Expanded, Never Foldable
        public string GroupBy { get; set; } = "Use report's 'Group By'";
        public bool PrintOnNewPage { get; set; } = false;
        public bool HideIfZero { get; set; } = false;
    }

    public class ReportLineComputation
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int ReportLineId { get; set; }
        public string Label { get; set; } = "balance";
        public string ComputationEngine { get; set; } = "Prefix of Account Codes"; // Prefix of Account Codes, Sum of other lines, Custom SQL
        public string Formula { get; set; } = string.Empty;
        public string Subformula { get; set; } = string.Empty;
    }

    public class SalesOrder : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string SONumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string Status { get; set; } = "Draft"; // "Draft" (Quotation), "Confirmed" (Sales Order), "Delivered", "Cancelled"
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime QuotationDate { get; set; } = DateTime.Now;
        public DateTime? ExpirationDate { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public string PaymentTerms { get; set; } = "Immediate Payment";
        public string Notes { get; set; } = string.Empty;
        public string CreatedByUsername { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public bool IsTaxInclusive { get; set; } = false;
        public string BillingStatus { get; set; } = "Waiting Invoice"; // Waiting Invoice, Invoiced
        public bool IsArchived { get; set; } = false;
        public string Company { get; set; } = "My Company";
        public string Currency { get; set; } = "RWF";
        public string DeliveryStatus { get; set; } = "Pending"; // Pending, Delivered, Partially Delivered
        public bool IsPosSale { get; set; } = false;
        public int? PosPaymentMethodId { get; set; }
    }

    public class SalesOrderItem : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public int SalesOrderId { get; set; }
        public int ProductId { get; set; }
        public int QuantityOrdered { get; set; }
        public int QuantityDelivered { get; set; }
        public int QuantityInvoiced { get; set; }
        public decimal UnitPrice { get; set; }
        public int? TaxId { get; set; }
    }

    public class SalesOrderListItem
    {
        public SalesOrder SalesOrder { get; set; } = new();
        public string CustomerName { get; set; } = string.Empty;

        public bool CanDeliver => SalesOrder != null && SalesOrder.DeliveryStatus != "Delivered";
        public bool CanInvoice => SalesOrder != null && SalesOrder.BillingStatus != "Invoiced";
    }

    public class PaymentTerm
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Unique]
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>Days after invoice/bill date until payment is due.</summary>
        public int DueDays { get; set; }
    }

    public class InvoicePayment : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        /// <summary>SalesOrder or PurchaseOrder</summary>
        public string DocumentType { get; set; } = string.Empty;
        public int DocumentId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "RWF";
        public DateTime PaymentDate { get; set; } = DateTime.Now;
        public string PaymentMethod { get; set; } = "Bank";
        public int? BankAccountId { get; set; }
        public int? BankStatementLineId { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string CreatedByUsername { get; set; } = string.Empty;
    }

    public class BankStatement
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int BankAccountId { get; set; }
        public DateTime StatementDate { get; set; } = DateTime.Today;
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public string Reference { get; set; } = string.Empty;
        public DateTime ImportedAt { get; set; } = DateTime.Now;
    }

    public class BankStatementLine
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int BankStatementId { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Today;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        public bool IsReconciled { get; set; }
        public int? MatchedPaymentId { get; set; }
    }

    public class ExchangeRate
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string FromCurrency { get; set; } = string.Empty;
        public string ToCurrency { get; set; } = string.Empty;
        public decimal Rate { get; set; } = 1m;
        public DateTime EffectiveDate { get; set; } = DateTime.Today;
    }

    public class BudgetLine
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int FiscalYear { get; set; }
        public int AccountId { get; set; }
        /// <summary>1-12 for monthly budgets; 0 for annual total.</summary>
        public int PeriodMonth { get; set; }
        public decimal BudgetAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class VatReturnSummary
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal OutputVat { get; set; }
        public decimal InputVat { get; set; }
        public decimal NetVatPayable => OutputVat - InputVat;
        public decimal TaxableSales { get; set; }
        public decimal TaxablePurchases { get; set; }
    }

    public class BudgetVsActualLine
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal BudgetAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal Variance => ActualAmount - BudgetAmount;
        public decimal VariancePercent => BudgetAmount == 0 ? 0 : Math.Round(Variance / BudgetAmount * 100, 1);
    }

    public class ReconciliationCandidate
    {
        public InvoicePayment? Payment { get; set; }
        public BankStatementLine? StatementLine { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public bool IsMatched { get; set; }
    }

    public class BillOfMaterial : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public int ProductId { get; set; }
        public double Quantity { get; set; } = 1.0;
        public string Reference { get; set; } = string.Empty;
        public string BomType { get; set; } = "Manufacture this product";
        public string Company { get; set; } = "My Company";
        /// <summary>Expected finished-goods yield percent (e.g. 95 = 95% output).</summary>
        public double YieldPercent { get; set; } = 100.0;
        /// <summary>Overall scrap allowance on the finished product run.</summary>
        public double ScrapPercent { get; set; } = 0.0;
    }

    public class BillOfMaterialLine : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public int BillOfMaterialId { get; set; }
        public int ProductId { get; set; }
        public double Quantity { get; set; } = 1.0;
        public string Unit { get; set; } = "Pcs";
        /// <summary>Component scrap percent added on top of base BOM quantity.</summary>
        public double ScrapPercent { get; set; } = 0.0;
    }

    public class BillOfMaterialListItem
    {
        public BillOfMaterial BillOfMaterial { get; set; } = new();
        public string ProductName { get; set; } = string.Empty;
        public string Reference => BillOfMaterial.Reference;
        public string BomType => BillOfMaterial.BomType;
        public string Company => BillOfMaterial.Company;
    }

    public class ManufacturingOrder : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string MONumber { get; set; } = string.Empty;
        public int BomId { get; set; }
        public int ProductId { get; set; }
        public double TargetQuantity { get; set; } = 1.0;
        public double ActualQuantity { get; set; } = 0.0;
        public string Status { get; set; } = "Draft"; // Draft, Confirmed, Done
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? ProduceDate { get; set; }
        public decimal TotalCost { get; set; } = 0m;
        public string Company { get; set; } = "My Company";
    }

    public class ManufacturingOrderLine : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public int ManufacturingOrderId { get; set; }
        public int ProductId { get; set; }
        public double ExpectedQuantity { get; set; }
        public double ActualQuantity { get; set; }
        public string Unit { get; set; } = "Pcs";
        public decimal UnitCost { get; set; } = 0m;
    }

    public class ManufacturingOrderListItem
    {
        public ManufacturingOrder ManufacturingOrder { get; set; } = new();
        public string ProductName { get; set; } = string.Empty;
        
        public string MONumber => ManufacturingOrder.MONumber;
        public string TargetQuantityDisplay => $"{ManufacturingOrder.TargetQuantity:F2}";
        public string ActualQuantityDisplay => ManufacturingOrder.Status == "Done" ? $"{ManufacturingOrder.ActualQuantity:F2}" : "-";
        public string Status => ManufacturingOrder.Status;
        public string OrderDateDisplay => ManufacturingOrder.OrderDate.ToString("yyyy-MM-dd HH:mm");
        public string TotalCostDisplay => ManufacturingOrder.Status == "Done" ? $"{ManufacturingOrder.TotalCost:N2}" : "-";
        public string Company => ManufacturingOrder.Company;
    }

    public class Bank
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class BankAccount
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountHolder { get; set; } = string.Empty;
        public int BankId { get; set; }
        public string Currency { get; set; } = "RWF";
        public bool SendMoney { get; set; } = false;
    }

    public class PosPaymentMethod
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int JournalId { get; set; }
    }

    // --- CROSS-INDUSTRY CONFIGURABILITY: CUSTOM FIELDS ---

    public class CustomFieldDefinition
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty; // Product, Customer, Supplier, SalesOrder, PurchaseOrder
        public string FieldKey { get; set; } = string.Empty;
        public string FieldLabel { get; set; } = string.Empty;
        public string FieldType { get; set; } = "Text"; // Text, Number, Date, Boolean, Choice
        public string ChoiceOptions { get; set; } = string.Empty; // comma-separated, only for FieldType == "Choice"
        public bool IsRequired { get; set; } = false;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CustomFieldValue
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int DefinitionId { get; set; }
        public int EntityId { get; set; }
        public string ValueText { get; set; } = string.Empty;
    }

    // --- CROSS-INDUSTRY CONFIGURABILITY: CUSTOMER ENTITY ---

    public class Customer : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PaymentTerms { get; set; } = "Direct Payment";
        public string TinNumber { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    // --- PHASE 1: FORMAL CREDIT / DEBIT DOCUMENTS ---

    public class CreditNote : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string CreditNoteNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public int? CustomerReturnId { get; set; }
        public int? SalesOrderId { get; set; }
        public decimal Amount { get; set; }
        public DateTime IssueDate { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Posted";
        public string Reason { get; set; } = string.Empty;
        public string CreatedByUsername { get; set; } = string.Empty;
    }

    public class DebitNote : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string DebitNoteNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public int? SupplierReturnId { get; set; }
        public int? PurchaseOrderId { get; set; }
        public decimal Amount { get; set; }
        public DateTime IssueDate { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Posted";
        public string Reason { get; set; } = string.Empty;
        public string CreatedByUsername { get; set; } = string.Empty;
    }

    public class AgingLine
    {
        public string PartnerName { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal OpenBalance { get; set; }
        public int DaysOverdue { get; set; }
        public string AgingBucket { get; set; } = "Current";
    }

    public class AgingSummary
    {
        public decimal Current { get; set; }
        public decimal Days1To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Over90 { get; set; }
        public decimal TotalOpen { get; set; }
    }

    public class WebhookEndpoint
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TargetUrl { get; set; } = string.Empty;
        /// <summary>Comma-separated event types, e.g. sales.created,payment.received</summary>
        public string EventTypes { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class WebhookDeliveryLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int WebhookEndpointId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public int HttpStatusCode { get; set; }
        public string ResponseBody { get; set; } = string.Empty;
        public bool Success { get; set; }
        public DateTime DeliveredAt { get; set; } = DateTime.UtcNow;
    }

    public class NotificationOutbox
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Channel { get; set; } = "Email";
        public string Recipient { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string ReferenceType { get; set; } = string.Empty;
        public int? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class SyncConflictLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public Guid SyncId { get; set; }
        public string LocalPayloadJson { get; set; } = string.Empty;
        public string ServerPayloadJson { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public string Resolution { get; set; } = "Pending";
        public DateTime? ResolvedAt { get; set; }
    }

    public class CategoryMarginLine
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal MarginPercent { get; set; }
        public int ProductCount { get; set; }
    }

    public class AbcAnalysisLine
    {
        public string SKU { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Classification { get; set; } = "C";
        public decimal Revenue { get; set; }
        public decimal RevenueSharePercent { get; set; }
    }

    public class MonthCloseSummary
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public bool IsBalanced => Math.Abs(TotalDebits - TotalCredits) < 0.01m;
        public decimal NetProfit { get; set; }
        public int PostedEntryCount { get; set; }
        public int OpenArCount { get; set; }
        public int OpenApCount { get; set; }
    }

    // --- PHASE 5: ENTERPRISE TIER ---

    public class CompanyBranch : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int? ParentBranchId { get; set; }
        public string Address { get; set; } = string.Empty;
        public string Currency { get; set; } = "RWF";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ConsolidatedBranchLine
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string BranchCode { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal StockValue { get; set; }
        public int LocationCount { get; set; }
        public int OpenSalesOrders { get; set; }
    }

    public class ApprovalRequest
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        /// <summary>PurchaseOrder, Discount, WriteOff</summary>
        public string RequestType { get; set; } = string.Empty;
        public string ReferenceType { get; set; } = string.Empty;
        public int ReferenceId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending";
        public string RequestedByUsername { get; set; } = string.Empty;
        public string ReviewedByUsername { get; set; } = string.Empty;
        public string RequestNotes { get; set; } = string.Empty;
        public string ReviewNotes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
    }

    public class WorkCenter
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public double HoursPerDay { get; set; } = 8.0;
        public double EfficiencyPercent { get; set; } = 100.0;
        public bool IsActive { get; set; } = true;
    }

    public class MrpPlannedOrder
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int ProductId { get; set; }
        /// <summary>Purchase or Manufacturing</summary>
        public string OrderType { get; set; } = "Purchase";
        public double Quantity { get; set; }
        public DateTime PlannedStartDate { get; set; } = DateTime.Today;
        public DateTime PlannedEndDate { get; set; } = DateTime.Today.AddDays(7);
        public string Status { get; set; } = "Planned";
        public string SourceReference { get; set; } = string.Empty;
        public int? WorkCenterId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class MrpCapacityLine
    {
        public int WorkCenterId { get; set; }
        public string WorkCenterName { get; set; } = string.Empty;
        public double AvailableHours { get; set; }
        public double ScheduledHours { get; set; }
        public double UtilizationPercent { get; set; }
        public bool IsOverloaded { get; set; }
    }

    public class CrmOpportunity : ISyncableEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public string Title { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        /// <summary>Lead, Qualified, Proposal, Won, Lost</summary>
        public string Stage { get; set; } = "Lead";
        public decimal ExpectedRevenue { get; set; }
        public int ProbabilityPercent { get; set; } = 10;
        public int? SalesOrderId { get; set; }
        public string AssignedToUsername { get; set; } = string.Empty;
        public DateTime? ExpectedCloseDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CrmOpportunityListItem
    {
        public CrmOpportunity Opportunity { get; set; } = new();
        public string CustomerName { get; set; } = string.Empty;
        public string QuotationNumber { get; set; } = string.Empty;
    }

    public class MobileDeviceRegistration
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        /// <summary>Warehouse or FieldSales</summary>
        public string DeviceType { get; set; } = "Warehouse";
        public string ApiKeyHash { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastSyncAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class MobileSyncQueue
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
    }

    public class SecurityPolicy
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public bool EnableEncryptionAtRest { get; set; }
        public int MinPasswordLength { get; set; } = 8;
        public bool RequireMfa { get; set; }
        public string SsoProvider { get; set; } = string.Empty;
        public string SsoClientId { get; set; } = string.Empty;
        public int BackupRetentionDays { get; set; } = 30;
        public int BackupSlaHours { get; set; } = 24;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedByUsername { get; set; } = string.Empty;
    }

    public class BackupSlaLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string BackupType { get; set; } = "Cloud";
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public bool Success { get; set; }
        public long SizeBytes { get; set; }
        public bool WithinSla { get; set; }
    }
}

