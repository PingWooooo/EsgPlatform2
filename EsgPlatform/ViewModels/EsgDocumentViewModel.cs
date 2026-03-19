using EsgPlatform.Models;
using System.ComponentModel.DataAnnotations;

namespace EsgPlatform.ViewModels;

/// <summary>ESG 文件監控看板 ViewModel</summary>
public class EsgDocumentIndexViewModel
{
    public IEnumerable<EsgDocumentSchedule> Schedules { get; set; } = [];

    public int GreenCount  => Schedules.Count(x => x.TrafficLight == "Green");
    public int YellowCount => Schedules.Count(x => x.TrafficLight == "Yellow");
    public int RedCount    => Schedules.Count(x => x.TrafficLight == "Red");
}

/// <summary>排程新增/編輯表單</summary>
public class EsgDocumentScheduleFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "文件名稱為必填")]
    [StringLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    [Required(ErrorMessage = "頻率為必填")]
    public string Frequency { get; set; } = "Monthly";

    [Required(ErrorMessage = "負責窗口為必填")]
    [StringLength(100)]
    public string ResponsiblePerson { get; set; } = string.Empty;

    [Range(1, 365, ErrorMessage = "預警天數須介於 1~365 天")]
    public int WarningDays { get; set; } = 7;

    [Required(ErrorMessage = "截止日為必填")]
    public DateTime NextDueDate { get; set; } = DateTime.Today.AddMonths(1);
}

/// <summary>文件上傳表單</summary>
public class EsgDocumentUploadFormModel
{
    public int ScheduleId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public IFormFile? File { get; set; }
    public string? Remark { get; set; }
}
