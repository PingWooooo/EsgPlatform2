using EsgPlatform.Models;

namespace EsgPlatform.Repositories.Interfaces;

/// <summary>範疇三類別與計算方法資料存取介面</summary>
public interface IScope3Repository
{
    // ── 類別 ──
    Task<IEnumerable<Scope3Category>> GetAllCategoriesAsync();
    Task<Scope3Category?> GetCategoryByIdAsync(int id);
    Task<int> InsertCategoryAsync(Scope3Category category);
    Task UpdateCategoryAsync(Scope3Category category);
    Task DeleteCategoryAsync(int id);

    // ── 計算方法 ──
    Task<IEnumerable<Scope3CalculationMethod>> GetMethodsByCategoryIdAsync(int categoryId);
    Task<Scope3CalculationMethod?> GetMethodByIdAsync(int id);
    Task<int> InsertMethodAsync(Scope3CalculationMethod method);
    Task UpdateMethodAsync(Scope3CalculationMethod method);
    Task DeleteMethodAsync(int id);

    // ── 計算結果 ──
    Task<int> InsertResultAsync(Scope3CalculationResult result);
    Task<IEnumerable<Scope3CalculationResult>> GetResultsByUserIdAsync(int userId);
    Task<IEnumerable<Scope3CalculationResult>> GetRecentResultsAsync(int count = 20);
    Task<Dictionary<int, decimal>> GetCategoryCO2eSummaryAsync();
}
