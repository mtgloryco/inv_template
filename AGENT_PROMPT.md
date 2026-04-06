# Enterprise IMS Upgrade - Agent Implementation Prompt

## YOUR ROLE
You are a senior C#/.NET engineer. Your job is to upgrade an existing Inventory Management System
from a basic CRUD app into a full enterprise-grade solution. You will implement features in phases.
Do not rewrite what already works. Extend and add on top of the existing architecture.

---

## EXISTING SYSTEM OVERVIEW

**Tech Stack**: Avalonia UI, C#, .NET 8, SQLite (SQLite-net-pcl), MVVM (CommunityToolkit.Mvvm)
**Architecture**: Shared library (InventoryManagementSystem.Shared) + Desktop launcher (InventoryManagementSystem.Desktop)
**Pattern**: Services → ViewModels → Views (AXAML)

**Existing Entities** (in Domain/Entities.cs):
- Product (Id, Name, SKU, Unit, Price, Cost, StockQuantity, Category)
- StockMovement (Id, ProductId, QuantityChanged, MovementType, Date, Reason, Username, UnitPrice)
- PurchaseBatch (Id, ProductId, QuantityPurchased, QuantityRemaining, CostPerUnit, PurchaseDate)
- SaleBatchUsage (StockMovementId, PurchaseBatchId, QuantityUsed, CostPerUnit)
- User (Id, Username, PasswordHash, Role, IsActive)
- LocalLicense (Id, LicenseToken, DeviceFingerprint, ExpirationDate, Status, Type)

**Existing Services**:
- InventoryService - product CRUD, stock movements, FIFO batch tracking, financial overview
- UserService - authentication, user management
- LicenseService - hardware-bound license validation, tier-based feature gating
- AnalyticsService - reorder recommendations, dead stock, low margin alerts
- ReceiptService - PDF receipt generation via QuestPDF
- SettingsService - app settings (store name, currency, tax rate)
- CloudSyncService - STUB ONLY, does nothing real

**Existing ViewModels**: Dashboard, Inventory, POS, Reports, Analytics, Users, Settings, License, Login

---

## IMPLEMENTATION INSTRUCTIONS

For each feature below:
1. Add new Domain entities to Domain/Entities.cs
2. Register new tables in DatabaseService.InitializeAsync()
3. Create a new Service class in Services/
4. Create a ViewModel in UI/ViewModels/
5. Create a View (AXAML + code-behind) in UI/Views/
6. Wire navigation in MainViewModel
7. Gate features behind appropriate LicenseService tier checks

---

## PHASE 1: SUPPLIER & PROCUREMENT MANAGEMENT

### 1.1 New Entities to Add

```csharp
public class Supplier {
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Name { get; set; }
    public string ContactPerson { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Address { get; set; }
    public int DefaultLeadTimeDays { get; set; }  // How many days to deliver
    public string PaymentTerms { get; set; }       // e.g. "Net 30"
    public decimal Rating { get; set; }            // 1-5 performance score
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class PurchaseOrder {
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string PONumber { get; set; }           // e.g. "PO-2026-0001"
    public int SupplierId { get; set; }
    public string Status { get; set; }             // Draft, Pending, Approved, Shipped, Received, Cancelled
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }
    public string Notes { get; set; }
    public string CreatedByUsername { get; set; }
    public string ApprovedByUsername { get; set; }
    public decimal TotalAmount { get; set; }
}

public class PurchaseOrderItem {
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int ProductId { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }      // For partial deliveries
    public decimal UnitCost { get; set; }
    public decimal TotalCost => QuantityOrdered * UnitCost;
}
```

### 1.2 SupplierService to Create

Methods:
- `GetAllSuppliersAsync()` - list all active suppliers
- `AddSupplierAsync(Supplier supplier)` - create supplier
- `UpdateSupplierAsync(Supplier supplier)` - update supplier
- `DeleteSupplierAsync(int supplierId)` - soft delete (set IsActive = false)
- `GetSupplierPerformanceAsync(int supplierId)` - return on-time delivery %, avg lead time, total orders
- `GetTopSuppliersAsync(int limit = 5)` - ranked by rating and on-time delivery

### 1.3 PurchaseOrderService to Create

Methods:
- `CreatePurchaseOrderAsync(PurchaseOrder po, List<PurchaseOrderItem> items)` - create PO with auto-generated PONumber
- `ApprovePurchaseOrderAsync(int poId, string approverUsername)` - change status to Approved
- `MarkAsShippedAsync(int poId)` - change status to Shipped
- `ReceivePurchaseOrderAsync(int poId, List<(int itemId, int quantityReceived)> receivedItems)` - receive stock, auto-create StockMovements and PurchaseBatches for each received item, update PO status to Received
- `GetAllPurchaseOrdersAsync()` - list all POs with supplier name
- `GetPendingPurchaseOrdersAsync()` - only Draft/Pending/Approved/Shipped
- `CancelPurchaseOrderAsync(int poId)` - cancel if not yet received

### 1.4 Views to Create

- SuppliersView.axaml - DataGrid of suppliers, add/edit panel, performance stats per supplier
- PurchaseOrdersView.axaml - List of POs with status badges, create new PO flow (select supplier → add items → submit), receive PO flow with quantity input per line item

---

## PHASE 2: MULTI-LOCATION / WAREHOUSE MANAGEMENT

### 2.1 New Entities to Add

```csharp
public class Location {
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Name { get; set; }               // e.g. "Main Warehouse", "Retail Store", "Van 1"
    public string Type { get; set; }               // Warehouse, Store, Vehicle, External
    public string Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LocationStock {
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public int LocationId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int ReorderPoint { get; set; }          // Location-specific reorder threshold
}

public class StockTransfer {
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public int FromLocationId { get; set; }
    public int ToLocationId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; }             // Pending, InTransit, Completed, Cancelled
    public DateTime RequestedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string RequestedByUsername { get; set; }
    public string Notes { get; set; }
}
```

### 2.2 LocationService to Create

Methods:
- `GetAllLocationsAsync()` - list all active locations
- `AddLocationAsync(Location location)` - create location
- `GetStockByLocationAsync(int locationId)` - all products and quantities at a location
- `GetProductLocationsAsync(int productId)` - where is this product and how much at each location
- `TransferStockAsync(StockTransfer transfer)` - deduct from source, add to destination, create transfer record
- `GetTotalStockAcrossLocationsAsync(int productId)` - sum of all location quantities
- `GetLowStockByLocationAsync(int locationId)` - items below reorder point at that location

### 2.3 Views to Create

- LocationsView.axaml - manage locations, view stock per location
- StockTransferView.axaml - create transfer (from/to location, product, quantity), track transfer status

---

## PHASE 3: EXPIRY & BATCH QUALITY MANAGEMENT

### 3.1 Extend Existing Entities

Add these fields to PurchaseBatch:
```csharp
public DateTime? ExpiryDate { get; set; }
public string BatchNumber { get; set; }            // Supplier's batch/lot number
public string QualityStatus { get; set; } = "Good"; // Good, Quarantine, Rejected, Recalled
```

### 3.2 ExpiryService to Create

Methods:
- `GetExpiringProductsAsync(int daysAhead = 30)` - products expiring within N days
- `GetExpiredProductsAsync()` - already expired batches still in stock
- `RecallBatchAsync(string batchNumber, string reason)` - mark batch as Recalled, create OUT movements for all remaining stock, log reason
- `GetBatchTraceabilityAsync(string batchNumber)` - full history: received from which supplier, sold to which transactions
- `GetWasteReportAsync(DateTime from, DateTime to)` - expired/recalled stock value lost in period

### 3.3 Views to Create

- ExpiryDashboardView.axaml - traffic light system (Red = expired, Orange = expiring soon, Green = ok), batch recall button, waste report

---

## PHASE 4: INTELLIGENT REORDERING & DEMAND FORECASTING

### 4.1 New Entities to Add

```csharp
public class ReorderRule {
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public int ProductId { get; set; }
    public int PreferredSupplierId { get; set; }
    public int ReorderPoint { get; set; }          // Trigger reorder when stock hits this
    public int ReorderQuantity { get; set; }       // How much to order (EOQ-based)
    public int LeadTimeDays { get; set; }          // Expected delivery time
    public int SafetyStockDays { get; set; }       // Buffer days of stock to maintain
    public bool AutoCreatePO { get; set; } = false; // Auto-generate PO when triggered
}
```

### 4.2 ForecastingService to Create

Methods:
- `GetSalesVelocityAsync(int productId, int days = 30)` - average units sold per day over last N days
- `GetDaysUntilStockoutAsync(int productId)` - current stock / daily velocity = days remaining
- `GetReorderRecommendationsAsync()` - products where DaysUntilStockout <= LeadTimeDays + SafetyStockDays
- `CalculateEOQAsync(int productId)` - Economic Order Quantity: sqrt((2 * AnnualDemand * OrderCost) / HoldingCost)
- `GetSeasonalTrendAsync(int productId)` - compare same month last year vs this year, return trend multiplier
- `GetDemandForecastAsync(int productId, int futureDays)` - project future demand using linear regression on historical sales
- `GetABCClassificationAsync()` - classify all products as A (top 80% revenue), B (next 15%), C (bottom 5%)
- `GetXYZClassificationAsync()` - classify by demand variability: X (stable), Y (variable), Z (erratic)

### 4.3 Views to Create

- ForecastingView.axaml - show each product with: current stock, daily velocity, days until stockout (color coded), recommended order quantity, ABC/XYZ classification
- ReorderDashboardView.axaml - actionable list of items needing reorder, one-click "Create PO" button per item

---

## PHASE 5: RETURNS & REFUNDS MANAGEMENT

### 5.1 New Entities to Add

```csharp
public class CustomerReturn {
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string ReturnNumber { get; set; }       // e.g. "RET-2026-0001"
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; }             // Defective, Wrong Item, Changed Mind, Expired
    public string Condition { get; set; }          // Resaleable, Damaged, Destroyed
    public decimal RefundAmount { get; set; }
    public string ProcessedByUsername { get; set; }
    public DateTime ReturnDate { get; set; }
    public string OriginalReceiptId { get; set; }  // Link back to original sale
    public string Resolution { get; set; }         // Restocked, Returned to Supplier, Written Off
}

public class SupplierReturn {
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string ReturnNumber { get; set; }
    public int SupplierId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; }             // Defective, Wrong Item, Overstock
    public string Status { get; set; }             // Pending, Shipped, Credited
    public decimal CreditAmount { get; set; }
    public DateTime ReturnDate { get; set; }
}
```

### 5.2 ReturnsService to Create

Methods:
- `ProcessCustomerReturnAsync(CustomerReturn ret)` - if Condition == Resaleable, add stock back via StockMovement IN with reason "Customer Return", else log as waste
- `ProcessSupplierReturnAsync(SupplierReturn ret)` - deduct stock, update PurchaseBatch, track credit
- `GetReturnReasonsReportAsync(DateTime from, DateTime to)` - breakdown of return reasons and financial impact
- `GetReturnRateByProductAsync()` - which products have highest return rates (flag quality issues)

### 5.3 Views to Create

- ReturnsView.axaml - tabs for Customer Returns and Supplier Returns, return reason analytics chart

---

## PHASE 6: ADVANCED ANALYTICS & BUSINESS INTELLIGENCE

### 6.1 AdvancedAnalyticsService to Create

Methods:
- `GetStockTurnoverRatioAsync(int productId, DateTime from, DateTime to)` - COGS / Average Inventory Value
- `GetCarryingCostAnalysisAsync()` - estimate cost of holding inventory (typically 20-30% of inventory value per year)
- `GetProfitabilityRankingAsync()` - rank all products by total profit generated, margin %, and revenue contribution
- `GetSalesTrendAsync(int productId, int months = 12)` - month-by-month sales data for charting
- `GetParetoAnalysisAsync()` - which products make up 80% of revenue (Pareto/80-20 rule)
- `GetCohortAnalysisAsync()` - group sales by time period, show retention/repeat purchase patterns
- `GetPriceElasticityAsync(int productId)` - analyze how price changes correlated with demand changes
- `GetAnomalyDetectionAsync()` - flag unusual patterns: sudden sales drop, unexpected spike, unusual returns
- `GetInventoryHealthScoreAsync()` - composite score (0-100) based on turnover, dead stock %, stockout frequency, margin health

### 6.2 Dashboard Upgrade

Upgrade DashboardViewModel to show:
- Inventory Health Score (big number, color coded)
- Days Cash Tied Up in Inventory
- Top 5 Products by Profit (not just revenue)
- Stockout Risk Alerts (items running out in < 7 days)
- Expiry Alerts (items expiring in < 14 days)
- Pending POs count and value
- Return Rate this month vs last month
- ABC Classification summary (how many A, B, C items)

---

## PHASE 7: KITTING & PRODUCT BUNDLES

### 7.1 New Entities to Add

```csharp
public class ProductBundle {
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public int ParentProductId { get; set; }       // The bundle SKU
    public int ComponentProductId { get; set; }    // A component inside the bundle
    public int QuantityRequired { get; set; }      // How many of this component per bundle
}
```

### 7.2 BundleService to Create

Methods:
- `GetBundleComponentsAsync(int bundleProductId)` - list all components and quantities
- `GetAvailableBundleQuantityAsync(int bundleProductId)` - min(component stock / required qty) across all components
- `AssembleBundleAsync(int bundleProductId, int quantity, string username)` - deduct components, add to bundle product stock
- `DisassembleBundleAsync(int bundleProductId, int quantity, string username)` - reverse: deduct bundle, return components
- `CanFulfillBundleOrderAsync(int bundleProductId, int requestedQty)` - check if enough components exist

---

## PHASE 8: AUDIT TRAIL & COMPLIANCE

### 8.1 New Entity to Add

```csharp
public class AuditLog {
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string EntityType { get; set; }         // "Product", "User", "PurchaseOrder", etc.
    public int EntityId { get; set; }
    public string Action { get; set; }             // Created, Updated, Deleted, Approved, etc.
    public string ChangedByUsername { get; set; }
    public DateTime Timestamp { get; set; }
    public string OldValues { get; set; }          // JSON snapshot of before
    public string NewValues { get; set; }          // JSON snapshot of after
    public string IpAddress { get; set; }
}
```

### 8.2 AuditService to Create

Methods:
- `LogAsync(string entityType, int entityId, string action, string username, object oldValue, object newValue)` - serialize to JSON and insert
- `GetAuditTrailAsync(string entityType, int entityId)` - full history for one record
- `GetUserActivityAsync(string username, DateTime from, DateTime to)` - all actions by a user
- `GetAuditReportAsync(DateTime from, DateTime to)` - full audit log for compliance export

Integrate AuditService calls into: InventoryService (product changes), UserService (user changes), PurchaseOrderService (PO status changes), LicenseService (activation events).

---

## PHASE 9: REPORTING ENGINE UPGRADE

### 9.1 ReportingService to Create

Methods:
- `GeneratePurchaseOrderPdfAsync(int poId)` - professional PDF of PO to send to supplier
- `GenerateStockValuationReportAsync()` - full inventory value by location, category, supplier
- `GenerateProfitAndLossReportAsync(DateTime from, DateTime to)` - P&L with revenue, COGS, gross profit, by category
- `GenerateSupplierPerformanceReportAsync()` - on-time delivery, quality, pricing comparison
- `GenerateABCAnalysisReportAsync()` - visual ABC classification with action recommendations
- `GenerateExpiryReportAsync()` - all batches with expiry dates, days remaining, value at risk
- `ExportToExcelAsync(string reportType, DateTime from, DateTime to)` - export any report to Excel using ClosedXML or EPPlus

---

## PHASE 10: REAL CLOUD SYNC (Replace the Stub)

### 10.1 Replace CloudSyncService

Implement real cloud sync using a REST API backend or direct cloud storage:

Option A (Simple): Use Azure Blob Storage or AWS S3 to backup/restore the SQLite database file
Option B (Advanced): Build a sync protocol that sends delta changes (new/modified records since last sync)

Methods to implement for real:
- `BackupToCloudAsync(string userId, string authToken)` - compress and upload inventory.db to cloud storage
- `RestoreFromCloudAsync(string userId, string authToken)` - download and replace local database
- `SyncDeltaAsync()` - send only records modified since LastSyncDate, merge with server state
- `GetLastSyncStatusAsync()` - when was last successful sync, how many records synced

Add `LastModifiedAt` timestamp to all major entities to support delta sync.

---

## LICENSE TIER MAPPING

Map new features to license tiers in LicenseService:

```
Basic (50 products):
  - Single location only
  - No supplier management
  - No purchase orders
  - No forecasting
  - No expiry tracking

Medium (500 products):
  - Supplier management
  - Purchase orders (manual approval)
  - Basic expiry tracking
  - Multi-location (up to 3)
  - Returns management

Pro (unlimited products):
  - Full forecasting & demand prediction
  - Advanced analytics (ABC/XYZ, Pareto)
  - Multi-location (unlimited)
  - Kitting & bundles
  - Full reporting engine
  - Audit trail

Enterprise (unlimited + multi-user):
  - Everything in Pro
  - Real cloud sync
  - Auto-reorder workflows
  - API integrations
  - Compliance reports
  - Bulk import/export
```

Add these methods to LicenseService:
- `CanAccessSupplierManagement()` → Medium+
- `CanAccessPurchaseOrders()` → Medium+
- `CanAccessMultiLocation()` → Medium+
- `CanAccessExpiryTracking()` → Medium+
- `CanAccessForecasting()` → Pro+
- `CanAccessAdvancedAnalytics()` → Pro+
- `CanAccessKitting()` → Pro+
- `CanAccessAuditTrail()` → Pro+
- `CanAccessCloudSync()` → Enterprise
- `CanAccessAutoReorder()` → Enterprise
- `GetMaxLocationCount()` → Basic=1, Medium=3, Pro/Enterprise=unlimited

---

## UI/UX UPGRADE REQUIREMENTS

The app should NOT look like a generic inventory app. Apply these UI principles:

1. **Dashboard as Command Center**: Replace simple stats with an actionable intelligence panel. Show "3 items will stock out in 7 days" not just "Low Stock: 3". Every metric should have a drill-down action.

2. **Color-Coded Health System**: Use a consistent traffic light system across all views:
   - Red = Critical (stockout, expired, overdue PO)
   - Orange = Warning (expiring soon, low stock, delayed PO)
   - Green = Healthy
   - Blue = Informational

3. **Contextual Actions**: Every list item should have smart context actions. A product row should show: Edit, Stock Movement, View History, Create PO, View Analytics - not just Edit/Delete.

4. **Timeline Views**: Stock movements, PO history, and audit logs should show as visual timelines, not just flat tables.

5. **Smart Search**: Global search across products, suppliers, POs, and movements. Search by SKU, name, batch number, supplier name.

6. **Notification Center**: In-app notification panel showing: expiry alerts, stockout warnings, PO approvals needed, sync status.

7. **Keyboard Shortcuts**: POS should support barcode scanner input (scan = add to cart). Inventory should support quick-add via keyboard.

---

## IMPLEMENTATION ORDER

Implement in this exact sequence to maximize business value at each step:

1. Phase 1 (Supplier + PO) - Solves procurement blindspot
2. Phase 4 (Forecasting) - Immediately useful with existing sales data
3. Phase 3 (Expiry) - Critical for food/pharma businesses
4. Phase 5 (Returns) - Completes the sales cycle
5. Phase 2 (Multi-Location) - Enables business growth
6. Phase 6 (Advanced Analytics) - Turns data into decisions
7. Phase 8 (Audit Trail) - Compliance and trust
8. Phase 7 (Kitting) - Manufacturing/assembly use cases
9. Phase 9 (Reporting Engine) - Professional output
10. Phase 10 (Cloud Sync) - Multi-device and backup

---

## PHASE 11: PREMIUM PRODUCT EXPERIENCE (UI Philosophy Upgrade)

This phase transforms the app from a functional tool into a premium product that feels expensive.
Every item here is about changing the user's emotional experience, not just adding features.

---

### 11.1 PROACTIVE "MORNING BRIEFING" DASHBOARD

**Philosophy**: Simple apps wait for the user to ask. Premium apps tell the user what to do.

Create a `DailyBriefingService` that runs on app startup and produces a prioritized list of action items.

```csharp
public class BriefingItem {
    public string Priority { get; set; }   // Critical, Warning, Info, Positive
    public string Icon { get; set; }       // emoji or icon key
    public string Message { get; set; }   // "3 items expire in 4 days"
    public string ActionLabel { get; set; } // "Review Now"
    public string NavigateTo { get; set; } // which view to open on click
}
```

Methods:
- `GetDailyBriefingAsync()` - returns ordered list of BriefingItems covering:
  - Expiring stock (Critical if < 3 days, Warning if < 7 days)
  - Stockout risk (Critical if < 2 days remaining, Warning if < 7 days)
  - Sales vs last week comparison ("Sales up 12% vs last Monday" or "Sales down 8%")
  - Pending PO approvals ("5 purchase orders awaiting your approval")
  - Overdue deliveries ("PO #0023 is 3 days overdue")
  - Dead stock alert ("$450 worth of stock hasn't moved in 90 days")
  - Top performer of the day ("Coca-Cola is today's best seller so far")

Replace the current dashboard stats grid with a "Morning Briefing" card at the top that shows
these items as an actionable feed. Each item is clickable and navigates to the relevant view.
Below the briefing, keep the KPI cards but make them interactive (click to drill down).

---

### 11.2 SPARKLINES & VISUAL PRODUCT LIST

**Philosophy**: A manager should understand product health at a glance without clicking anything.

Create a `SparklineControl` custom Avalonia control that:
- Accepts a `List<decimal>` of values (last 7 days of sales)
- Renders a tiny SVG-style line chart (40px wide, 20px tall) inline in the product row
- Colors the line green if trending up, red if trending down, grey if flat

In InventoryViewModel, for each product load the last 7 days of daily sales quantities from
StockMovement table and attach as a `SalesTrend` property.

In InventoryView.axaml, add the SparklineControl as a column in the product DataGrid next to
the stock quantity. Also add a colored stock health indicator dot:
- Red dot = stock below reorder point
- Orange dot = stock below 2x reorder point
- Green dot = healthy

Add a "Low Stock Gauge" widget to the dashboard: a circular progress ring showing
"X of Y products are healthy" as a percentage with color coding.

---

### 11.3 ESC/POS THERMAL PRINTER SUPPORT

**Philosophy**: Simple apps print PDFs. Premium apps talk to hardware directly.

Create an `EscPosPrintService` that sends raw ESC/POS commands to a thermal printer.

Use the NuGet package `ESCPOS-ThermalPrinter` or implement raw ESC/POS byte commands directly.

Methods:
- `GetAvailablePrintersAsync()` - list all connected printers (USB and network)
- `PrintReceiptAsync(ReceiptData data)` - format and send receipt to thermal printer
- `PrintTestPageAsync()` - print a test page for printer setup
- `IsConnectedAsync()` - check if configured printer is reachable

ReceiptData should include: store name, cashier, items (name, qty, price, subtotal), total, cash paid, change, receipt ID, date.

In SettingsView, add a "Printer Setup" section where the user can:
- Select their thermal printer from a dropdown
- Choose between PDF receipt (existing) and ESC/POS direct print
- Print a test page

In POSViewModel, after checkout:
- If ESC/POS printer is configured and connected → print directly, no PDF dialog
- If not configured → fall back to existing PDF generation

The receipt format should be clean 80mm thermal layout:
store name centered, dashed separator lines, items left-aligned with price right-aligned,
bold TOTAL line, thank you message at bottom.

---

### 11.4 BARCODE SCANNER SUPPORT

**Philosophy**: Staff should never touch the keyboard during a sale or stock receive.

Barcode scanners in HID mode send keystrokes ending with Enter. Implement scanner detection by:

In POSViewModel:
- Add a `BarcodeBuffer` string property
- In the View's KeyDown handler, if input arrives faster than 50ms between characters,
  treat it as scanner input (not manual typing)
- When Enter is received after scanner input, call `AddToCartByBarcodeAsync(string barcode)`
- `AddToCartByBarcodeAsync` looks up product by SKU field, adds to cart, clears buffer, plays a beep sound

In InventoryViewModel (for receiving stock):
- Same scanner detection logic
- When barcode scanned in stock receive mode, auto-populate the product field in the stock movement form

In SettingsView, add a "Barcode Scanner" toggle so the user can enable/disable scanner mode.

Add a `BarcodeGeneratorService`:
- `GenerateBarcodeImageAsync(string sku)` - generate Code128 barcode as PNG using ZXing.Net
- In product detail view, show the product's barcode image with a "Print Label" button
- `PrintProductLabelAsync(Product product)` - print a small label with product name, price, and barcode via ESC/POS

---

### 11.5 PROFIT SIMULATOR (WHAT-IF ANALYSIS)

**Philosophy**: Give business owners the power to make pricing decisions with confidence.

Create a `ProfitSimulatorViewModel` and `ProfitSimulatorView`.

The simulator works as follows:
- User selects a product
- System loads: current price, current cost, units sold last 30 days, current margin %
- User sees a price slider (range: Cost price to 3x current price)
- As user drags the slider, in real-time update:
  - New margin % 
  - Projected monthly profit (new margin × last month units)
  - Profit delta vs current ("+ 12,500 RWF / month")
  - Break-even units (how many units needed to cover COGS at new price)
- Add a "Volume Impact" toggle: user can also adjust expected volume change
  (e.g. "if I raise price 10%, I expect 5% fewer sales") and see net effect
- Show a comparison chart: Current vs Projected profit as two bars side by side

Also add a "Bulk Price Optimizer":
- Show all products with margin < 20%
- Suggest minimum price increase to reach 25% margin
- "Apply All Suggestions" button that updates prices in bulk with confirmation

Gate this feature behind Pro+ license tier.

---

### 11.6 GRANULAR ROLE-BASED PERMISSIONS

**Philosophy**: Business owners pay for control over what their staff can and cannot do.

Replace the simple `Role` string on User with a full permissions system.

New entity:
```csharp
public class UserPermissions {
    [PrimaryKey] public int UserId { get; set; }

    // POS Permissions
    public bool CanProcessSales { get; set; } = true;
    public bool CanApplyDiscount { get; set; } = false;
    public decimal MaxDiscountPercent { get; set; } = 0;
    public bool CanViewCostPrice { get; set; } = false;
    public bool CanDeleteTransactions { get; set; } = false;
    public bool CanIssueRefunds { get; set; } = false;

    // Inventory Permissions
    public bool CanAddProducts { get; set; } = false;
    public bool CanEditProducts { get; set; } = false;
    public bool CanDeleteProducts { get; set; } = false;
    public bool CanAdjustStock { get; set; } = false;
    public bool CanReceiveStock { get; set; } = false;
    public bool CanViewInventoryValue { get; set; } = false;

    // Reports & Analytics
    public bool CanViewReports { get; set; } = false;
    public bool CanViewProfitMargins { get; set; } = false;
    public bool CanExportData { get; set; } = false;

    // Management
    public bool CanManageUsers { get; set; } = false;
    public bool CanManageSuppliers { get; set; } = false;
    public bool CanApprovePurchaseOrders { get; set; } = false;
    public bool CanManageSettings { get; set; } = false;
}
```

Create a `PermissionService`:
- `GetPermissionsAsync(int userId)` - load user's permissions
- `UpdatePermissionsAsync(UserPermissions permissions)` - save changes
- `GetDefaultPermissionsForRole(string role)` - return sensible defaults for Admin/Manager/Cashier/Staff/Guest
- `HasPermission(string permissionKey)` - check current user's permission by key name

Update `UserSession` to hold the current user's `UserPermissions` object after login.

In UsersView, when editing a user, show a permissions panel with toggle switches grouped by category
(POS, Inventory, Reports, Management). Show a role preset dropdown at the top that auto-fills
sensible defaults when selected, but allows individual overrides.

Enforce permissions throughout the app:
- In POSViewModel: hide discount field if `!CanApplyDiscount`, hide cost column if `!CanViewCostPrice`
- In InventoryViewModel: disable add/edit/delete buttons based on permissions
- In MainViewModel: hide navigation items the user has no access to
- In ReportsViewModel: check `CanViewReports` and `CanViewProfitMargins` before loading data

---

## CONSTRAINTS & RULES

- Do NOT break existing functionality. All current features must continue working.
- Keep SQLite as the database. Do not migrate to a different DB engine.
- Follow existing MVVM patterns. New ViewModels extend ViewModelBase.
- New Views must follow existing AXAML styling conventions.
- All new service methods must be async.
- All database operations must use transactions where multiple tables are affected.
- All new features must be gated behind LicenseService tier checks.
- Do not add NuGet packages without justification. Prefer packages already in use.
- Maintain the existing project structure: Domain → Infrastructure → Services → UI/ViewModels → UI/Views.
