# Cloud Sync Setup

This document describes the offline-first cloud sync architecture for Inventory Management System (IMS).

## Architecture

- **Local:** SQLite remains the on-device cache (`%LocalAppData%/InventoryManagementSystem/inventory.db`).
- **Cloud:** ASP.NET Core API in `InventoryManagementSystem.Cloud/` with **organization/workspace tenancy**.
- **Sync modes:**
  1. **Backup/restore** — compressed SQLite snapshot (Phase 1)
  2. **Delta sync** — push/pull changes by `SyncId`, `UpdatedAt`, `IsDeleted` (Phase 2+)

All cloud data is scoped by `organizationId`. JWT tokens embed the user's organization.

## Run the cloud backend (development)

```bash
cd InventoryManagementSystem.Cloud
dotnet run
```

Default URL: `http://localhost:5080`

Health check: `GET http://localhost:5080/health`

### Dev storage

- **Metadata DB:** SQLite file `cloud.db` in the Cloud project folder (default)
- **Backups:** `backups/{organizationId}/inventory.db.gz`

### Production (Postgres)

Set `DATABASE_URL` to a Postgres connection string (Neon, Supabase, Railway, etc.):

```bash
export DATABASE_URL="postgresql://user:pass@host:5432/ims_cloud"
export Jwt__Key="your-production-secret-at-least-32-characters"
dotnet run --project InventoryManagementSystem.Cloud
```

## Environment variables

| Variable | Where | Purpose |
|----------|-------|---------|
| `IMS_CLOUD_API_URL` | Desktop client | Cloud API base URL (default `http://localhost:5080`) |
| `DATABASE_URL` | Cloud backend | Postgres connection (optional; SQLite used if unset) |
| `Jwt__Key` | Cloud backend | JWT signing key (override dev default in production) |
| `BackupStoragePath` | Cloud backend | Folder for gzip backups (default `backups`) |

## Desktop client setup

1. Activate an **Enterprise** license (cloud sync is gated by `LicenseService.CanAccessCloudSync()`).
2. Ensure the cloud API is running and reachable.
3. Optionally set the API URL before starting the app:

   ```bash
   export IMS_CLOUD_API_URL=http://localhost:5080
   ```

4. In the sidebar **Cloud Sync** panel:
   - Enter email, password, and organization name
   - Click **Connect / Register** (creates org + user on first run)
   - Use **Sync Now** for delta sync
   - Use **Backup** / **Restore** for full database snapshot

## API endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/register` | No | Create organization + user |
| POST | `/api/auth/login` | No | Login, receive JWT |
| GET | `/api/backup/info` | JWT | Backup metadata |
| POST | `/api/backup` | JWT | Upload gzip SQLite backup |
| GET | `/api/backup` | JWT | Download gzip backup |
| POST | `/api/sync/push` | JWT | Push delta changes |
| GET | `/api/sync/pull?since=` | JWT | Pull changes since timestamp |

## Test cloud sync manually

### 1. Register and login (curl)

```bash
curl -s -X POST http://localhost:5080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"owner@shop.com","password":"Secret123!","organizationName":"Demo Shop"}'
```

Save the `token` from the response.

### 2. Push a test change

```bash
TOKEN="your-jwt-here"
curl -s -X POST http://localhost:5080/api/sync/push \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId":"test-device",
    "changes":[{
      "entityType":"Product",
      "syncId":"11111111-1111-1111-1111-111111111111",
      "payloadJson":"{\"SyncId\":\"11111111-1111-1111-1111-111111111111\",\"Name\":\"Cloud Widget\",\"SKU\":\"CW-1\",\"Unit\":\"Pcs\",\"Price\":10,\"Cost\":5,\"StockQuantity\":0,\"Category\":\"General\",\"UpdatedAt\":\"2026-07-05T00:00:00Z\",\"IsDeleted\":false}",
      "updatedAt":"2026-07-05T00:00:00Z",
      "isDeleted":false
    }]
  }'
```

### 3. Pull changes

```bash
curl -s "http://localhost:5080/api/sync/pull?since=1970-01-01T00:00:00Z" \
  -H "Authorization: Bearer $TOKEN"
```

### 4. Two-device flow

1. Device A: connect, **Backup**, edit data, **Sync Now**
2. Device B: connect with same account, **Restore** (full copy) or **Sync Now** (delta)

## Sync metadata (local DB v2)

Major entities include:

- `SyncId` (Guid) — stable global identifier
- `UpdatedAt` (UTC) — last modification time
- `IsDeleted` — soft delete flag

Local `SyncState` table stores device ID, auth token, last push/pull times, and sync status.

## Synced entity types

**Core:** Product, StockMovement, Supplier, PurchaseOrder, PurchaseOrderItem, SalesOrder, SalesOrderItem

**Extended:** Category, Tax, SupplierProduct, Account, Journal, JournalEntry, JournalLine, ProductBundle, BillOfMaterial, BillOfMaterialLine, ManufacturingOrder, ManufacturingOrderLine, CustomerReturn, SupplierReturn, Location, LocationStock, StockTransfer

## Build and test

```bash
dotnet build
dotnet test
```

## Tenancy model

Each user belongs to exactly one **organization/workspace**. All sync records and backups are partitioned by `organizationId`. This supports single-shop deployments today and multi-tenant SaaS later without schema changes.
