using EsgPlatform.Services.Interfaces;
using EsgPlatform.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniExcelLibs;
using System.Security.Claims;

namespace EsgPlatform.Controllers;

/// <summary>Excel 數據上傳控制器</summary>
[Authorize]
public class UploadController : Controller
{
    private readonly IUploadService _uploadService;
    private readonly IGptService _gptService;
    private readonly ILogger<UploadController> _logger;

    // 允許的副檔名
    private static readonly string[] AllowedExtensions = [".xlsx", ".xls", ".csv"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public UploadController(
        IUploadService uploadService,
        IGptService gptService,
        ILogger<UploadController> logger)
    {
        _uploadService = uploadService;
        _gptService    = gptService;
        _logger        = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new UploadViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        var vm = new UploadViewModel { IsSubmitted = true };

        if (file == null || file.Length == 0)
        {
            vm.Message = "請選擇要上傳的 Excel 檔案";
            return View("Index", vm);
        }

        // 驗證檔案大小
        if (file.Length > MaxFileSizeBytes)
        {
            vm.Message = $"檔案大小超過限制（最大 10 MB），目前檔案：{file.Length / 1024 / 1024:F1} MB";
            return View("Index", vm);
        }

        // 驗證副檔名
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            vm.Message = $"不支援的檔案格式，請上傳 .xlsx 或 .csv 格式";
            return View("Index", vm);
        }

        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            if (userId == 0)
                return Challenge();

            using var stream = file.OpenReadStream();
            var (success, error, errors) = await _uploadService.ProcessExcelUploadAsync(stream, userId);

            vm.SuccessCount = success;
            vm.ErrorCount   = error;
            vm.Errors       = errors;
            vm.Message      = success > 0
                ? $"成功匯入 {success} 筆數據，並已完成 CO2e 計算"
                : "沒有成功匯入任何資料，請確認 Excel 格式是否正確";

            _logger.LogInformation(
                "使用者 {UserId} 上傳 Excel，成功 {S} 筆，失敗 {E} 筆",
                userId, success, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理 Excel 上傳時發生例外");
            vm.Message = $"上傳處理失敗：{ex.Message}";
            vm.ErrorCount = 1;
        }

        return View("Index", vm);
    }

    /// <summary>下載 Excel 範本（動態產生，含說明列與範例資料）</summary>
    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        // 使用 MiniExcel 動態產生範本，確保欄位與上傳解析一致
        var templateData = new List<object>
        {
            // 欄位說明列（供使用者參考）
            new { Scope = "範疇(1或2)", Category = "排放類別",  ItemName = "排放項目",    Value = "活動數據量", Unit = "單位" },
            // 範疇一範例
            new { Scope = "1", Category = "固定燃燒源", ItemName = "天然氣",    Value = "500",   Unit = "m³" },
            new { Scope = "1", Category = "移動燃燒源", ItemName = "汽油",      Value = "200",   Unit = "L"  },
            new { Scope = "1", Category = "逸散排放",   ItemName = "冷媒R-22",  Value = "5",     Unit = "kg" },
            // 範疇二範例
            new { Scope = "2", Category = "外購電力",   ItemName = "台電電力",  Value = "10000", Unit = "kWh"},
        };

        var tempPath = Path.Combine(Path.GetTempPath(), $"esg_template_{Guid.NewGuid()}.xlsx");
        try
        {
            MiniExcel.SaveAs(tempPath, templateData, printHeader: true);
            var bytes = System.IO.File.ReadAllBytes(tempPath);
            _logger.LogInformation("使用者 {User} 下載 Excel 範本", User.Identity?.Name);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "ESG數據上傳範本.xlsx");
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    /// <summary>AI 減碳建議（POST，接收排放摘要，回傳 JSON）</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetAiAdvice([FromBody] AiAdviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Summary))
            return BadRequest(new { advice = "請提供碳排放數據摘要" });

        var advice = await _gptService.GetCarbonReductionAdviceAsync(request.Summary);
        return Json(new { advice });
    }

    /// <summary>AI 建議請求模型</summary>
    public class AiAdviceRequest
    {
        public string? Summary { get; set; }
    }
}
