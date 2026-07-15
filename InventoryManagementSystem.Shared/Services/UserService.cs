using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class UserService
    {
        private readonly DatabaseService _databaseService;
        private readonly AuditService? _auditService;

        public UserService(DatabaseService databaseService, AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _auditService = auditService;
        }

        public async Task InitializeAsync()
        {
            await RepairLegacyAdminPasswordAsync();

            // Create default admin if no users exist
            var count = await _databaseService.Connection.Table<User>().CountAsync();
            if (count == 0)
            {
                var admin = new User
                {
                    Username = "admin",
                    PasswordHash = HashPassword("admin123"),
                    Role = "Admin"
                };
                await _databaseService.Connection.InsertAsync(admin);
            }

            // Create Guest user if not exists
            var guest = await _databaseService.Connection.Table<User>().Where(u => u.Username == "guest").FirstOrDefaultAsync();
            if (guest == null)
            {
                guest = new User
                {
                    Username = "guest",
                    PasswordHash = HashPassword(""),
                    Role = "Guest"
                };
                await _databaseService.Connection.InsertAsync(guest);
            }
        }

        /// <summary>
        /// Older DB versions stored admin password as plain text. Re-hash so login works.
        /// </summary>
        private async Task RepairLegacyAdminPasswordAsync()
        {
            var admin = await _databaseService.Connection.Table<User>()
                .Where(u => u.Username == "admin")
                .FirstOrDefaultAsync();
            if (admin == null)
            {
                return;
            }

            if (admin.PasswordHash == "admin123" || admin.PasswordHash.Length != 64)
            {
                admin.PasswordHash = HashPassword("admin123");
                await _databaseService.Connection.UpdateAsync(admin);
            }
        }

        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            var hash = HashPassword(password);
            var normalized = username.Trim();
            return await _databaseService.Connection.Table<User>()
                .Where(u => u.Username == normalized && u.PasswordHash == hash)
                .FirstOrDefaultAsync();
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _databaseService.Connection.Table<User>().ToListAsync();
        }

        public async Task AddUserAsync(User user, string plainPassword)
        {
            user.PasswordHash = HashPassword(plainPassword);
            await _databaseService.Connection.InsertAsync(user);
            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    UserSession.CurrentUser?.Username ?? "System",
                    "Create", "User", user.Id, ToAuditSnapshot(user));
            }
        }

        public async Task UpdateUserAsync(User user)
        {
            var old = await GetUserByIdAsync(user.Id);
            await _databaseService.Connection.UpdateAsync(user);
            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    UserSession.CurrentUser?.Username ?? "System",
                    "Update", "User", user.Id,
                    ToAuditSnapshot(user),
                    old != null ? ToAuditSnapshot(old) : null);
            }
        }

        public async Task UpdateUserAccessAsync(User user, IEnumerable<string> permissions)
        {
            var old = await GetUserByIdAsync(user.Id);
            user.PermissionsJson = UserAccessService.SerializePermissions(permissions);
            await _databaseService.Connection.UpdateAsync(user);
            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    UserSession.CurrentUser?.Username ?? "System",
                    "Update", "User", user.Id,
                    new { user.Username, user.Role, Permissions = permissions },
                    old != null ? new { old.Username, old.Role, old.PermissionsJson } : null);
            }
        }

        public async Task RecordLoginAsync(User user)
        {
            user.LastLoginAt = DateTime.Now;
            await _databaseService.Connection.UpdateAsync(user);
        }

        public async Task<User?> GetUserByIdAsync(int id) =>
            await _databaseService.Connection.FindAsync<User>(id);

        public async Task UpdatePasswordAsync(User user, string newPassword)
        {
            user.PasswordHash = HashPassword(newPassword);
            await _databaseService.Connection.UpdateAsync(user);
            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    UserSession.CurrentUser?.Username ?? "System",
                    "Update", "User", user.Id,
                    new { user.Username, PasswordChanged = true });
            }
        }

        public async Task DeleteUserAsync(User user)
        {
            await _databaseService.Connection.DeleteAsync(user);
            if (_auditService != null)
            {
                await _auditService.LogActionAsync(
                    UserSession.CurrentUser?.Username ?? "System",
                    "Delete", "User", user.Id, null, ToAuditSnapshot(user));
            }
        }

        private static object ToAuditSnapshot(User user) =>
            new { user.Username, user.Role, user.PermissionsJson, user.LastLoginAt };

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
