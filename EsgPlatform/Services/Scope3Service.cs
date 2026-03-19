using System.Text.Json;
using EsgPlatform.Models;
using EsgPlatform.Repositories.Interfaces;
using EsgPlatform.Services.Interfaces;

namespace EsgPlatform.Services;

/// <summary>
/// 範疇三計算服務
/// 支援計算公式：spend / supplier / average / activity_transport / direct
/// </summary>
public class Scope3Service : IScope3Service
{
    private readonly IScope3Repository _repo;
    private readonly ILogger<Scope3Service> _logger;

    public Scope3Service(IScope3Repository repo, ILogger<Scope3Service> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public Task<IEnumerable<Scope3Category>> GetCategoriesAsync()
        => _repo.GetAllCategoriesAsync();

    public Task<IEnumerable<Scope3CalculationMethod>> GetMethodsByCategoryAsync(int categoryId)
        => _repo.GetMethodsByCategoryIdAsync(categoryId);

    public Task<Scope3CalculationMethod?> GetMethodAsync(int methodId)
        => _repo.GetMethodByIdAsync(methodId);

    public async Task<decimal> CalculateAndSaveAsync(
        int userId, int categoryId, int methodId,
        Dictionary<string, string> inputFields, string period, string? remark)
    {
        var method = await _repo.GetMethodByIdAsync(methodId)
            ?? throw new InvalidOperationException($"找不到計算方法 Id={methodId}");

        // 計算 CO2e（kg）依公式類型分派
        var co2eKg = method.CalculationFormula switch
        {
            "spend"              => CalculateBySpend(inputFields),
            "supplier"           => CalculateBySupplier(inputFields),
            "average"            => CalculateByAverage(inputFields),
            "activity_transport" => CalculateByActivityTransport(inputFields),
            "direct"             => CalculateByDirect(inputFields),
            _                    => throw new InvalidOperationException($"未知計算公式：{method.CalculationFormula}")
        };

        var co2eTonne = co2eKg / 1000m;

        var result = new Scope3CalculationResult
        {
            UserId        = userId,
            CategoryId    = categoryId,
            MethodId      = methodId,
            InputDataJson = JsonSerializer.Serialize(inputFields),
            TotalCO2e     = co2eTonne,
            Period        = period,
            Remark        = remark,
            CalculatedAt  = DateTime.Now
        };

        await _repo.InsertResultAsync(result);

        _logger.LogInformation(
            "範疇三計算完成：使用者 {UserId}，類別 {CategoryId}，方法 {MethodId}，CO2e={CO2e:F4} 公噸，期間={Period}",
            userId, categoryId, methodId, co2eTonne, period);

        return co2eTonne;
    }

    public Task<IEnumerable<Scope3CalculationResult>> GetRecentResultsAsync(int count = 20)
        => _repo.GetRecentResultsAsync(count);

    public Task<Dictionary<int, decimal>> GetCategorySummaryAsync()
        => _repo.GetCategoryCO2eSummaryAsync();

    // ── 各公式計算邏輯 ──

    /// <summary>
    /// 支出法：CO2e(kg) = (amount / 1000) × emissionFactor
    /// amount 單位：千元；emissionFactor 單位：kg CO2e / 千元
    /// </summary>
    private static decimal CalculateBySpend(Dictionary<string, string> f)
    {
        var amount         = ParseDecimal(f, "amount");
        var emissionFactor = ParseDecimal(f, "emissionFactor");
        return amount * emissionFactor;
    }

    /// <summary>
    /// 供應商特有法：CO2e(kg) = quantity × supplierFactor
    /// </summary>
    private static decimal CalculateBySupplier(Dictionary<string, string> f)
    {
        var quantity       = ParseDecimal(f, "quantity");
        var supplierFactor = ParseDecimal(f, "supplierFactor");
        return quantity * supplierFactor;
    }

    /// <summary>
    /// 平均資料法：CO2e(kg) = weight × emissionFactor
    /// </summary>
    private static decimal CalculateByAverage(Dictionary<string, string> f)
    {
        var weight         = ParseDecimal(f, "weight");
        var emissionFactor = ParseDecimal(f, "emissionFactor");
        return weight * emissionFactor;
    }

    /// <summary>
    /// 活動數據法（運輸）：CO2e(kg) = weight(公噸) × distance(km) × emissionFactor(kg CO2e/公噸·km)
    /// </summary>
    private static decimal CalculateByActivityTransport(Dictionary<string, string> f)
    {
        var weight         = ParseDecimal(f, "weight");
        var distance       = ParseDecimal(f, "distance");
        var emissionFactor = ParseDecimal(f, "emissionFactor");
        return weight * distance * emissionFactor;
    }

    /// <summary>
    /// 直接法：CO2e(kg) = activityAmount × emissionFactor
    /// 亦用於員工通勤（人次·km × 係數）、商務旅行（距離 × 係數）等
    /// 若有 quantity 欄位則額外乘以 quantity
    /// </summary>
    private static decimal CalculateByDirect(Dictionary<string, string> f)
    {
        var activityAmount = ParseDecimal(f, "activityAmount");
        var emissionFactor = ParseDecimal(f, "emissionFactor");

        // 部分方法（如電力使用法）有額外的 quantity 欄位
        if (f.TryGetValue("quantity", out var qStr) && decimal.TryParse(qStr, out var qty))
            return activityAmount * qty * emissionFactor;

        return activityAmount * emissionFactor;
    }

    private static decimal ParseDecimal(Dictionary<string, string> fields, string key)
    {
        if (!fields.TryGetValue(key, out var raw) || !decimal.TryParse(raw, out var value))
            throw new InvalidOperationException($"缺少或無效的輸入欄位：{key}（值：{raw}）");
        return value;
    }
}
