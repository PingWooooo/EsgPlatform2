using EsgPlatform.Services.Interfaces;
using EsgPlatform.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EsgPlatform.Controllers;

/// <summary>範疇三溫室氣體管理控制器</summary>
[Authorize]
public class Scope3Controller : Controller
{
    private readonly IScope3Service _scope3Service;
    private readonly ILogger<Scope3Controller> _logger;

    public Scope3Controller(IScope3Service scope3Service, ILogger<Scope3Controller> logger)
    {
        _scope3Service = scope3Service;
        _logger        = logger;
    }

    /// <summary>範疇三主頁：顯示類別清單、計算表單、歷史結果</summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vm = new Scope3ViewModel
        {
            Categories    = await _scope3Service.GetCategoriesAsync(),
            RecentResults = await _scope3Service.GetRecentResultsAsync(15),
            CategorySummary = await _scope3Service.GetCategorySummaryAsync()
        };
        return View(vm);
    }

    /// <summary>AJAX：依類別 Id 取得可用計算方法清單（回傳 JSON）</summary>
    [HttpGet]
    public async Task<IActionResult> GetMethods(int categoryId)
    {
        var methods = await _scope3Service.GetMethodsByCategoryAsync(categoryId);
        var result = methods.Select(m => new
        {
            id         = m.Id,
            methodName = m.MethodName
        });
        return Json(result);
    }

    /// <summary>AJAX：依方法 Id 取得 RequiredFieldsJson（回傳 JSON）</summary>
    [HttpGet]
    public async Task<IActionResult> GetRequiredFields(int methodId)
    {
        var method = await _scope3Service.GetMethodAsync(methodId);
        if (method == null)
            return NotFound();

        return Content(method.RequiredFieldsJson, "application/json");
    }

    /// <summary>POST：執行範疇三計算並儲存結果</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(Scope3CalculateFormModel form)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "表單驗證失敗，請確認必填欄位";
            return RedirectToAction("Index");
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (userId == 0) return Challenge();

        try
        {
            var co2e = await _scope3Service.CalculateAndSaveAsync(
                userId, form.CategoryId, form.MethodId,
                form.Fields, form.Period, form.Remark);

            TempData["Success"] = $"範疇三計算完成！CO₂e = {co2e:N4} 公噸";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "範疇三計算失敗，使用者 {UserId}", userId);
            TempData["Error"] = $"計算失敗：{ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "範疇三計算發生例外，使用者 {UserId}", userId);
            TempData["Error"] = "計算時發生系統錯誤，請稍後再試";
        }

        return RedirectToAction("Index");
    }
}
