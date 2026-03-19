using EsgPlatform.Models;

namespace EsgPlatform.Services.Interfaces;

/// <summary>範疇三業務邏輯介面</summary>
public interface IScope3Service
{
    Task<IEnumerable<Scope3Category>> GetCategoriesAsync();
    Task<IEnumerable<Scope3CalculationMethod>> GetMethodsByCategoryAsync(int categoryId);
    Task<Scope3CalculationMethod?> GetMethodAsync(int methodId);

    /// <summary>執行計算並儲存結果，回傳 CO2e（公噸）</summary>
    Task<decimal> CalculateAndSaveAsync(
        int userId, int categoryId, int methodId,
        Dictionary<string, string> inputFields, string period, string? remark);

    Task<IEnumerable<Scope3CalculationResult>> GetRecentResultsAsync(int count = 20);
    Task<Dictionary<int, decimal>> GetCategorySummaryAsync();
}
