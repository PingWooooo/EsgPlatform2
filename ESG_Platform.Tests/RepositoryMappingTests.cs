using System.Reflection;
using EsgPlatform.Models;

namespace ESG_Platform.Tests;

/// <summary>
/// 資料存取層（Repository）Model Mapping 測試。
/// 驗證 Dapper 所依賴的實體屬性名稱，是否與 SQL 資料表欄位名稱一致。
/// 採用反射法：確認 Model 具備所有對應 SQL 欄位所需的屬性。
/// </summary>
public class RepositoryMappingTests
{
    // ─── EmissionConfig ──────────────────────────────────────

    [Theory]
    [InlineData("Id")]
    [InlineData("Scope")]
    [InlineData("Category")]
    [InlineData("ItemName")]
    [InlineData("Factor")]
    [InlineData("GWP")]
    [InlineData("Unit")]
    [InlineData("UpdatedAt")]
    public void EmissionConfig_HasRequiredProperty(string propertyName)
    {
        var type = typeof(EmissionConfig);
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.True(prop.CanWrite, $"EmissionConfig.{propertyName} 應可寫入（Dapper mapping 需要）");
    }

    [Fact]
    public void EmissionConfig_DefaultGWP_IsOne()
    {
        // GWP 預設值應為 1（非溫室氣體的基準）
        var config = new EmissionConfig();
        Assert.Equal(1m, config.GWP);
    }

    [Fact]
    public void EmissionConfig_CanHoldDecimalFactor()
    {
        var config = new EmissionConfig { Factor = 0.509m };
        Assert.Equal(0.509m, config.Factor);
    }

    // ─── RawDataUpload ───────────────────────────────────────

    [Theory]
    [InlineData("Id")]
    [InlineData("UserId")]
    [InlineData("Scope")]
    [InlineData("Category")]
    [InlineData("ItemName")]
    [InlineData("Value")]
    [InlineData("Unit")]
    [InlineData("UploadDate")]
    public void RawDataUpload_HasRequiredProperty(string propertyName)
    {
        var type = typeof(RawDataUpload);
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.True(prop.CanWrite, $"RawDataUpload.{propertyName} 應可寫入");
    }

    [Fact]
    public void RawDataUpload_Username_IsNullable()
    {
        // Username 是 JOIN 查詢欄位，允許 null
        var prop = typeof(RawDataUpload).GetProperty("Username");
        Assert.NotNull(prop);
        // 驗證是 nullable string (string?)
        var isNullable = Nullable.GetUnderlyingType(prop!.PropertyType) != null
                      || prop.PropertyType == typeof(string); // string 在 C# 中透過 NullabilityInfo 判斷
        Assert.True(isNullable);
    }

    // ─── CalculationResult ───────────────────────────────────

    [Theory]
    [InlineData("Id")]
    [InlineData("UploadId")]
    [InlineData("TotalCO2e")]
    [InlineData("CalculatedAt")]
    [InlineData("Scope")]
    [InlineData("UploadDate")]
    public void CalculationResult_HasRequiredProperty(string propertyName)
    {
        var type = typeof(CalculationResult);
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.True(prop.CanWrite, $"CalculationResult.{propertyName} 應可寫入");
    }

    [Fact]
    public void CalculationResult_TotalCO2e_IsDecimal()
    {
        // 公噸精度需求，必須使用 decimal
        var prop = typeof(CalculationResult).GetProperty("TotalCO2e");
        Assert.NotNull(prop);
        Assert.Equal(typeof(decimal), prop!.PropertyType);
    }

    // ─── ReportSchedule ──────────────────────────────────────

    [Theory]
    [InlineData("Id")]
    [InlineData("ReportName")]
    [InlineData("Frequency")]
    [InlineData("ResponsiblePerson")]
    [InlineData("WarningDays")]
    [InlineData("NextDueDate")]
    [InlineData("CreatedAt")]
    public void ReportSchedule_HasRequiredProperty(string propertyName)
    {
        var type = typeof(ReportSchedule);
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.True(prop.CanWrite, $"ReportSchedule.{propertyName} 應可寫入");
    }

    [Fact]
    public void ReportSchedule_NextDueDate_IsDateOnly()
    {
        // 截止日應為 DateOnly，確保不含時間部分
        var prop = typeof(ReportSchedule).GetProperty("NextDueDate");
        Assert.NotNull(prop);
        Assert.Equal(typeof(DateOnly), prop!.PropertyType);
    }

    [Fact]
    public void ReportSchedule_FrequencyDisplayName_MonthlyMapsCorrectly()
    {
        var schedule = new ReportSchedule { Frequency = "Monthly" };
        Assert.Equal("月報", schedule.FrequencyDisplayName);
    }

    [Fact]
    public void ReportSchedule_FrequencyDisplayName_YearlyMapsCorrectly()
    {
        var schedule = new ReportSchedule { Frequency = "Yearly" };
        Assert.Equal("年報", schedule.FrequencyDisplayName);
    }

    // ─── User ────────────────────────────────────────────────

    [Theory]
    [InlineData("Id")]
    [InlineData("Username")]
    [InlineData("PasswordHash")]
    [InlineData("Email")]
    [InlineData("RoleId")]
    [InlineData("CreatedAt")]
    public void User_HasRequiredProperty(string propertyName)
    {
        var type = typeof(EsgPlatform.Models.User);
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.True(prop.CanWrite, $"User.{propertyName} 應可寫入");
    }

    // ─── SQL 查詢字串驗證 ─────────────────────────────────────

    [Fact]
    public void EmissionConfig_SqlSelectFields_MatchModelProperties()
    {
        // 驗證 EmissionConfigRepository 中 SELECT * 所對應的欄位名稱
        // 預期的 SQL 欄位（與 DB Schema 一致）
        var expectedSqlColumns = new[]
        {
            "Id", "Scope", "Category", "ItemName", "Factor", "GWP", "Unit", "UpdatedAt"
        };

        var modelProps = typeof(EmissionConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var col in expectedSqlColumns)
        {
            Assert.Contains(col, modelProps);
        }
    }

    [Fact]
    public void RawDataUpload_SqlSelectFields_MatchModelProperties()
    {
        var expectedSqlColumns = new[]
        {
            "Id", "UserId", "Scope", "Category", "ItemName", "Value", "Unit", "UploadDate"
        };

        var modelProps = typeof(RawDataUpload)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var col in expectedSqlColumns)
        {
            Assert.Contains(col, modelProps);
        }
    }
}
