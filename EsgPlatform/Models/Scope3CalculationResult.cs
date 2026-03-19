namespace EsgPlatform.Models;

/// <summary>範疇三計算結果記錄</summary>
public class Scope3CalculationResult
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int CategoryId { get; set; }

    public int MethodId { get; set; }

    /// <summary>輸入欄位的 JSON 快照（供稽核查閱）</summary>
    public string InputDataJson { get; set; } = "{}";

    /// <summary>計算結果（公噸 CO₂e）</summary>
    public decimal TotalCO2e { get; set; }

    /// <summary>盤查期間（例：2024-Q1、2024-01）</summary>
    public string Period { get; set; } = string.Empty;

    public string? Remark { get; set; }

    public DateTime CalculatedAt { get; set; } = DateTime.Now;

    // ── 以下為 JOIN 查詢補充欄位，非資料庫欄位 ──
    public string CategoryName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
