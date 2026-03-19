using EsgPlatform.Models;
using EsgPlatform.Services.Interfaces;
using EsgPlatform.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EsgPlatform.Controllers;

/// <summary>ESG 文件監控控制器</summary>
[Authorize]
public class EsgDocumentController : Controller
{
    private readonly IEsgDocumentService _docService;
    private readonly ILogger<EsgDocumentController> _logger;

    public EsgDocumentController(
        IEsgDocumentService docService,
        ILogger<EsgDocumentController> logger)
    {
        _docService = docService;
        _logger     = logger;
    }

    /// <summary>文件監控看板（紅綠燈清單）</summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var schedules = await _docService.GetSchedulesWithTrafficLightAsync();
        var vm = new EsgDocumentIndexViewModel { Schedules = schedules };
        return View(vm);
    }

    /// <summary>排程維護清單（管理員）</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Schedules()
    {
        var schedules = await _docService.GetSchedulesWithTrafficLightAsync();
        return View(schedules);
    }

    /// <summary>新增排程表單（管理員）</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult CreateSchedule()
    {
        var model = new EsgDocumentScheduleFormModel
        {
            WarningDays = 7,
            NextDueDate = DateTime.Today.AddMonths(1)
        };
        return View("ScheduleForm", model);
    }

    /// <summary>儲存新增排程（管理員）</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSchedule(EsgDocumentScheduleFormModel form)
    {
        if (!ModelState.IsValid) return View("ScheduleForm", form);

        var schedule = new EsgDocumentSchedule
        {
            DocumentName      = form.DocumentName,
            Frequency         = form.Frequency,
            ResponsiblePerson = form.ResponsiblePerson,
            WarningDays       = form.WarningDays,
            NextDueDate       = form.NextDueDate
        };

        await _docService.CreateScheduleAsync(schedule);
        TempData["Success"] = $"排程「{form.DocumentName}」新增成功";
        return RedirectToAction("Schedules");
    }

    /// <summary>編輯排程表單（管理員）</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditSchedule(int id)
    {
        var schedule = await _docService.GetScheduleByIdAsync(id);
        if (schedule == null) return NotFound();

        var form = new EsgDocumentScheduleFormModel
        {
            Id                = schedule.Id,
            DocumentName      = schedule.DocumentName,
            Frequency         = schedule.Frequency,
            ResponsiblePerson = schedule.ResponsiblePerson,
            WarningDays       = schedule.WarningDays,
            NextDueDate       = schedule.NextDueDate
        };
        return View("ScheduleForm", form);
    }

    /// <summary>儲存編輯排程（管理員）</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditSchedule(EsgDocumentScheduleFormModel form)
    {
        if (!ModelState.IsValid) return View("ScheduleForm", form);

        var schedule = new EsgDocumentSchedule
        {
            Id                = form.Id,
            DocumentName      = form.DocumentName,
            Frequency         = form.Frequency,
            ResponsiblePerson = form.ResponsiblePerson,
            WarningDays       = form.WarningDays,
            NextDueDate       = form.NextDueDate
        };

        await _docService.UpdateScheduleAsync(schedule);
        TempData["Success"] = $"排程「{form.DocumentName}」更新成功";
        return RedirectToAction("Schedules");
    }

    /// <summary>刪除排程（管理員，軟刪除）</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        await _docService.DeleteScheduleAsync(id);
        TempData["Success"] = "排程已停用";
        return RedirectToAction("Schedules");
    }

    /// <summary>文件上傳頁面</summary>
    [HttpGet]
    public async Task<IActionResult> Upload(int scheduleId)
    {
        var schedule = await _docService.GetScheduleByIdAsync(scheduleId);
        if (schedule == null) return NotFound();

        var form = new EsgDocumentUploadFormModel
        {
            ScheduleId   = scheduleId,
            DocumentName = schedule.DocumentName
        };
        return View(form);
    }

    /// <summary>執行文件上傳</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(EsgDocumentUploadFormModel form)
    {
        if (form.File == null || form.File.Length == 0)
        {
            ModelState.AddModelError("File", "請選擇要上傳的文件");
            return View(form);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (userId == 0) return Challenge();

        try
        {
            await _docService.UploadDocumentAsync(
                form.ScheduleId, userId, form.File, form.Remark);

            TempData["Success"] = $"文件「{form.File.FileName}」上傳成功，系統已自動更新下次截止日";
            return RedirectToAction("Index");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "文件上傳驗證失敗，排程 {ScheduleId}", form.ScheduleId);
            ModelState.AddModelError("File", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文件上傳發生例外，排程 {ScheduleId}", form.ScheduleId);
            ModelState.AddModelError("", "上傳失敗：" + ex.Message);
        }

        return View(form);
    }

    /// <summary>上傳歷史記錄（AJAX 或頁面）</summary>
    [HttpGet]
    public async Task<IActionResult> UploadHistory(int scheduleId)
    {
        var uploads = await _docService.GetUploadHistoryAsync(scheduleId);
        return View(uploads);
    }
}
