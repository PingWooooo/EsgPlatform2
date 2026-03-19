using Dapper;
using EsgPlatform.Models;
using EsgPlatform.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace EsgPlatform.Repositories;

/// <summary>動態導覽列資料存取實作</summary>
public class NavItemRepository : INavItemRepository
{
    private readonly string _connectionString;

    public NavItemRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("資料庫連線字串未設定");
    }

    public async Task<IEnumerable<NavItem>> GetAllAsync()
    {
        const string sql = @"
            SELECT Id, Title, Controller, Action, Icon, ParentId, DisplayOrder, IsAdminOnly
            FROM NavItems
            ORDER BY ISNULL(ParentId, 0), DisplayOrder";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<NavItem>(sql);
    }

    public async Task<IEnumerable<NavItem>> GetRootItemsAsync()
    {
        const string sql = @"
            SELECT Id, Title, Controller, Action, Icon, ParentId, DisplayOrder, IsAdminOnly
            FROM NavItems
            WHERE ParentId IS NULL
            ORDER BY DisplayOrder";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<NavItem>(sql);
    }

    public async Task<IEnumerable<NavItem>> GetChildrenAsync(int parentId)
    {
        const string sql = @"
            SELECT Id, Title, Controller, Action, Icon, ParentId, DisplayOrder, IsAdminOnly
            FROM NavItems
            WHERE ParentId = @ParentId
            ORDER BY DisplayOrder";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<NavItem>(sql, new { ParentId = parentId });
    }
}
