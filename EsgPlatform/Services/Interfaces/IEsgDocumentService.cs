using EsgPlatform.Models;

namespace EsgPlatform.Services.Interfaces;

/// <summary>ESG 文件監控業務邏輯介面</summary>
public interface IEsgDocumentService
{
    // ── 排程 ──
    Task<IEnumerable<EsgDocumentSchedule>> GetSchedulesWithTrafficLightAsync();
    Task<EsgDocumentSchedule?> GetScheduleByIdAsync(int id);
    Task<int> CreateScheduleAsync(EsgDocumentSchedule schedule);
    Task UpdateScheduleAsync(EsgDocumentSchedule schedule);
    Task DeleteScheduleAsync(int id);

    // ── 上傳 ──
    Task<int> UploadDocumentAsync(int scheduleId, int userId, IFormFile file, string? remark);
    Task<IEnumerable<EsgDocumentUpload>> GetUploadHistoryAsync(int scheduleId);

    // ── 紅綠燈計算 ──
    string CalculateTrafficLight(EsgDocumentSchedule schedule);
}
