namespace EsgPlatform.Models;

/// <summary>二維導覽列資料表對映模型</summary>
public class NavItem
{
    public int Id { get; set; }

    /// <summary>選單顯示名稱（繁體中文）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>MVC Controller 名稱（一級群組可為 null）</summary>
    public string? Controller { get; set; }

    /// <summary>MVC Action 名稱</summary>
    public string? Action { get; set; }

    /// <summary>Bootstrap Icons 類別（例：bi bi-speedometer2）</summary>
    public string Icon { get; set; } = "bi bi-circle";

    /// <summary>父選單 Id，null 表示一級選單</summary>
    public int? ParentId { get; set; }

    /// <summary>同層顯示排序</summary>
    public int DisplayOrder { get; set; }

    /// <summary>true = 僅管理員可見</summary>
    public bool IsAdminOnly { get; set; }

    /// <summary>子選單清單（由應用程式組裝，非資料庫欄位）</summary>
    public List<NavItem> Children { get; set; } = [];
}
