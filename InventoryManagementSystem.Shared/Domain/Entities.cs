using System;
using SQLite;

namespace InventoryManagementSystem.Domain
{
    public class Product
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public string Unit { get; set; } = "Pcs";
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int StockQuantity { get; set; }
        public string Category { get; set; } = "General";
    }

    public class Category
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class StockMovement
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
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

    public class Supplier
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
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

    public class SupplierProduct
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public int ProductId { get; set; }
    }

    public class PurchaseOrder
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string PONumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string Status { get; set; } = "Draft";
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? ExpectedDeliveryDate { get; set; }
        public DateTime? ActualDeliveryDate { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string CreatedByUsername { get; set; } = string.Empty;
        public string ApprovedByUsername { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }

    public class PurchaseOrderItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public int ProductId { get; set; }
        public int QuantityOrdered { get; set; }
        public int QuantityReceived { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost => QuantityOrdered * UnitCost;
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

    public class Location
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Warehouse"; // Warehouse, Store, Vehicle, External
        public string Address { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class LocationStock
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int LocationId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public int ReorderPoint { get; set; }
    }

    public class StockTransfer
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
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

    public class CustomerReturn
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
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

    public class SupplierReturn
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Shipped, Credited
        public decimal CreditAmount { get; set; }
        public string ProcessedByUsername { get; set; } = string.Empty;
        public DateTime ReturnDate { get; set; } = DateTime.Now;
    }

    // --- PHASE 7: KITTING & BUNDLES ---

    public class ProductBundle
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
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

    public class Tax
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Computation { get; set; } = "Percentage"; // Percentage, Fixed
        public decimal Amount { get; set; }
        public string TaxType { get; set; } = "Sales"; // Sales, Purchases
        public string Description { get; set; } = string.Empty;
        public string LabelOnInvoice { get; set; } = string.Empty;
        public string Scope { get; set; } = "Goods"; // Goods, Services
        public string IncludedInPrice { get; set; } = "Exclude"; // Include, Exclude
        public bool IsActive { get; set; } = true;
    }
}
