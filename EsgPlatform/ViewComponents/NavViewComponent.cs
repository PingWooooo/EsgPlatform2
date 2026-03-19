using EsgPlatform.Models;
using EsgPlatform.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EsgPlatform.ViewComponents;

/// <summary>
/// 動態二維導覽列 ViewComponent
/// 從 NavItems 資料表讀取選單，依角色過濾 IsAdminOnly 項目，組裝父子樹狀結構
/// </summary>
public class NavViewComponent : ViewComponent
{
    private readonly INavItemRepository _navRepo;

    public NavViewComponent(INavItemRepository navRepo)
    {
        _navRepo = navRepo;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var isAdmin = UserClaimsPrincipal.IsInRole("Admin");

        // 讀取所有導覽項目
        var allItems = (await _navRepo.GetAllAsync()).ToList();

        // 依角色過濾：非管理員移除 IsAdminOnly 項目
        var filtered = isAdmin
            ? allItems
            : allItems.Where(x => !x.IsAdminOnly).ToList();

        // 組裝父子樹狀結構（只取兩層）
        var roots = filtered
            .Where(x => x.ParentId == null)
            .OrderBy(x => x.DisplayOrder)
            .ToList();

        foreach (var root in roots)
        {
            root.Children = filtered
                .Where(x => x.ParentId == root.Id)
                .OrderBy(x => x.DisplayOrder)
                .ToList();
        }

        return View(roots);
    }
}
