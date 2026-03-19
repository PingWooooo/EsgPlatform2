namespace EsgPlatform.Models;

/// <summary>ESG 文件上傳紀錄</summary>
public class EsgDocumentUpload
{
    public int Id { get; set; }

    public int ScheduleId { get; set; }

    public int UserId { get; set; }

    /// <summary>原始檔名（供顯示，不用於路徑）</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>實際儲存路徑（使用 GUID 重命名）</summary>
    public string StoredFilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;

    public string? Remark { get; set; }

    // ── JOIN 查詢補充欄位 ──
    public string DocumentName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;

    /// <summary>檔案大小顯示（KB/MB）</summary>
    public string FileSizeDisplay => FileSizeBytes >= 1024 * 1024
        ? $"{FileSizeBytes / 1024.0 / 1024.0:F2} MB"
        : $"{FileSizeBytes / 1024.0:F1} KB";
}
