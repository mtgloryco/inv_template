# Glory Desk

**Stock, sales & accounts in one place.**

Glory Desk is a desktop business management app by **MT GLORY CO**. It helps shops and growing companies run inventory, point of sale, purchases, sales, accounting, and reporting from a single workspace — without the complexity of traditional ERP jargon.

- **Product site:** [glorydesk.mtglory.com](https://glorydesk.mtglory.com) — **private repo** [`mtgloryco/glorydesk-web`](https://github.com/mtgloryco/glorydesk-web) (Next.js on Vercel)
- **License & docs:** [glorydesk.mtglory.com/activate](https://glorydesk.mtglory.com/activate) · [glorydesk.mtglory.com/docs](https://glorydesk.mtglory.com/docs)
- **Support:** support@mtglory.com

The license portal lives in a **separate private repository** (not in this repo). See **[docs/WEB_PORTAL.md](docs/WEB_PORTAL.md)**.

---

## Screenshots

Upload your captures to [`docs/screenshots/`](docs/screenshots/) using the filenames below. Once committed to GitHub, they will display here automatically.

### Dashboard

![Glory Desk dashboard](docs/screenshots/dashboard.png)

### Point of Sale

![Glory Desk POS](docs/screenshots/pos.png)

### Inventory

![Glory Desk inventory](docs/screenshots/inventory.png)

### Reports

![Glory Desk reports](docs/screenshots/reports.png)

### Enterprise (Pro / Enterprise license)

![Glory Desk enterprise hub](docs/screenshots/enterprise.png)

### Login (optional)

![Glory Desk login](docs/screenshots/login.png)

> **Adding screenshots:** See [docs/screenshots/README.md](docs/screenshots/README.md) for the exact filenames to use.

---

## What Glory Desk does

Glory Desk is built for day-to-day business operations:

| Area | Highlights |
|------|------------|
| **Inventory** | Products, categories, stock movements, batch/expiry tracking, cycle counts, reorder rules |
| **POS** | Fast checkout, barcode scan, multi-currency, receipts & invoices |
| **Sales & purchases** | Sales orders, purchase orders, RFQs, receiving, landed costs, partial payments |
| **Finance** | General ledger, AR/AP aging, bank reconciliation, VAT exports, budget vs actual |
| **Manufacturing** | Bills of material, production orders, MRP planning |
| **Reports** | P&amp;L, balance sheet, ABC analysis, dead stock, margin by category, month close |
| **Enterprise** | Multi-branch, approvals, CRM pipeline, mobile sync queue, security policies |
| **Governance** | Role-based access, full audit trail, cloud backup & sync |

Works **offline-first** with optional **cloud sync** for multi-device and backup scenarios.

---

## Who it’s for

- Retail shops and wholesalers  
- Growing SMEs that outgrow spreadsheets  
- Multi-location businesses (Enterprise tier)  
- Teams that need POS + inventory + basic accounting in one tool  

Available in **English**, **French**, and **Kinyarwanda**.

---

## Quick start

### Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Windows, Linux, or macOS (desktop target)

### Run from source

```bash
git clone <your-repo-url>
cd InventoryManagementSystem
dotnet run --project InventoryManagementSystem.Desktop
```

**Default login:** `admin` / `admin123`

On first launch you may see the setup wizard and license screen. Activate a **Pro** or **Enterprise** license to unlock advanced reports and cloud features.

### Run tests

```bash
dotnet test
```

### Windows installer (share with customers)

See **[docs/WINDOWS_INSTALL.md](docs/WINDOWS_INSTALL.md)** for building `GloryDesk_Setup_v1.0.0_Windows.exe` and fixing the VC++ side-by-side error.

Quick build on Windows:

```powershell
.\scripts\publish_win.ps1
```

Share **`Releases\GloryDesk_Setup_v1.0.0_Windows.exe`**.

---

## Project structure

| Project | Purpose |
|---------|---------|
| `InventoryManagementSystem.Desktop` | Avalonia desktop app (Glory Desk UI) |
| `InventoryManagementSystem.Shared` | Domain, services, views, view models |
| `InventoryManagementSystem.Cloud` | Cloud API for sync, auth, and backup |
| `web` | **License portal** (Next.js) — deploy to **Vercel** |
| `InventoryManagementSystem.Tests` | Integration and phase tests |

User data is stored locally:

- **Database:** `~/.local/share/GloryDesk/inventory.db` (legacy: `InventoryManagementSystem`)
- **Exports:** `~/Documents/GloryDesk/` (receipts, reports, PDFs)

---

## License tiers

| Tier | Typical use |
|------|-------------|
| **Free / Starter** | Core inventory & POS |
| **Pro** | Advanced reports, analytics, integrations |
| **Enterprise** | Cloud sync, multi-branch, Enterprise hub, MRP, CRM |

Activate at [glorydesk.mtglory.com/activate](https://glorydesk.mtglory.com/activate).

---

## Tech stack

- **UI:** Avalonia UI (.NET 8)  
- **Database:** SQLite (offline-first)  
- **PDF:** QuestPDF  
- **Updates:** Velopack  
- **Cloud:** ASP.NET Core API  

---

## Roadmap status

Phases 1–5 are implemented (core ops → financial maturity → inventory depth → integrations → enterprise). See [`industry-roadmap.txt`](industry-roadmap.txt) for the full checklist.

---

## Contributing & support

This repository is maintained by **MT GLORY CO**.

- Issues and feature requests: use GitHub Issues  
- Email: support@mtglory.com  
- Documentation: [glorydesk.mtglory.com/docs](https://glorydesk.mtglory.com/docs)

---

## License

Proprietary — © MT GLORY CO. Contact MT GLORY CO for licensing and distribution terms.
