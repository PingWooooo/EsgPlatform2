namespace EsgPlatform.Models;

/// <summary>ESG 文件排程</summary>
public class EsgDocumentSchedule
{
    public int Id { get; set; }

    /// <summary>文件內容名稱（例：月度碳排放盤查報告）</summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>頻率：Monthly / Yearly</summary>
    public string Frequency { get; set; } = "Monthly";

    /// <summary>負責窗口姓名</summary>
    public string ResponsiblePerson { get; set; } = string.Empty;

    /// <summary>預警天數（到期前幾天顯示黃燈）</summary>
    public int WarningDays { get; set; }

    /// <summary>下次截止日</summary>
    public DateTime NextDueDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // ── 應用程式層級計算欄位 ──

    /// <summary>最後上傳時間（從 EsgDocumentUploads 關聯）</summary>
    public DateTime? LastUploadedAt { get; set; }

    /// <summary>紅綠燈狀態：Green / Yellow / Red</summary>
    public string TrafficLight { get; set; } = "Green";

    /// <summary>頻率中文顯示</summary>
    public string FrequencyDisplay => Frequency == "Monthly" ? "月報" : "年報";

    /// <summary>燈號 CSS 類別</summary>
    public string TrafficLightCssClass => TrafficLight switch
    {
        "Red"    => "status-red",
        "Yellow" => "status-yellow",
        _        => "status-green"
    };

    /// <summary>燈號中文顯示</summary>
    public string TrafficLightDisplay => TrafficLight switch
    {
        "Red"    => "逾期",
        "Yellow" => "警告",
        _        => "正常"
    };

    /// <summary>距截止日天數（正數 = 剩餘，負數 = 已逾期）</summary>
    public int DaysUntilDue => (NextDueDate.Date - DateTime.Today).Days;
}
