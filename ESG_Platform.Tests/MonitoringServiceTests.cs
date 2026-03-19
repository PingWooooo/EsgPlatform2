using EsgPlatform.Models;
using EsgPlatform.Repositories.Interfaces;
using EsgPlatform.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ESG_Platform.Tests;

/// <summary>
/// 監控服務測試：模擬不同日期情境，驗證燈號狀態碼是否正確。
/// 規則：
///   月報（WarningDays=7）：剩 ≤7 天未上傳 → Yellow；逾期未上傳 → Red
///   年報（WarningDays=90）：剩 ≤90 天未上傳 → Yellow；逾期未上傳 → Red
///   已上傳且在期限內 → Green
/// </summary>
public class MonitoringServiceTests
{
    private readonly Mock<IReportScheduleRepository> _scheduleRepoMock;
    private readonly Mock<IReportStatusLogRepository> _statusRepoMock;
    private readonly Mock<IRawDataRepository> _rawDataRepoMock;
    private readonly Mock<ILogger<MonitoringService>> _loggerMock;
    private readonly MonitoringService _service;

    public MonitoringServiceTests()
    {
        _scheduleRepoMock = new Mock<IReportScheduleRepository>();
        _statusRepoMock   = new Mock<IReportStatusLogRepository>();
        _rawDataRepoMock  = new Mock<IRawDataRepository>();
        _loggerMock       = new Mock<ILogger<MonitoringService>>();
        _service          = new MonitoringService(
            _scheduleRepoMock.Object,
            _statusRepoMock.Object,
            _rawDataRepoMock.Object,
            _loggerMock.Object);
    }

    // ─── 月報：剩 7 天 → Yellow ──────────────────────────────

    [Fact]
    public void CalculateStatus_Monthly_7DaysLeft_NoUpload_ReturnsYellow()
    {
        // Arrange：今天距截止日剛好 7 天（恰好等於 WarningDays）
        var today   = DateTime.Today;
        var dueDate = DateOnly.FromDateTime(today.AddDays(7));

        var schedule = new ReportSchedule
        {
            Id          = 1,
            Frequency   = "Monthly",
            WarningDays = 7,
            NextDueDate = dueDate
        };

        // Act：尚未上傳（lastUploadDate = null）
        var status = _service.CalculateStatus(schedule, lastUploadDate: null);

        // Assert
        Assert.Equal("Yellow", status);
    }

    [Fact]
    public void CalculateStatus_Monthly_6DaysLeft_NoUpload_ReturnsYellow()
    {
        // Arrange：剩 6 天（在 WarningDays=7 內）
        var today   = DateTime.Today;
        var dueDate = DateOnly.FromDateTime(today.AddDays(6));

        var schedule = new ReportSchedule
        {
            Frequency   = "Monthly",
            WarningDays = 7,
            NextDueDate = dueDate
        };

        var status = _service.CalculateStatus(schedule, null);

        Assert.Equal("Yellow", status);
    }

    [Fact]
    public void CalculateStatus_Monthly_8DaysLeft_NoUpload_ReturnsGreen()
    {
        // Arrange：剩 8 天（超過 WarningDays=7）→ 應為 Green
        var today   = DateTime.Today;
        var dueDate = DateOnly.FromDateTime(today.AddDays(8));

        var schedule = new ReportSchedule
        {
            Frequency   = "Monthly",
            WarningDays = 7,
            NextDueDate = dueDate
        };

        var status = _service.CalculateStatus(schedule, null);

        Assert.Equal("Green", status);
    }

    // ─── 年報：剩 90 天 → Yellow ─────────────────────────────

    [Fact]
    public void CalculateStatus_Yearly_90DaysLeft_NoUpload_ReturnsYellow()
    {
        // Arrange：今天距截止日剛好 90 天（WarningDays=90）
        var today   = DateTime.Today;
        var dueDate = DateOnly.FromDateTime(today.AddDays(90));

        var schedule = new ReportSchedule
        {
            Frequency   = "Yearly",
            WarningDays = 90,
            NextDueDate = dueDate
        };

        var status = _service.CalculateStatus(schedule, null);

        Assert.Equal("Yellow", status);
    }

    [Fact]
    public void CalculateStatus_Yearly_91DaysLeft_NoUpload_ReturnsGreen()
    {
        // Arrange：剩 91 天（超過 WarningDays=90）→ Green
        var today   = DateTime.Today;
        var dueDate = DateOnly.FromDateTime(today.AddDays(91));

        var schedule = new ReportSchedule
        {
            Frequency   = "Yearly",
            WarningDays = 90,
            NextDueDate = dueDate
        };

        var status = _service.CalculateStatus(schedule, null);

        Assert.Equal("Green", status);
    }

    // ─── 逾期 → Red ──────────────────────────────────────────

    [Fact]
    public void CalculateStatus_Overdue_NoUpload_ReturnsRed()
    {
        // Arrange：截止日已過去 1 天，且沒有上傳記錄
        var today   = DateTime.Today;
        var dueDate = DateOnly.FromDateTime(today.AddDays(-1));

        var schedule = new ReportSchedule
        {
            Frequency   = "Monthly",
            WarningDays = 7,
            NextDueDate = dueDate
        };

        var status = _service.CalculateStatus(schedule, null);

        Assert.Equal("Red", status);
    }

    [Fact]
    public void CalculateStatus_LongOverdue_NoUpload_ReturnsRed()
    {
        // Arrange：截止日已過去 30 天
        var today   = DateTime.Today;
        var dueDate = DateOnly.FromDateTime(today.AddDays(-30));

        var schedule = new ReportSchedule
        {
            Frequency   = "Yearly",
            WarningDays = 90,
            NextDueDate = dueDate
        };

        var status = _service.CalculateStatus(schedule, null);

        Assert.Equal("Red", status);
    }

    // ─── 已上傳 → Green ──────────────────────────────────────

    [Fact]
    public void CalculateStatus_Overdue_WithValidUpload_ReturnsGreen()
    {
        // Arrange：截止日已過，但在期限內已有上傳記錄
        var today      = DateTime.Today;
        var dueDate    = DateOnly.FromDateTime(today.AddDays(-5));
        var uploadDate = today.AddDays(-10); // 截止日前 5 天上傳

        var schedule = new ReportSchedule
        {
            Frequency   = "Monthly",
            WarningDays = 7,
            NextDueDate = dueDate
        };

        var status = _service.CalculateStatus(schedule, uploadDate);

        // 已在截止日前上傳 → Green
        Assert.Equal("Green", status);
    }

    [Fact]
    public void CalculateStatus_BeforeDue_WithUpload_ReturnsGreen()
    {
        // Arrange：尚在截止日前 3 天，且已上傳
        var today      = DateTime.Today;
        var dueDate    = DateOnly.FromDateTime(today.AddDays(3));
        var uploadDate = today.AddDays(-1); // 昨天已上傳

        var schedule = new ReportSchedule
        {
            Frequency   = "Monthly",
            WarningDays = 7,
            NextDueDate = dueDate
        };

        var status = _service.CalculateStatus(schedule, uploadDate);

        Assert.Equal("Green", status);
    }

    // ─── 整合：RefreshAllStatusAsync ─────────────────────────

    [Fact]
    public async Task RefreshAllStatusAsync_NoExistingLog_InsertsNewStatus()
    {
        // Arrange
        var today    = DateTime.Today;
        var schedule = new ReportSchedule
        {
            Id          = 99,
            Frequency   = "Monthly",
            WarningDays = 7,
            NextDueDate = DateOnly.FromDateTime(today.AddDays(-1))
        };

        _scheduleRepoMock.Setup(r => r.GetAllAsync())
                         .ReturnsAsync(new[] { schedule });
        _rawDataRepoMock.Setup(r => r.GetAllAsync())
                        .ReturnsAsync(Enumerable.Empty<RawDataUpload>());
        _statusRepoMock.Setup(r => r.GetLatestByScheduleIdAsync(99))
                       .ReturnsAsync((ReportStatusLog?)null);
        _statusRepoMock.Setup(r => r.InsertAsync(It.IsAny<ReportStatusLog>()))
                       .ReturnsAsync(1);

        // Act
        await _service.RefreshAllStatusAsync();

        // Assert：因逾期且無上傳，應以 "Red" 插入新記錄
        _statusRepoMock.Verify(r => r.InsertAsync(
            It.Is<ReportStatusLog>(log =>
                log.ScheduleId == 99 &&
                log.Status == "Red")),
            Times.Once);
    }

    [Fact]
    public async Task RefreshAllStatusAsync_ExistingLog_UpdatesStatus()
    {
        // Arrange
        var today    = DateTime.Today;
        var schedule = new ReportSchedule
        {
            Id          = 100,
            Frequency   = "Yearly",
            WarningDays = 90,
            NextDueDate = DateOnly.FromDateTime(today.AddDays(5)) // 還有 5 天，在警示期內
        };

        _scheduleRepoMock.Setup(r => r.GetAllAsync())
                         .ReturnsAsync(new[] { schedule });
        _rawDataRepoMock.Setup(r => r.GetAllAsync())
                        .ReturnsAsync(Enumerable.Empty<RawDataUpload>());
        _statusRepoMock.Setup(r => r.GetLatestByScheduleIdAsync(100))
                       .ReturnsAsync(new ReportStatusLog { Id = 1, ScheduleId = 100, Status = "Green" });
        _statusRepoMock.Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>()))
                       .Returns(Task.CompletedTask);

        // Act
        await _service.RefreshAllStatusAsync();

        // Assert：剩 5 天 ≤ 90，無上傳 → 應更新為 Yellow
        _statusRepoMock.Verify(r => r.UpdateStatusAsync(
            100,
            "Yellow",
            It.IsAny<DateTime?>()),
            Times.Once);
    }
}
