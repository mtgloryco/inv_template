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
}
