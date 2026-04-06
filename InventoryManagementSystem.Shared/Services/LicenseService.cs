using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class LicenseService
    {
        private readonly DatabaseService _databaseService;
        private readonly HardwareIdService _hardwareIdService;
        private readonly LicenseCryptoService _cryptoService;

        public LocalLicense CurrentLicense { get; private set; } = new LocalLicense();

        public LicenseService(
            DatabaseService databaseService,
            HardwareIdService hardwareIdService,
            LicenseCryptoService cryptoService)
        {
            _databaseService = databaseService;
            _hardwareIdService = hardwareIdService;
            _cryptoService = cryptoService;
        }

        public async Task InitializeAsync()
        {
            var hid = _hardwareIdService.GetCompositeHardwareId();

            // Fetch existing license
            var license = await _databaseService.Connection.Table<LocalLicense>()
                .OrderByDescending(l => l.Id)
                .FirstOrDefaultAsync();

            if (license == null)
            {
                // New installation: Start as Locked
                license = new LocalLicense
                {
                    DeviceFingerprint = hid,
                    Status = "Locked",
                    Type = "None",
                    ExpirationDate = DateTime.MinValue,
                    LastKnownValidDate = DateTime.UtcNow
                };
                await _databaseService.Connection.InsertAsync(license);
            }
            else
            {
                // 1. Force lock if legacy Free tier is detected
                if (license.Type == "Free")
                {
                    license.Status = "Locked";
                    license.Type = "None";
                    license.LicenseToken = string.Empty;
                }

                // 2. Validate existing license key if present
                if (!string.IsNullOrEmpty(license.LicenseToken))
                {
                    var result = ValidateLicenseKey(license.LicenseToken);
                    license.Status = result.ToString();

                    // Clock manipulation check
                    if (DateTime.UtcNow < license.LastKnownValidDate)
                    {
                        license.Status = "ClockSkewed";
                    }
                    else if (result == LicenseValidationResult.Valid)
                    {
                        license.LastKnownValidDate = DateTime.UtcNow;
                    }
                }
                else
                {
                    license.Status = "Locked";
                    license.Type = "None";
                }

                await _databaseService.Connection.UpdateAsync(license);
            }

            CurrentLicense = license;
        }

        public async Task<LicenseValidationResult> ActivateLicenseAsync(string licenseKey)
        {
            var result = ValidateLicenseKey(licenseKey);

            if (result == LicenseValidationResult.Valid)
            {
                var payload = ExtractPayload(licenseKey);
                if (payload != null)
                {
                    CurrentLicense.LicenseToken = licenseKey;
                    CurrentLicense.Type = payload.Tier;
                    CurrentLicense.Status = "Active";
                    CurrentLicense.ExpirationDate = payload.Expiry;
                    CurrentLicense.LastKnownValidDate = DateTime.UtcNow;
                    CurrentLicense.DeviceFingerprint = payload.HardwareId;

                    await _databaseService.Connection.UpdateAsync(CurrentLicense);
                }
            }

            return result;
        }

        public LicenseValidationResult ValidateLicenseKey(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey)) return LicenseValidationResult.InvalidFormat;

            var parts = licenseKey.Split('.');
            if (parts.Length != 2) return LicenseValidationResult.InvalidFormat;

            try
            {
                string payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                string signature = parts[1]; // signature is already base64 in the split

                // 1. Verify RSA Signature
                if (!_cryptoService.VerifySignature(payloadJson, signature))
                {
                    return LicenseValidationResult.InvalidSignature;
                }

                // 2. Deserialize payload
                var payload = LicensePayload.FromJson(payloadJson);
                if (payload == null) return LicenseValidationResult.InvalidFormat;

                // 3. Hardware binding check
                var currentHid = _hardwareIdService.GetCompositeHardwareId();
                if (payload.HardwareId != currentHid)
                {
                    return LicenseValidationResult.HardwareMismatch;
                }

                // 4. Expiry check
                if (DateTime.UtcNow > payload.Expiry)
                {
                    return LicenseValidationResult.Expired;
                }

                return LicenseValidationResult.Valid;
            }
            catch
            {
                return LicenseValidationResult.InvalidFormat;
            }
        }

        private LicensePayload? ExtractPayload(string licenseKey)
        {
            try
            {
                var parts = licenseKey.Split('.');
                string payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                return LicensePayload.FromJson(payloadJson);
            }
            catch { return null; }
        }

        public bool IsBasic => CheckTier("Basic");
        public bool IsMedium => CheckTier("Medium");
        public bool IsPro => CheckTier("Pro");
        public bool IsEnterprise => CheckTier("Enterprise");

        private bool CheckTier(string tier)
        {
            if (CurrentLicense.Status != "Valid" && CurrentLicense.Status != "Active") return false;
            // Allow string contains for flexibility (e.g. "Basic Starter" contains "Basic")
            return (CurrentLicense.Type?.Contains(tier, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        // --- Permission Helpers ---

        public int GetMaxProductCount()
        {
            if (IsEnterprise || IsPro) return int.MaxValue;
            if (IsMedium) return 500;
            if (IsBasic) return 50;
            
            // Default/Locked/Free
            return 0; // Or minimal if we want to allow something for free
        }

        // --- Permission Helpers ---

        public bool CanAccessPOS()
        {
            // Medium, Pro, Enterprise
            return IsMedium || IsPro || IsEnterprise;
        }

        public bool CanAccessReceipts()
        {
            // Medium, Pro, Enterprise
            return IsMedium || IsPro || IsEnterprise;
        }

        public bool CanAccessAnalytics()
        {
            // Pro, Enterprise (Business Analytics)
            return IsPro || IsEnterprise;
        }
        
        public bool CanAccessAdvancedReports()
        {
             // Pro, Enterprise
            return IsPro || IsEnterprise;
        }

        public bool CanAccessStockForecasting()
        {
            // Pro, Enterprise (Stock Forecasting)
            return IsPro || IsEnterprise;
        }

        public bool CanAccessProfitAndLoss()
        {
             // Pro, Enterprise (Profit & Loss Reports)
            return IsPro || IsEnterprise;
        }

        public bool CanAccessExport()
        {
             // Pro, Enterprise
            return IsPro || IsEnterprise;
        }

        public bool CanAccessBulkImport()
        {
            // Enterprise only (or Pro if generous) - limiting to Enterprise for now based on request
            return IsEnterprise;
        }

        public bool CanAccessSupplierManagement() => IsMedium || IsPro || IsEnterprise;
        public bool CanAccessPurchaseOrders() => IsMedium || IsPro || IsEnterprise;
        public bool CanAccessMultiLocation() => IsMedium || IsPro || IsEnterprise;
        public bool CanAccessExpiryTracking() => IsMedium || IsPro || IsEnterprise;
        public bool CanAccessForecasting() => IsPro || IsEnterprise;
        public bool CanAccessAdvancedAnalytics() => IsPro || IsEnterprise;
        public bool CanAccessKitting() => IsPro || IsEnterprise;
        public bool CanAccessAuditTrail() => IsPro || IsEnterprise;
        public bool CanAccessCloudSync() => IsEnterprise;
        public bool CanAccessAutoReorder() => IsEnterprise;

        public int GetMaxLocationCount()
        {
            if (IsBasic) return 1;
            if (IsMedium) return 3;
            if (IsPro || IsEnterprise) return int.MaxValue;
            return 0;
        }

        [Obsolete("Use specific permission methods instead.")]
        public bool IsFeatureAllowed(string featureName)
        {
             if (featureName == "POS") return CanAccessPOS();
             if (featureName == "Reports") return CanAccessAdvancedReports();
             if (featureName == "ExportReports") return CanAccessExport();
             return false;
        }
    }
}
