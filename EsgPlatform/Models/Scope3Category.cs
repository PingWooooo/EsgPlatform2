namespace EsgPlatform.Models;

/// <summary>範疇三 15 項類別</summary>
public class Scope3Category
{
    public int Id { get; set; }

    /// <summary>類別編號（1 ~ 15）</summary>
    public int CategoryNumber { get; set; }

    /// <summary>中文類別名稱</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>類別說明</summary>
    public string Description { get; set; } = string.Empty;
}
