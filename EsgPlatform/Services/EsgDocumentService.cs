using EsgPlatform.Models;
using EsgPlatform.Repositories.Interfaces;
using EsgPlatform.Services.Interfaces;

namespace EsgPlatform.Services;

/// <summary>
/// ESG 文件監控服務
/// 紅綠燈邏輯：
///   若今日 &gt; 截止日：有效上傳記錄 → 綠燈；否則 → 紅燈
///   若今日 ≤ 截止日：已上傳 → 綠燈；剩餘天數 ≤ 預警天數 → 黃燈；否則 → 綠燈（充裕）
/// </summary>
public class EsgDocumentService : IEsgDocumentService
{
    private readonly IEsgDocumentRepository _repo;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EsgDocumentService> _logger;

    // 允許上傳的文件副檔名白名單
    private static readonly HashSet<string> AllowedExtensions =
        [".pdf", ".docx", ".doc", ".xlsx", ".xls", ".csv", ".txt", ".zip"];

    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    public EsgDocumentService(
        IEsgDocumentRepository repo,
        IWebHostEnvironment env,
        ILogger<EsgDocumentService> logger)
    {
        _repo   = repo;
        _env    = env;
        _logger = logger;
    }

    public async Task<IEnumerable<EsgDocumentSchedule>> GetSchedulesWithTrafficLightAsync()
    {
        var schedules = (await _repo.GetAllSchedulesAsync()).ToList();

        foreach (var s in schedules)
        {
            s.TrafficLight = CalculateTrafficLight(s);
        }

        return schedules;
    }

    public Task<EsgDocumentSchedule?> GetScheduleByIdAsync(int id)
        => _repo.GetScheduleByIdAsync(id);

    public Task<int> CreateScheduleAsync(EsgDocumentSchedule schedule)
        => _repo.InsertScheduleAsync(schedule);

    public Task UpdateScheduleAsync(EsgDocumentSchedule schedule)
        => _repo.UpdateScheduleAsync(schedule);

    public Task DeleteScheduleAsync(int id)
        => _repo.DeleteScheduleAsync(id);

    public async Task<int> UploadDocumentAsync(
        int scheduleId, int userId, IFormFile file, string? remark)
    {
        // 驗證檔案大小
        if (file.Length > MaxFileSizeBytes)
            throw new InvalidOperationException(
                $"檔案大小超過限制（最大 50 MB），目前：{file.Length / 1024 / 1024:F1} MB");

        // 驗證副檔名白名單
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"不支援的檔案格式：{ext}");

        // 建立儲存目錄
        var uploadRoot = Path.Combine(_env.WebRootPath, "uploads", "esg-documents");
        Directory.CreateDirectory(uploadRoot);

        // GUID 重命名，防止路徑穿越與惡意檔名
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var storedPath = Path.Combine(uploadRoot, storedName);

        // 儲存檔案
        await using (var stream = new FileStream(storedPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var upload = new EsgDocumentUpload
        {
            ScheduleId       = scheduleId,
            UserId           = userId,
            OriginalFileName = file.FileName,
            StoredFilePath   = $"/uploads/esg-documents/{storedName}",
            FileSizeBytes    = file.Length,
            Remark           = remark,
            UploadedAt       = DateTime.Now
        };

        var uploadId = await _repo.InsertUploadAsync(upload);

        // 上傳成功後，自動推算下次截止日
        var schedule = await _repo.GetScheduleByIdAsync(scheduleId);
        if (schedule != null)
        {
            var nextDue = CalculateNextDueDate(schedule.Frequency, schedule.NextDueDate);
            await _repo.UpdateNextDueDateAsync(scheduleId, nextDue);

            _logger.LogInformation(
                "ESG 文件上傳完成：排程 {ScheduleId}（{Name}），使用者 {UserId}，" +
                "原始檔名={FileName}，下次截止日更新為 {NextDue:yyyy-MM-dd}",
                scheduleId, schedule.DocumentName, userId, file.FileName, nextDue);
        }

        return uploadId;
    }

    public Task<IEnumerable<EsgDocumentUpload>> GetUploadHistoryAsync(int scheduleId)
        => _repo.GetUploadsByScheduleIdAsync(scheduleId);

    /// <summary>
    /// 計算紅綠燈狀態
    /// Green：期限內已上傳
    /// Yellow：未上傳且距截止日 ≤ 預警天數
    /// Red：已過截止日且未上傳
    /// </summary>
    public string CalculateTrafficLight(EsgDocumentSchedule schedule)
    {
        var today   = DateTime.Today;
        var dueDate = schedule.NextDueDate.Date;

        // 已過截止日
        if (today > dueDate)
        {
            // 若有在截止日當天或之前的上傳記錄，視為準時
            if (schedule.LastUploadedAt.HasValue &&
                schedule.LastUploadedAt.Value.Date <= dueDate)
                return "Green";

            return "Red";
        }

        // 截止日尚未到
        if (schedule.LastUploadedAt.HasValue)
            return "Green"; // 已上傳

        // 未上傳，計算剩餘天數
        var daysLeft = (dueDate - today).Days;
        return daysLeft <= schedule.WarningDays ? "Yellow" : "Green";
    }

    /// <summary>依頻率推算下次截止日</summary>
    private static DateTime CalculateNextDueDate(string frequency, DateTime currentDueDate)
    {
        return frequency == "Monthly"
            ? currentDueDate.AddMonths(1)
            : currentDueDate.AddYears(1);
    }
}
