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
        public string QualityStatus { get; set; } = "Good";
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
        public string Role { get; set; } = "Staff"; // Admin, Staff
        public bool IsActive { get; set; } = true;
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
}

