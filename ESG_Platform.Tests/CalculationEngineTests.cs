using EsgPlatform.Models;
using EsgPlatform.Repositories.Interfaces;
using EsgPlatform.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ESG_Platform.Tests;

/// <summary>
/// 計算引擎測試：驗證範疇一、二的 CO2e 計算公式。
/// 公式：CO2e（公噸）= 活動量 × 排放係數 × GWP / 1000
/// </summary>
public class CalculationEngineTests
{
    private readonly Mock<IEmissionConfigRepository> _configRepoMock;
    private readonly Mock<ICalculationResultRepository> _resultRepoMock;
    private readonly Mock<ILogger<CalculationEngine>> _loggerMock;
    private readonly CalculationEngine _engine;

    public CalculationEngineTests()
    {
        _configRepoMock = new Mock<IEmissionConfigRepository>();
        _resultRepoMock = new Mock<ICalculationResultRepository>();
        _loggerMock     = new Mock<ILogger<CalculationEngine>>();
        _engine         = new CalculationEngine(
            _configRepoMock.Object,
            _resultRepoMock.Object,
            _loggerMock.Object);
    }

    // ─── 正常值測試 ───────────────────────────────────────────

    [Fact]
    public async Task CalculateAndSave_Scope1_ReturnsCorrectCO2e()
    {
        // Arrange
        var upload = new RawDataUpload
        {
            Id       = 1,
            Scope    = 1,
            Category = "固定燃燒源",
            ItemName = "天然氣",
            Value    = 1000m   // 1000 單位活動量
        };
        var config = new EmissionConfig
        {
            Scope    = 1,
            Category = "固定燃燒源",
            ItemName = "天然氣",
            Factor   = 2.2m,   // 2.2 kg CO2e / 單位
            GWP      = 1m
        };

        _configRepoMock.Setup(r => r.GetByScopeAndItemAsync(1, "固定燃燒源", "天然氣"))
                       .ReturnsAsync(config);
        _resultRepoMock.Setup(r => r.InsertAsync(It.IsAny<CalculationResult>()))
                       .ReturnsAsync(1);

        // Act
        var result = await _engine.CalculateAndSaveAsync(upload);

        // Assert: 1000 × 2.2 × 1 / 1000 = 2.2 公噸
        Assert.Equal(2.2m, result);
        _resultRepoMock.Verify(r => r.InsertAsync(It.Is<CalculationResult>(cr =>
            cr.TotalCO2e == 2.2m && cr.UploadId == 1)), Times.Once);
    }

    [Fact]
    public async Task CalculateAndSave_Scope2_ReturnsCorrectCO2e()
    {
        // Arrange
        var upload = new RawDataUpload
        {
            Id       = 2,
            Scope    = 2,
            Category = "外購電力",
            ItemName = "電力",
            Value    = 5000m   // 5000 kWh
        };
        var config = new EmissionConfig
        {
            Scope    = 2,
            Category = "外購電力",
            ItemName = "電力",
            Factor   = 0.509m, // 台灣電力排放係數
            GWP      = 1m
        };

        _configRepoMock.Setup(r => r.GetByScopeAndItemAsync(2, "外購電力", "電力"))
                       .ReturnsAsync(config);
        _resultRepoMock.Setup(r => r.InsertAsync(It.IsAny<CalculationResult>()))
                       .ReturnsAsync(2);

        // Act
        var result = await _engine.CalculateAndSaveAsync(upload);

        // Assert: 5000 × 0.509 × 1 / 1000 = 2.545 公噸
        Assert.Equal(2.545m, result);
    }

    [Fact]
    public async Task CalculateAndSave_WithGWP_ReturnsCorrectCO2e()
    {
        // Arrange：含 GWP 的溫室氣體（如 CH4 GWP=25）
        var upload = new RawDataUpload
        {
            Id       = 3,
            Scope    = 1,
            Category = "逸散排放",
            ItemName = "甲烷",
            Value    = 100m
        };
        var config = new EmissionConfig
        {
            Scope    = 1,
            Category = "逸散排放",
            ItemName = "甲烷",
            Factor   = 1m,
            GWP      = 25m  // CH4 的 GWP
        };

        _configRepoMock.Setup(r => r.GetByScopeAndItemAsync(1, "逸散排放", "甲烷"))
                       .ReturnsAsync(config);
        _resultRepoMock.Setup(r => r.InsertAsync(It.IsAny<CalculationResult>()))
                       .ReturnsAsync(3);

        // Act
        var result = await _engine.CalculateAndSaveAsync(upload);

        // Assert: 100 × 1 × 25 / 1000 = 2.5 公噸
        Assert.Equal(2.5m, result);
    }

    // ─── 零值測試 ────────────────────────────────────────────

    [Fact]
    public async Task CalculateAndSave_ZeroValue_ReturnsZero()
    {
        // Arrange：活動量為 0（邊界值）
        var upload = new RawDataUpload
        {
            Id       = 4,
            Scope    = 1,
            Category = "固定燃燒源",
            ItemName = "柴油",
            Value    = 0m
        };
        var config = new EmissionConfig
        {
            Scope    = 1,
            Category = "固定燃燒源",
            ItemName = "柴油",
            Factor   = 2.6m,
            GWP      = 1m
        };

        _configRepoMock.Setup(r => r.GetByScopeAndItemAsync(1, "固定燃燒源", "柴油"))
                       .ReturnsAsync(config);
        _resultRepoMock.Setup(r => r.InsertAsync(It.IsAny<CalculationResult>()))
                       .ReturnsAsync(4);

        // Act
        var result = await _engine.CalculateAndSaveAsync(upload);

        // Assert: 0 × 2.6 × 1 / 1000 = 0
        Assert.Equal(0m, result);
    }

    // ─── 極大值測試 ───────────────────────────────────────────

    [Fact]
    public async Task CalculateAndSave_ExtremelyLargeValue_ReturnsCorrectCO2e()
    {
        // Arrange：極大值不應發生溢位
        var upload = new RawDataUpload
        {
            Id       = 5,
            Scope    = 1,
            Category = "固定燃燒源",
            ItemName = "煤炭",
            Value    = 10_000_000m  // 一千萬單位
        };
        var config = new EmissionConfig
        {
            Scope    = 1,
            Category = "固定燃燒源",
            ItemName = "煤炭",
            Factor   = 2.0m,
            GWP      = 1m
        };

        _configRepoMock.Setup(r => r.GetByScopeAndItemAsync(1, "固定燃燒源", "煤炭"))
                       .ReturnsAsync(config);
        _resultRepoMock.Setup(r => r.InsertAsync(It.IsAny<CalculationResult>()))
                       .ReturnsAsync(5);

        // Act
        var result = await _engine.CalculateAndSaveAsync(upload);

        // Assert: 10,000,000 × 2.0 × 1 / 1000 = 20,000 公噸
        Assert.Equal(20_000m, result);
    }

    // ─── 負數測試（應報錯）────────────────────────────────────

    [Fact]
    public async Task CalculateAndSave_NegativeValue_ThrowsArgumentException()
    {
        // Arrange：負值活動量不合理，應拋出例外
        var upload = new RawDataUpload
        {
            Id       = 6,
            Scope    = 1,
            Category = "固定燃燒源",
            ItemName = "天然氣",
            Value    = -500m  // 不合法的負值
        };

        // 不需 mock config，應在呼叫前就拋出錯誤
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _engine.CalculateAndSaveAsync(upload));
    }

    // ─── 找不到排放係數 ──────────────────────────────────────

    [Fact]
    public async Task CalculateAndSave_MissingConfig_ReturnsZeroAndDoesNotInsert()
    {
        // Arrange：找不到對應排放係數
        var upload = new RawDataUpload
        {
            Id       = 7,
            Scope    = 1,
            Category = "不存在類別",
            ItemName = "未知項目",
            Value    = 100m
        };

        _configRepoMock.Setup(r => r.GetByScopeAndItemAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                       .ReturnsAsync((EmissionConfig?)null);

        // Act
        var result = await _engine.CalculateAndSaveAsync(upload);

        // Assert：應回傳 0 且不寫入計算結果
        Assert.Equal(0m, result);
        _resultRepoMock.Verify(r => r.InsertAsync(It.IsAny<CalculationResult>()), Times.Never);
    }

    // ─── 批次計算測試 ────────────────────────────────────────

    [Fact]
    public async Task BatchCalculate_MultipleUploads_ReturnsAllResults()
    {
        // Arrange
        var uploads = new List<RawDataUpload>
        {
            new() { Id = 10, Scope = 1, Category = "燃燒", ItemName = "汽油", Value = 500m },
            new() { Id = 11, Scope = 2, Category = "電力", ItemName = "電力", Value = 2000m }
        };
        var config1 = new EmissionConfig { Factor = 2.0m, GWP = 1m };
        var config2 = new EmissionConfig { Factor = 0.5m, GWP = 1m };

        _configRepoMock.Setup(r => r.GetByScopeAndItemAsync(1, "燃燒", "汽油")).ReturnsAsync(config1);
        _configRepoMock.Setup(r => r.GetByScopeAndItemAsync(2, "電力", "電力")).ReturnsAsync(config2);
        _resultRepoMock.Setup(r => r.InsertAsync(It.IsAny<CalculationResult>())).ReturnsAsync(1);

        // Act
        var results = await _engine.BatchCalculateAsync(uploads);

        // Assert：應回傳兩筆結果
        Assert.Equal(2, results.Count());
        _resultRepoMock.Verify(r => r.InsertAsync(It.IsAny<CalculationResult>()), Times.Exactly(2));
    }
}
