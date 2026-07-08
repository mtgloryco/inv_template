using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using InventoryManagementSystem.Cloud.Data;
using InventoryManagementSystem.Cloud.Models;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace InventoryManagementSystem.Cloud.Services;

public class AuthService
{
    private readonly CloudDatabase _db;
    private readonly IConfiguration _configuration;

    public AuthService(CloudDatabase db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orgName = string.IsNullOrWhiteSpace(request.OrganizationName)
            ? $"{request.Email.Split('@')[0]} Workspace"
            : request.OrganizationName.Trim();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var now = DateTime.UtcNow;

        try
        {
            await _db.WithConnectionAsync(async conn =>
            {
                if (_db.Provider == CloudDatabaseProvider.Postgres)
                {
                    var pg = (NpgsqlConnection)conn;
                    await using var tx = await pg.BeginTransactionAsync();

                    await using (var orgCmd = new NpgsqlCommand(
                        "INSERT INTO organizations (id, name, created_at) VALUES (@id, @name, @created)",
                        pg, tx))
                    {
                        orgCmd.Parameters.AddWithValue("id", orgId);
                        orgCmd.Parameters.AddWithValue("name", orgName);
                        orgCmd.Parameters.AddWithValue("created", now);
                        await orgCmd.ExecuteNonQueryAsync();
                    }

                    await using (var userCmd = new NpgsqlCommand(
                        "INSERT INTO users (id, email, password_hash, organization_id, created_at) VALUES (@id, @email, @hash, @org, @created)",
                        pg, tx))
                    {
                        userCmd.Parameters.AddWithValue("id", userId);
                        userCmd.Parameters.AddWithValue("email", request.Email.Trim().ToLowerInvariant());
                        userCmd.Parameters.AddWithValue("hash", passwordHash);
                        userCmd.Parameters.AddWithValue("org", orgId);
                        userCmd.Parameters.AddWithValue("created", now);
                        await userCmd.ExecuteNonQueryAsync();
                    }

                    await tx.CommitAsync();
                }
                else
                {
                    var sqlite = (SqliteConnection)conn;
                    await using var tx = sqlite.BeginTransaction();

                    await using (var orgCmd = sqlite.CreateCommand())
                    {
                        orgCmd.Transaction = tx;
                        orgCmd.CommandText = "INSERT INTO Organizations (Id, Name, CreatedAt) VALUES ($id, $name, $created)";
                        orgCmd.Parameters.AddWithValue("$id", orgId.ToString());
                        orgCmd.Parameters.AddWithValue("$name", orgName);
                        orgCmd.Parameters.AddWithValue("$created", now.ToString("O"));
                        await orgCmd.ExecuteNonQueryAsync();
                    }

                    await using (var userCmd = sqlite.CreateCommand())
                    {
                        userCmd.Transaction = tx;
                        userCmd.CommandText = "INSERT INTO Users (Id, Email, PasswordHash, OrganizationId, CreatedAt) VALUES ($id, $email, $hash, $org, $created)";
                        userCmd.Parameters.AddWithValue("$id", userId.ToString());
                        userCmd.Parameters.AddWithValue("$email", request.Email.Trim().ToLowerInvariant());
                        userCmd.Parameters.AddWithValue("$hash", passwordHash);
                        userCmd.Parameters.AddWithValue("$org", orgId.ToString());
                        userCmd.Parameters.AddWithValue("$created", now.ToString("O"));
                        await userCmd.ExecuteNonQueryAsync();
                    }

                    tx.Commit();
                }
            });
        }
        catch
        {
            return null;
        }

        var token = CreateToken(userId, orgId, request.Email.Trim().ToLowerInvariant());
        return new AuthResponse(token, orgId.ToString(), orgName, userId.ToString(), request.Email.Trim().ToLowerInvariant());
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        CloudUser? user = null;
        string orgName = string.Empty;

        await _db.WithConnectionAsync(async conn =>
        {
            if (_db.Provider == CloudDatabaseProvider.Postgres)
            {
                var pg = (NpgsqlConnection)conn;
                await using var cmd = new NpgsqlCommand(
                    """
                    SELECT u.id, u.email, u.password_hash, u.organization_id, o.name
                    FROM users u
                    INNER JOIN organizations o ON o.id = u.organization_id
                    WHERE lower(u.email) = lower(@email)
                    LIMIT 1
                    """, pg);
                cmd.Parameters.AddWithValue("email", request.Email.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    user = new CloudUser
                    {
                        Id = reader.GetGuid(0),
                        Email = reader.GetString(1),
                        PasswordHash = reader.GetString(2),
                        OrganizationId = reader.GetGuid(3)
                    };
                    orgName = reader.GetString(4);
                }
            }
            else
            {
                var sqlite = (SqliteConnection)conn;
                await using var cmd = sqlite.CreateCommand();
                cmd.CommandText = """
                    SELECT u.Id, u.Email, u.PasswordHash, u.OrganizationId, o.Name
                    FROM Users u
                    INNER JOIN Organizations o ON o.Id = u.OrganizationId
                    WHERE lower(u.Email) = lower($email)
                    LIMIT 1
                    """;
                cmd.Parameters.AddWithValue("$email", request.Email.Trim());

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    user = new CloudUser
                    {
                        Id = Guid.Parse(reader.GetString(0)),
                        Email = reader.GetString(1),
                        PasswordHash = reader.GetString(2),
                        OrganizationId = Guid.Parse(reader.GetString(3))
                    };
                    orgName = reader.GetString(4);
                }
            }
        });

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        var token = CreateToken(user.Id, user.OrganizationId, user.Email);
        return new AuthResponse(token, user.OrganizationId.ToString(), orgName, user.Id.ToString(), user.Email);
    }

    private string CreateToken(Guid userId, Guid organizationId, string email)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
        var issuer = _configuration["Jwt:Issuer"] ?? "ims-cloud";
        var audience = _configuration["Jwt:Audience"] ?? "ims-clients";

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(CloudClaims.UserId, userId.ToString()),
            new Claim(CloudClaims.OrganizationId, organizationId.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
