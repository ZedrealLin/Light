using FluentValidation;

namespace Light.Api.Models.Dto;

// ---------- 响应 ----------

public sealed record DiaryListItemResponse(
    long Id,
    DateOnly EntryDate,
    int MoodScore,
    IReadOnlyList<string> Tags,
    bool HasCrisis,
    DateTime CreatedAt);

public sealed record DiaryDetailResponse(
    long Id,
    DateOnly EntryDate,
    int MoodScore,
    IReadOnlyList<string> Tags,
    string Content,
    bool HasCrisis,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record DiaryTrendItem(DateOnly Date, int MoodScore);

public sealed record DiaryReminderResponse(bool Enabled, string RemindTime, string DaysOfWeek);

// ---------- 请求 ----------

public sealed record DiaryUpsertRequest(
    DateOnly EntryDate,
    int MoodScore,
    string Content,
    IReadOnlyList<string> Tags);

public sealed record DiaryReminderRequest(bool Enabled, string RemindTime, string DaysOfWeek);

public sealed class DiaryUpsertRequestValidator : AbstractValidator<DiaryUpsertRequest>
{
    public DiaryUpsertRequestValidator()
    {
        RuleFor(x => x.EntryDate)
            .NotEmpty().WithMessage("日记日期不能为空")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)))
            .WithMessage("日记日期不能晚于明天");
        RuleFor(x => x.MoodScore)
            .InclusiveBetween(1, 5).WithMessage("心情分须在 1~5 之间（5 为最好）");
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("日记内容不能为空")
            .MaximumLength(10000).WithMessage("日记内容最长 10000 字");
        RuleFor(x => x.Tags)
            .Must(t => t.Count <= 10).WithMessage("标签最多 10 个");
        RuleForEach(x => x.Tags)
            .MaximumLength(16).WithMessage("单个标签最长 16 字");
    }
}

public sealed class DiaryReminderRequestValidator : AbstractValidator<DiaryReminderRequest>
{
    private static readonly string[] Week = ["1", "2", "3", "4", "5", "6", "7"];

    public DiaryReminderRequestValidator()
    {
        RuleFor(x => x.RemindTime)
            .Matches(@"^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d)?$").WithMessage("提醒时间格式应为 HH:mm");
        RuleFor(x => x.DaysOfWeek)
            .NotEmpty().WithMessage("提醒日不能为空")
            .Must(days => days.Split(',').All(d => Week.Contains(d.Trim())))
            .WithMessage("提醒日只能是 1~7（逗号分隔，1=周一）");
    }
}
