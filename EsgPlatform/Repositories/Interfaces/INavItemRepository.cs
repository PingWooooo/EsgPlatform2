using EsgPlatform.Models;

namespace EsgPlatform.Repositories.Interfaces;

/// <summary>動態導覽列資料存取介面</summary>
public interface INavItemRepository
{
    /// <summary>取得所有導覽項目（含父子結構）</summary>
    Task<IEnumerable<NavItem>> GetAllAsync();

    /// <summary>取得一級選單（ParentId 為 null）</summary>
    Task<IEnumerable<NavItem>> GetRootItemsAsync();

    /// <summary>依父選單 Id 取得子選單</summary>
    Task<IEnumerable<NavItem>> GetChildrenAsync(int parentId);
}
