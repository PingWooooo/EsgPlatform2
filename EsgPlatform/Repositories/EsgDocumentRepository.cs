using Dapper;
using EsgPlatform.Models;
using EsgPlatform.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace EsgPlatform.Repositories;

/// <summary>ESG 文件排程與上傳紀錄資料存取實作</summary>
public class EsgDocumentRepository : IEsgDocumentRepository
{
    private readonly string _connectionString;

    public EsgDocumentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("資料庫連線字串未設定");
    }

    // ── 排程 ──

    public async Task<IEnumerable<EsgDocumentSchedule>> GetAllSchedulesAsync()
    {
        const string sql = @"
            SELECT s.Id, s.DocumentName, s.Frequency, s.ResponsiblePerson,
                   s.WarningDays, s.NextDueDate, s.IsActive, s.CreatedAt,
                   MAX(u.UploadedAt) AS LastUploadedAt
            FROM EsgDocumentSchedules s
            LEFT JOIN EsgDocumentUploads u ON s.Id = u.ScheduleId
            WHERE s.IsActive = 1
            GROUP BY s.Id, s.DocumentName, s.Frequency, s.ResponsiblePerson,
                     s.WarningDays, s.NextDueDate, s.IsActive, s.CreatedAt
            ORDER BY s.NextDueDate";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<EsgDocumentSchedule>(sql);
    }

    public async Task<EsgDocumentSchedule?> GetScheduleByIdAsync(int id)
    {
        const string sql = @"
            SELECT s.Id, s.DocumentName, s.Frequency, s.ResponsiblePerson,
                   s.WarningDays, s.NextDueDate, s.IsActive, s.CreatedAt,
                   MAX(u.UploadedAt) AS LastUploadedAt
            FROM EsgDocumentSchedules s
            LEFT JOIN EsgDocumentUploads u ON s.Id = u.ScheduleId
            WHERE s.Id = @Id
            GROUP BY s.Id, s.DocumentName, s.Frequency, s.ResponsiblePerson,
                     s.WarningDays, s.NextDueDate, s.IsActive, s.CreatedAt";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<EsgDocumentSchedule>(sql, new { Id = id });
    }

    public async Task<int> InsertScheduleAsync(EsgDocumentSchedule schedule)
    {
        const string sql = @"
            INSERT INTO EsgDocumentSchedules
                (DocumentName, Frequency, ResponsiblePerson, WarningDays, NextDueDate, IsActive, CreatedAt)
            VALUES
                (@DocumentName, @Frequency, @ResponsiblePerson, @WarningDays, @NextDueDate, 1, GETDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conn = new SqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(sql, schedule);
    }

    public async Task UpdateScheduleAsync(EsgDocumentSchedule schedule)
    {
        const string sql = @"
            UPDATE EsgDocumentSchedules
            SET DocumentName      = @DocumentName,
                Frequency         = @Frequency,
                ResponsiblePerson = @ResponsiblePerson,
                WarningDays       = @WarningDays,
                NextDueDate       = @NextDueDate
            WHERE Id = @Id";

        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, schedule);
    }

    public async Task DeleteScheduleAsync(int id)
    {
        // 軟刪除（設定 IsActive = 0）以保留歷史紀錄
        const string sql = "UPDATE EsgDocumentSchedules SET IsActive = 0 WHERE Id = @Id";
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    public async Task UpdateNextDueDateAsync(int scheduleId, DateTime nextDueDate)
    {
        const string sql = "UPDATE EsgDocumentSchedules SET NextDueDate = @NextDueDate WHERE Id = @Id";
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { NextDueDate = nextDueDate.Date, Id = scheduleId });
    }

    // ── 上傳紀錄 ──

    public async Task<int> InsertUploadAsync(EsgDocumentUpload upload)
    {
        const string sql = @"
            INSERT INTO EsgDocumentUploads
                (ScheduleId, UserId, OriginalFileName, StoredFilePath, FileSizeBytes, UploadedAt, Remark)
            VALUES
                (@ScheduleId, @UserId, @OriginalFileName, @StoredFilePath, @FileSizeBytes, GETDATE(), @Remark);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conn = new SqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(sql, upload);
    }

    public async Task<IEnumerable<EsgDocumentUpload>> GetUploadsByScheduleIdAsync(int scheduleId)
    {
        const string sql = @"
            SELECT u.Id, u.ScheduleId, u.UserId, u.OriginalFileName, u.StoredFilePath,
                   u.FileSizeBytes, u.UploadedAt, u.Remark,
                   s.DocumentName, usr.Username
            FROM EsgDocumentUploads u
            INNER JOIN EsgDocumentSchedules s ON u.ScheduleId = s.Id
            INNER JOIN Users usr              ON u.UserId     = usr.Id
            WHERE u.ScheduleId = @ScheduleId
            ORDER BY u.UploadedAt DESC";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<EsgDocumentUpload>(sql, new { ScheduleId = scheduleId });
    }

    public async Task<EsgDocumentUpload?> GetLatestUploadByScheduleIdAsync(int scheduleId)
    {
        const string sql = @"
            SELECT TOP 1 u.Id, u.ScheduleId, u.UserId, u.OriginalFileName,
                   u.StoredFilePath, u.FileSizeBytes, u.UploadedAt, u.Remark
            FROM EsgDocumentUploads u
            WHERE u.ScheduleId = @ScheduleId
            ORDER BY u.UploadedAt DESC";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<EsgDocumentUpload>(sql, new { ScheduleId = scheduleId });
    }

    public async Task<IEnumerable<EsgDocumentUpload>> GetRecentUploadsAsync(int count = 20)
    {
        const string sql = @"
            SELECT TOP (@Count)
                   u.Id, u.ScheduleId, u.UserId, u.OriginalFileName, u.StoredFilePath,
                   u.FileSizeBytes, u.UploadedAt, u.Remark,
                   s.DocumentName, usr.Username
            FROM EsgDocumentUploads u
            INNER JOIN EsgDocumentSchedules s ON u.ScheduleId = s.Id
            INNER JOIN Users usr              ON u.UserId     = usr.Id
            ORDER BY u.UploadedAt DESC";

        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<EsgDocumentUpload>(sql, new { Count = count });
    }
}
