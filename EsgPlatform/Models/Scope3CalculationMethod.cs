namespace EsgPlatform.Models;

/// <summary>範疇三計算方法</summary>
public class Scope3CalculationMethod
{
    public int Id { get; set; }

    /// <summary>所屬類別 Id</summary>
    public int CategoryId { get; set; }

    /// <summary>計算方法名稱（例：支出法、供應商特有法）</summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>計算公式代碼：spend / supplier / average / activity_transport / direct</summary>
    public string CalculationFormula { get; set; } = string.Empty;

    /// <summary>
    /// 前端動態欄位 JSON 定義
    /// 格式：[{"fieldName":"amount","label":"金額","type":"number","unit":"千元","required":true},...]
    /// </summary>
    public string RequiredFieldsJson { get; set; } = "[]";

    /// <summary>所屬類別（應用程式層級關聯，非資料庫欄位）</summary>
    public string CategoryName { get; set; } = string.Empty;
}
