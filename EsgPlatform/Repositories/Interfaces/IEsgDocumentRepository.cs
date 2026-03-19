using EsgPlatform.Models;

namespace EsgPlatform.Repositories.Interfaces;

/// <summary>ESG 文件排程與上傳紀錄資料存取介面</summary>
public interface IEsgDocumentRepository
{
    // ── 排程 ──
    Task<IEnumerable<EsgDocumentSchedule>> GetAllSchedulesAsync();
    Task<EsgDocumentSchedule?> GetScheduleByIdAsync(int id);
    Task<int> InsertScheduleAsync(EsgDocumentSchedule schedule);
    Task UpdateScheduleAsync(EsgDocumentSchedule schedule);
    Task DeleteScheduleAsync(int id);

    /// <summary>更新下次截止日（文件上傳後自動推算）</summary>
    Task UpdateNextDueDateAsync(int scheduleId, DateTime nextDueDate);

    // ── 上傳紀錄 ──
    Task<int> InsertUploadAsync(EsgDocumentUpload upload);
    Task<IEnumerable<EsgDocumentUpload>> GetUploadsByScheduleIdAsync(int scheduleId);
    Task<EsgDocumentUpload?> GetLatestUploadByScheduleIdAsync(int scheduleId);
    Task<IEnumerable<EsgDocumentUpload>> GetRecentUploadsAsync(int count = 20);
}
