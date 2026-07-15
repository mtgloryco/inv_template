using System;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class SecurityComplianceService
    {
        private readonly DatabaseService _databaseService;
        private readonly AuditService? _auditService;

        public SecurityComplianceService(DatabaseService databaseService, AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _auditService = auditService;
        }

        public async Task<SecurityPolicy> GetPolicyAsync()
        {
            var policy = await _databaseService.Connection.Table<SecurityPolicy>().FirstOrDefaultAsync();
            if (policy != null)
            {
                return policy;
            }

            policy = new SecurityPolicy
            {
                EnableEncryptionAtRest = false,
                MinPasswordLength = 8,
                RequireMfa = false,
                BackupRetentionDays = 30,
                BackupSlaHours = 24,
                LastUpdatedAt = DateTime.UtcNow,
                UpdatedByUsername = "System"
            };
            await _databaseService.Connection.InsertAsync(policy);
            return policy;
        }

        public async Task<SecurityPolicy> UpdatePolicyAsync(SecurityPolicy policy, string username)
        {
            policy.LastUpdatedAt = DateTime.UtcNow;
            policy.UpdatedByUsername = username;

            if (policy.Id == 0)
            {
                await _databaseService.Connection.InsertAsync(policy);
            }
            else
            {
                await _databaseService.Connection.UpdateAsync(policy);
            }

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "Update", "SecurityPolicy", policy.Id, policy);
            }

            return policy;
        }

        public async Task ConfigureSsoAsync(string provider, string clientId, string username)
        {
            var policy = await GetPolicyAsync();
            policy.SsoProvider = provider;
            policy.SsoClientId = clientId;
            await UpdatePolicyAsync(policy, username);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(username, "ConfigureSSO", "SecurityPolicy", policy.Id,
                    new { Provider = provider, ClientId = clientId });
            }
        }

        public async Task<BackupSlaLog> RecordBackupAsync(
            string backupType, DateTime startedAt, DateTime completedAt, bool success, long sizeBytes)
        {
            var policy = await GetPolicyAsync();
            var durationHours = (completedAt - startedAt).TotalHours;
            var withinSla = durationHours <= policy.BackupSlaHours;

            var log = new BackupSlaLog
            {
                BackupType = backupType,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Success = success,
                SizeBytes = sizeBytes,
                WithinSla = withinSla && success
            };

            await _databaseService.Connection.InsertAsync(log);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    UserSession.CurrentUser?.Username ?? "System",
                    "RecordBackup",
                    "BackupSlaLog",
                    log.Id,
                    log);
            }

            return log;
        }

        public async Task<bool> ValidateLatestBackupWithinSlaAsync()
        {
            var policy = await GetPolicyAsync();
            var latest = await _databaseService.Connection.Table<BackupSlaLog>()
                .OrderByDescending(l => l.CompletedAt)
                .FirstOrDefaultAsync();

            if (latest == null || !latest.Success || !latest.CompletedAt.HasValue)
            {
                return false;
            }

            var ageHours = (DateTime.UtcNow - latest.CompletedAt.Value).TotalHours;
            return latest.WithinSla && ageHours <= policy.BackupSlaHours;
        }

        public bool ValidatePasswordAgainstPolicy(string password, SecurityPolicy policy)
        {
            return !string.IsNullOrWhiteSpace(password) && password.Length >= policy.MinPasswordLength;
        }
    }
}
