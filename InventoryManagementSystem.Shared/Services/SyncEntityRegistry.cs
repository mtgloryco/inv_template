using System;
using System.Collections.Generic;
using InventoryManagementSystem.Domain;

namespace InventoryManagementSystem.Services
{
    /// <summary>
    /// Registry of entity types participating in cloud delta sync.
    /// Core tables first; expanded types included for full org-scoped sync.
    /// </summary>
    public static class SyncEntityRegistry
    {
        public static IReadOnlyList<SyncEntityDescriptor> Core { get; } = new[]
        {
            Descriptor<Product>("Product"),
            Descriptor<StockMovement>("StockMovement"),
            Descriptor<Supplier>("Supplier"),
            Descriptor<PurchaseOrder>("PurchaseOrder"),
            Descriptor<PurchaseOrderItem>("PurchaseOrderItem"),
            Descriptor<SalesOrder>("SalesOrder"),
            Descriptor<SalesOrderItem>("SalesOrderItem"),
        };

        public static IReadOnlyList<SyncEntityDescriptor> Expanded { get; } = new[]
        {
            Descriptor<Category>("Category"),
            Descriptor<Tax>("Tax"),
            Descriptor<SupplierProduct>("SupplierProduct"),
            Descriptor<Account>("Account"),
            Descriptor<Journal>("Journal"),
            Descriptor<JournalEntry>("JournalEntry"),
            Descriptor<JournalLine>("JournalLine"),
            Descriptor<ProductBundle>("ProductBundle"),
            Descriptor<BillOfMaterial>("BillOfMaterial"),
            Descriptor<BillOfMaterialLine>("BillOfMaterialLine"),
            Descriptor<ManufacturingOrder>("ManufacturingOrder"),
            Descriptor<ManufacturingOrderLine>("ManufacturingOrderLine"),
            Descriptor<CustomerReturn>("CustomerReturn"),
            Descriptor<SupplierReturn>("SupplierReturn"),
            Descriptor<Location>("Location"),
            Descriptor<LocationStock>("LocationStock"),
            Descriptor<StockTransfer>("StockTransfer"),
        };

        public static IReadOnlyList<SyncEntityDescriptor> All { get; } = BuildAll();

        private static IReadOnlyList<SyncEntityDescriptor> BuildAll()
        {
            var all = new List<SyncEntityDescriptor>();
            all.AddRange(Core);
            all.AddRange(Expanded);
            return all;
        }

        private static SyncEntityDescriptor Descriptor<T>(string name) where T : ISyncableEntity, new() =>
            new(name, typeof(T));
    }

    public sealed class SyncEntityDescriptor
    {
        public SyncEntityDescriptor(string entityType, Type clrType)
        {
            EntityType = entityType;
            ClrType = clrType;
        }

        public string EntityType { get; }
        public Type ClrType { get; }
    }
}
