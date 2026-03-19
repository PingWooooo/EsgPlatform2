using EsgPlatform.Models;
using EsgPlatform.Repositories.Interfaces;
using EsgPlatform.Services;
using EsgPlatform.Services.Interfaces;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using Moq;

namespace ESG_Platform.Tests;

/// <summary>
/// Excel 上傳服務測試：
/// - 正常 Excel 的解析與計算觸發
/// - 損毀或格式錯誤檔案的防錯機制
/// - 欄位驗證（負值、範疇錯誤、必填欄位遺漏）
/// </summary>
public class ExcelServiceTests
{
    private readonly Mock<IRawDataRepository> _rawDataRepoMock;
    private readonly Mock<ICalculationEngine> _calcEngineMock;
    private readonly Mock<ILogger<UploadService>> _loggerMock;
    private readonly UploadService _service;

    public ExcelServiceTests()
    {
        _rawDataRepoMock = new Mock<IRawDataRepository>();
        _calcEngineMock  = new Mock<ICalculationEngine>();
        _loggerMock      = new Mock<ILogger<UploadService>>();
        _service         = new UploadService(
            _rawDataRepoMock.Object,
            _calcEngineMock.Object,
            _loggerMock.Object);
    }

    // ─── 正常 Excel 解析 ─────────────────────────────────────

    [Fact]
    public async Task ProcessExcelUpload_ValidFile_ReturnsCorrectSuccessCount()
    {
        // Arrange：建立含兩列合法資料的 Excel
        var stream = CreateValidExcelStream(new[]
        {
            new ExcelRow { Scope = 1, Category = "固定燃燒源", ItemName = "天然氣", Value = 1000m, Unit = "m³" },
            new ExcelRow { Scope = 2, Category = "外購電力",   ItemName = "電力",   Value = 5000m, Unit = "kWh" }
        });

        _rawDataRepoMock.Setup(r => r.InsertAsync(It.IsAny<RawDataUpload>())).ReturnsAsync(1);
        _calcEngineMock.Setup(e => e.CalculateAndSaveAsync(It.IsAny<RawDataUpload>()))
                       .ReturnsAsync(1.0m);

        // Act
        var (success, error, errors) = await _service.ProcessExcelUploadAsync(stream, userId: 1);

        // Assert
        Assert.Equal(2, success);
        Assert.Equal(0, error);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ProcessExcelUpload_ValidFile_TriggersCalculationEngine()
    {
        // Arrange
        var stream = CreateValidExcelStream(new[]
        {
            new ExcelRow { Scope = 1, Category = "燃燒", ItemName = "煤炭", Value = 200m, Unit = "kg" }
        });

        _rawDataRepoMock.Setup(r => r.InsertAsync(It.IsAny<RawDataUpload>())).ReturnsAsync(42);
        _calcEngineMock.Setup(e => e.CalculateAndSaveAsync(It.IsAny<RawDataUpload>()))
                       .ReturnsAsync(0.5m);

        // Act
        await _service.ProcessExcelUploadAsync(stream, userId: 1);

        // Assert：計算引擎必須被呼叫一次
        _calcEngineMock.Verify(e => e.CalculateAndSaveAsync(
            It.Is<RawDataUpload>(u => u.Scope == 1 && u.ItemName == "煤炭")),
            Times.Once);
    }

    // ─── 損毀 / 格式錯誤的檔案 ──────────────────────────────

    [Fact]
    public async Task ProcessExcelUpload_CorruptedFile_ReturnsErrorAndDoesNotThrow()
    {
        // Arrange：損毀的 Excel（填入隨機位元組，非有效 xlsx）
        var corruptData = new byte[] { 0x00, 0xFF, 0xAB, 0xCD, 0x12, 0x34, 0x56, 0x78 };
        using var stream = new MemoryStream(corruptData);

        // Act：不應丟出未處理例外
        var (success, error, errors) = await _service.ProcessExcelUploadAsync(stream, userId: 1);

        // Assert：服務應捕獲錯誤並回報
        Assert.Equal(0, success);
        Assert.True(error >= 1, "損毀檔案應至少有一筆錯誤計數");
        Assert.NotEmpty(errors);
        Assert.Contains("Excel 解析失敗", errors[0]);
    }

    [Fact]
    public async Task ProcessExcelUpload_EmptyStream_ReturnsErrorOrZero()
    {
        // Arrange：空的 Stream（0 位元組）
        using var stream = new MemoryStream(Array.Empty<byte>());

        // Act
        var (success, error, errors) = await _service.ProcessExcelUploadAsync(stream, userId: 1);

        // Assert：空檔案應為 0 筆成功，且不丟出例外
        Assert.Equal(0, success);
        // 空的有效 xlsx 可能成功解析但無資料，或者解析失敗
        // 無論哪種，都不應崩潰
        Assert.True(success == 0);
    }

    // ─── 欄位驗證：負值活動量 ─────────────────────────────────

    [Fact]
    public async Task ProcessExcelUpload_NegativeValue_SkipsRowAndReturnsError()
    {
        // Arrange：Value 為負數
        var stream = CreateValidExcelStream(new[]
        {
            new ExcelRow { Scope = 1, Category = "燃燒", ItemName = "汽油", Value = -100m, Unit = "L" }
        });

        // Act
        var (success, error, errors) = await _service.ProcessExcelUploadAsync(stream, userId: 1);

        // Assert：負值應被過濾，不觸發計算
        Assert.Equal(0, success);
        Assert.Equal(1, error);
        _calcEngineMock.Verify(e => e.CalculateAndSaveAsync(It.IsAny<RawDataUpload>()), Times.Never);
    }

    [Fact]
    public async Task ProcessExcelUpload_ZeroValue_SkipsRowAndReturnsError()
    {
        // Arrange：Value 為 0（不合法）
        var stream = CreateValidExcelStream(new[]
        {
            new ExcelRow { Scope = 1, Category = "燃燒", ItemName = "汽油", Value = 0m, Unit = "L" }
        });

        // Act
        var (success, error, errors) = await _service.ProcessExcelUploadAsync(stream, userId: 1);

        // Assert：0 值應被過濾
        Assert.Equal(0, success);
        Assert.Equal(1, error);
    }

    // ─── 欄位驗證：無效範疇（非 1 或 2）────────────────────────

    [Fact]
    public async Task ProcessExcelUpload_InvalidScope_SkipsRow()
    {
        // Arrange：Scope = 3（無效）
        var stream = CreateValidExcelStream(new[]
        {
            new ExcelRow { Scope = 3, Category = "其他", ItemName = "未知", Value = 100m, Unit = "kg" }
        });

        // Act
        var (success, error, errors) = await _service.ProcessExcelUploadAsync(stream, userId: 1);

        // Assert：無效 Scope 應被拒絕
        Assert.Equal(0, success);
        Assert.Equal(1, error);
        Assert.Contains("範疇值 3 無效", errors[0]);
    }

    // ─── 欄位驗證：必填欄位為空 ──────────────────────────────

    [Fact]
    public async Task ProcessExcelUpload_EmptyCategory_SkipsRow()
    {
        // Arrange：Category 為空字串
        var stream = CreateValidExcelStream(new[]
        {
            new ExcelRow { Scope = 1, Category = "", ItemName = "天然氣", Value = 100m, Unit = "m³" }
        });

        // Act
        var (success, error, errors) = await _service.ProcessExcelUploadAsync(stream, userId: 1);

        // Assert：缺少必填欄位應被跳過
        Assert.Equal(0, success);
        Assert.Equal(1, error);
    }

    [Fact]
    public async Task ProcessExcelUpload_EmptyItemName_SkipsRow()
    {
        // Arrange：ItemName 為空白
        var stream = CreateValidExcelStream(new[]
        {
            new ExcelRow { Scope = 2, Category = "電力", ItemName = "   ", Value = 500m, Unit = "kWh" }
        });

        // Act
        var (success, error, errors) = await _service.ProcessExcelUploadAsync(stream, userId: 1);

        // Assert
        Assert.Equal(0, success);
        Assert.Equal(1, error);
    }

    // ─── 混合有效/無效資料 ────────────────────────────────────

    [Fact]
    public async Task ProcessExcelUpload_MixedRows_ReturnsCorrectCounts()
    {
        // Arrange：3 列：第 1 合法、第 2 負值（錯誤）、第 3 合法
        var stream = CreateValidExcelStream(new[]
        {
            new ExcelRow { Scope = 1, Category = "燃燒", ItemName = "天然氣", Value = 500m,  Unit = "m³" },
            new ExcelRow { Scope = 1, Category = "燃燒", ItemName = "汽油",   Value = -50m,  Unit = "L"  },
            new ExcelRow { Scope = 2, Category = "電力", ItemName = "電力",   Value = 1000m, Unit = "kWh"}
        });

        _rawDataRepoMock.Setup(r => r.InsertAsync(It.IsAny<RawDataUpload>())).ReturnsAsync(1);
        _calcEngineMock.Setup(e => e.CalculateAndSaveAsync(It.IsAny<RawDataUpload>()))
                       .ReturnsAsync(1.0m);

        // Act
        var (success, error, errors) = await _service.ProcessExcelUploadAsync(stream, userId: 1);

        // Assert
        Assert.Equal(2, success);
        Assert.Equal(1, error);
    }

    // ─── Unit 欄位缺失應補預設值 ─────────────────────────────

    [Fact]
    public async Task ProcessExcelUpload_NullUnit_DefaultsToUnknown()
    {
        // Arrange：Unit 欄位為空
        RawDataUpload? capturedUpload = null;
        var stream = CreateValidExcelStream(new[]
        {
            new ExcelRow { Scope = 1, Category = "燃燒", ItemName = "天然氣", Value = 100m, Unit = null }
        });

        _rawDataRepoMock.Setup(r => r.InsertAsync(It.IsAny<RawDataUpload>()))
                        .Callback<RawDataUpload>(u => capturedUpload = u)
                        .ReturnsAsync(1);
        _calcEngineMock.Setup(e => e.CalculateAndSaveAsync(It.IsAny<RawDataUpload>()))
                       .ReturnsAsync(0.5m);

        // Act
        await _service.ProcessExcelUploadAsync(stream, userId: 1);

        // Assert：Unit 應預設為 "未知"
        Assert.NotNull(capturedUpload);
        Assert.Equal("未知", capturedUpload!.Unit);
    }

    // ─── 輔助：建立 Excel Stream ────────────────────────────

    private static Stream CreateValidExcelStream(IEnumerable<ExcelRow> rows)
    {
        var ms = new MemoryStream();
        MiniExcel.SaveAs(ms, rows);
        ms.Position = 0;
        return ms;
    }

    private class ExcelRow
    {
        public int     Scope    { get; set; }
        public string  Category { get; set; } = string.Empty;
        public string  ItemName { get; set; } = string.Empty;
        public decimal Value    { get; set; }
        public string? Unit     { get; set; }
    }
}
