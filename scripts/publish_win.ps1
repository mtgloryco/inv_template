# Builds a shareable Glory Desk Windows installer (Inno Setup + VC++ runtime).
# Run on Windows with .NET 8 SDK and Inno Setup 6 installed.

$ErrorActionPreference = "Stop"

$OutputDir = "Releases\Windows"
$ArchiveName = "GloryDesk_Windows.zip"
$RedistDir = "InventoryManagementSystem.Shared\redist"
$VcRedistUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
$IssScript = "InventoryManagementSystem.Shared\GloryDesk_Setup_Script.iss"
$Version = "1.0.1"

Write-Host "Building Glory Desk for Windows x64..." -ForegroundColor Cyan

if (!(Test-Path -Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Folder publish (not single-file) avoids side-by-side manifest issues on clean PCs.
dotnet publish "InventoryManagementSystem.Desktop\InventoryManagementSystem.Desktop.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

if (!(Test-Path -Path "$OutputDir\GloryDesk.exe")) {
    Write-Host "Expected $OutputDir\GloryDesk.exe was not produced." -ForegroundColor Red
    exit 1
}

if (!(Test-Path -Path $RedistDir)) {
    New-Item -ItemType Directory -Path $RedistDir | Out-Null
}

$RedistPath = Join-Path $RedistDir "vc_redist.x64.exe"
if (!(Test-Path -Path $RedistPath)) {
    Write-Host "Downloading Visual C++ Redistributable (x64)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $VcRedistUrl -OutFile $RedistPath
}

Write-Host "Build successful." -ForegroundColor Green

if (Test-Path -Path "Releases\$ArchiveName") {
    Remove-Item -Path "Releases\$ArchiveName" -Force
}
Compress-Archive -Path $OutputDir -DestinationPath "Releases\$ArchiveName"
Write-Host "Portable zip: Releases\$ArchiveName" -ForegroundColor Green

$IsccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if ((Test-Path -Path $IsccPath) -and (Test-Path -Path $IssScript)) {
    Write-Host "Compiling installer..." -ForegroundColor Cyan
    & $IsccPath $IssScript
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Installer ready: Releases\GloryDesk_Setup_v$Version`_Windows.exe" -ForegroundColor Green
    } else {
        Write-Host "Inno Setup failed." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Inno Setup 6 not found. Install from https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
    Write-Host "Then re-run this script to produce GloryDesk_Setup_v$Version`_Windows.exe" -ForegroundColor Yellow
}
