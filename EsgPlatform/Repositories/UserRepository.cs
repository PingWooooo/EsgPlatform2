using Dapper;
using EsgPlatform.Models;
using EsgPlatform.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace EsgPlatform.Repositories;

/// <summary>使用者資料存取實作</summary>
public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("資料庫連線字串未設定");
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        const string sql = @"
            SELECT u.Id, u.Username, u.PasswordHash, u.Email, u.RoleId, u.CreatedAt, r.RoleName
            FROM Users u
            INNER JOIN Roles r ON u.RoleId = r.Id
            WHERE u.Username = @Username";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT u.Id, u.Username, u.PasswordHash, u.Email, u.RoleId, u.CreatedAt, r.RoleName
            FROM Users u
            INNER JOIN Roles r ON u.RoleId = r.Id
            WHERE u.Id = @Id";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        const string sql = @"
            SELECT u.Id, u.Username, u.Email, u.RoleId, u.IsActive, u.CreatedAt, r.RoleName
            FROM Users u
            INNER JOIN Roles r ON u.RoleId = r.Id
            ORDER BY u.CreatedAt DESC";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<User>(sql);
    }

    public async Task<int> CreateAsync(User user)
    {
        const string sql = @"
            INSERT INTO Users (Username, PasswordHash, Email, RoleId, IsActive, CreatedAt)
            VALUES (@Username, @PasswordHash, @Email, @RoleId, @IsActive, GETDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conn = new SqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(sql, user);
    }

    public async Task UpdateAsync(User user)
    {
        const string sql = @"
            UPDATE Users
            SET Email    = @Email,
                RoleId   = @RoleId,
                IsActive = @IsActive
            WHERE Id = @Id";

        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, user);
    }

    public async Task UpdatePasswordHashAsync(int id, string passwordHash)
    {
        const string sql = "UPDATE Users SET PasswordHash = @PasswordHash WHERE Id = @Id";
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { Id = id, PasswordHash = passwordHash });
    }

    public async Task DeleteAsync(int id)
    {
        const string sql = "DELETE FROM Users WHERE Id = @Id";
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<bool> UsernameExistsAsync(string username, int? excludeId = null)
    {
        const string sql = @"
            SELECT COUNT(1) FROM Users
            WHERE Username = @Username
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)";

        using var conn = new SqlConnection(_connectionString);
        var count = await conn.ExecuteScalarAsync<int>(sql, new { Username = username, ExcludeId = excludeId });
        return count > 0;
    }

    public async Task<bool> EmailExistsAsync(string email, int? excludeId = null)
    {
        const string sql = @"
            SELECT COUNT(1) FROM Users
            WHERE Email = @Email
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)";

        using var conn = new SqlConnection(_connectionString);
        var count = await conn.ExecuteScalarAsync<int>(sql, new { Email = email, ExcludeId = excludeId });
        return count > 0;
    }
}
