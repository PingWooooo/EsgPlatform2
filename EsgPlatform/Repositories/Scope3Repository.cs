using Dapper;
using EsgPlatform.Models;
using EsgPlatform.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace EsgPlatform.Repositories;

/// <summary>範疇三資料存取實作</summary>
public class Scope3Repository : IScope3Repository
{
    private readonly string _connectionString;

    public Scope3Repository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("資料庫連線字串未設定");
    }

    // ── 類別 ──

    public async Task<IEnumerable<Scope3Category>> GetAllCategoriesAsync()
    {
        const string sql = "SELECT * FROM Scope3Categories ORDER BY CategoryNumber";
        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<Scope3Category>(sql);
    }

    public async Task<Scope3Category?> GetCategoryByIdAsync(int id)
    {
        const string sql = "SELECT * FROM Scope3Categories WHERE Id = @Id";
        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<Scope3Category>(sql, new { Id = id });
    }

    public async Task<int> InsertCategoryAsync(Scope3Category category)
    {
        const string sql = @"
            INSERT INTO Scope3Categories (CategoryNumber, Name, Description)
            VALUES (@CategoryNumber, @Name, @Description);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conn = new SqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(sql, category);
    }

    public async Task UpdateCategoryAsync(Scope3Category category)
    {
        const string sql = @"
            UPDATE Scope3Categories
            SET CategoryNumber = @CategoryNumber, Name = @Name, Description = @Description
            WHERE Id = @Id";

        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, category);
    }

    public async Task DeleteCategoryAsync(int id)
    {
        // 先刪除子方法，再刪除類別
        const string deleteMethods  = "DELETE FROM Scope3CalculationMethods WHERE CategoryId = @Id";
        const string deleteCategory = "DELETE FROM Scope3Categories WHERE Id = @Id";

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(deleteMethods,  new { Id = id }, tx);
            await conn.ExecuteAsync(deleteCategory, new { Id = id }, tx);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── 計算方法 ──

    public async Task<IEnumerable<Scope3CalculationMethod>> GetMethodsByCategoryIdAsync(int categoryId)
    {
        const string sql = @"
            SELECT m.Id, m.CategoryId, m.MethodName, m.CalculationFormula, m.RequiredFieldsJson,
                   c.Name AS CategoryName
            FROM Scope3CalculationMethods m
            INNER JOIN Scope3Categories c ON m.CategoryId = c.Id
            WHERE m.CategoryId = @CategoryId
            ORDER BY m.Id";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<Scope3CalculationMethod>(sql, new { CategoryId = categoryId });
    }

    public async Task<Scope3CalculationMethod?> GetMethodByIdAsync(int id)
    {
        const string sql = @"
            SELECT m.Id, m.CategoryId, m.MethodName, m.CalculationFormula, m.RequiredFieldsJson,
                   c.Name AS CategoryName
            FROM Scope3CalculationMethods m
            INNER JOIN Scope3Categories c ON m.CategoryId = c.Id
            WHERE m.Id = @Id";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<Scope3CalculationMethod>(sql, new { Id = id });
    }

    public async Task<int> InsertMethodAsync(Scope3CalculationMethod method)
    {
        const string sql = @"
            INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson)
            VALUES (@CategoryId, @MethodName, @CalculationFormula, @RequiredFieldsJson);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conn = new SqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(sql, method);
    }

    public async Task UpdateMethodAsync(Scope3CalculationMethod method)
    {
        const string sql = @"
            UPDATE Scope3CalculationMethods
            SET CategoryId = @CategoryId, MethodName = @MethodName,
                CalculationFormula = @CalculationFormula, RequiredFieldsJson = @RequiredFieldsJson
            WHERE Id = @Id";

        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, method);
    }

    public async Task DeleteMethodAsync(int id)
    {
        const string sql = "DELETE FROM Scope3CalculationMethods WHERE Id = @Id";
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    // ── 計算結果 ──

    public async Task<int> InsertResultAsync(Scope3CalculationResult result)
    {
        const string sql = @"
            INSERT INTO Scope3CalculationResults
                (UserId, CategoryId, MethodId, InputDataJson, TotalCO2e, Period, Remark, CalculatedAt)
            VALUES
                (@UserId, @CategoryId, @MethodId, @InputDataJson, @TotalCO2e, @Period, @Remark, GETDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conn = new SqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(sql, result);
    }

    public async Task<IEnumerable<Scope3CalculationResult>> GetResultsByUserIdAsync(int userId)
    {
        const string sql = @"
            SELECT r.Id, r.UserId, r.CategoryId, r.MethodId, r.InputDataJson,
                   r.TotalCO2e, r.Period, r.Remark, r.CalculatedAt,
                   c.Name AS CategoryName, m.MethodName, u.Username
            FROM Scope3CalculationResults r
            INNER JOIN Scope3Categories c         ON r.CategoryId = c.Id
            INNER JOIN Scope3CalculationMethods m ON r.MethodId   = m.Id
            INNER JOIN Users u                    ON r.UserId     = u.Id
            WHERE r.UserId = @UserId
            ORDER BY r.CalculatedAt DESC";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<Scope3CalculationResult>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<Scope3CalculationResult>> GetRecentResultsAsync(int count = 20)
    {
        const string sql = @"
            SELECT TOP (@Count)
                   r.Id, r.UserId, r.CategoryId, r.MethodId, r.InputDataJson,
                   r.TotalCO2e, r.Period, r.Remark, r.CalculatedAt,
                   c.Name AS CategoryName, m.MethodName, u.Username
            FROM Scope3CalculationResults r
            INNER JOIN Scope3Categories c         ON r.CategoryId = c.Id
            INNER JOIN Scope3CalculationMethods m ON r.MethodId   = m.Id
            INNER JOIN Users u                    ON r.UserId     = u.Id
            ORDER BY r.CalculatedAt DESC";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<Scope3CalculationResult>(sql, new { Count = count });
    }

    public async Task<Dictionary<int, decimal>> GetCategoryCO2eSummaryAsync()
    {
        const string sql = @"
            SELECT r.CategoryId, SUM(r.TotalCO2e) AS TotalCO2e
            FROM Scope3CalculationResults r
            GROUP BY r.CategoryId";

        using var conn = new SqlConnection(_connectionString);
        var rows = await conn.QueryAsync<(int CategoryId, decimal TotalCO2e)>(sql);
        return rows.ToDictionary(r => r.CategoryId, r => r.TotalCO2e);
    }
}
