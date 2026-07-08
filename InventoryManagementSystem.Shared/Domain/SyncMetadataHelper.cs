using System;

namespace InventoryManagementSystem.Domain
{
    public static class SyncMetadataHelper
    {
        public static void Touch(ISyncableEntity entity)
        {
            if (entity.SyncId == Guid.Empty)
            {
                entity.SyncId = Guid.NewGuid();
            }

            entity.UpdatedAt = DateTime.UtcNow;
        }

        public static void MarkDeleted(ISyncableEntity entity)
        {
            entity.IsDeleted = true;
            Touch(entity);
        }
    }
}
