using EsgPlatform.Models;

namespace EsgPlatform.ViewModels;

/// <summary>範疇三管理頁面 ViewModel</summary>
public class Scope3ViewModel
{
    /// <summary>所有範疇三類別（供下拉選單）</summary>
    public IEnumerable<Scope3Category> Categories { get; set; } = [];

    /// <summary>最近計算結果</summary>
    public IEnumerable<Scope3CalculationResult> RecentResults { get; set; } = [];

    /// <summary>各類別 CO2e 加總（供堆疊橫向條形圖，Key = CategoryId）</summary>
    public Dictionary<int, decimal> CategorySummary { get; set; } = [];

    /// <summary>範疇三總計 CO2e（公噸）</summary>
    public decimal TotalCO2e => CategorySummary.Values.Sum();
}

/// <summary>範疇三計算表單提交模型</summary>
public class Scope3CalculateFormModel
{
    public int CategoryId { get; set; }
    public int MethodId { get; set; }
    public string Period { get; set; } = string.Empty;
    public string? Remark { get; set; }

    /// <summary>動態欄位鍵值對（fieldName → 使用者輸入值）</summary>
    public Dictionary<string, string> Fields { get; set; } = [];
}
