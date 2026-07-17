# Windows install & troubleshooting

## Error: "side-by-side configuration is incorrect" (code 14001)

This usually means **Microsoft Visual C++ 2015–2022 (x64)** is missing — **not** a missing .NET install (Glory Desk is self-contained and bundles .NET 10).

### Quick fix for users

1. Uninstall any old **Inventory Management System** app (Settings → Apps)
2. Install **`GloryDesk_Setup_v1.0.1_Windows.exe`** (run as administrator)
3. If it still fails, open `C:\Program Files\GloryDesk\` and double-click **`vc_redist.x64.exe`**
4. Or download: [vc_redist.x64.exe](https://aka.ms/vs/17/release/vc_redist.x64.exe)
5. Restart the PC and launch **Glory Desk**

---

## Important: build on Windows for reliable installs

Installers built by **cross-compiling from Linux** (Wine + Inno Setup) can still fail on some clean Windows PCs because of how the Windows executable manifest is embedded.

**Most reliable:** build on a real Windows machine or GitHub Actions (`Build Windows Installer` workflow).

---

## Build the installer (developers)

### On Windows (recommended)

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download), [Inno Setup 6](https://jrsoftware.org/isinfo.php)

```powershell
cd InventoryManagementSystem
.\scripts\publish_win.ps1
```

Output:

| File | Purpose |
|------|---------|
| `Releases\GloryDesk_Setup_v1.0.1_Windows.exe` | **Share this** with customers |
| `Releases\GloryDesk_Windows.zip` | Portable folder (no installer) |

The installer includes **`vc_redist.x64.exe`** in the app folder so users can reinstall it manually if needed.

### Via GitHub Actions (no Windows PC needed)

1. Push this repo to GitHub
2. **Actions** → **Build Windows Installer** → **Run workflow**
3. Download **GloryDesk-Windows-Setup** artifact

---

## Default login

- Username: `admin`
- Password: `admin123`

Change the password after first login (Users screen).
