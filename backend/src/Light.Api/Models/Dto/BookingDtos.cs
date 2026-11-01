using FluentValidation;

namespace Light.Api.Models.Dto;

// ---------- 前台 ----------

public sealed record CounselorListItemResponse(
    long Id, string Name, string? Title, string? Specialties, string? AvatarUrl);

public sealed record CounselorDetailResponse(
    long Id, string Name, string? Title, string? Specialties, string? Intro,
    string? AvatarUrl, string ReviewStatus);

public sealed record SlotItemResponse(long Id, DateTime StartAt, DateTime EndAt);

public sealed record BookingItemResponse(
    long Id, long CounselorId, string CounselorName, DateTime StartAt, DateTime EndAt,
    string Status, string PaymentStatus, DateTime BookedAt, DateTime? CancelledAt);

public sealed record BookingCreateResponse(
    long BookingId, long CounselorId, string CounselorName, DateTime StartAt, DateTime EndAt);

// ---------- 请求 ----------

public sealed record BookingCreateRequest(long SlotId, string? Remark);

public sealed record BookingCancelRequest(string? Reason);

public sealed record SlotBatchCreateRequest(DateTime StartAt, DateTime EndAt, int Count, int IntervalMinutes);

public sealed record CounselorCreateRequest(
    string Name, string? Gender, string? Title, string? LicenseNo,
    string? Intro, string? Specialties);

// ---------- 验证 ----------

public class BookingCreateRequestValidator : AbstractValidator<BookingCreateRequest>
{
    public BookingCreateRequestValidator()
    {
        RuleFor(x => x.SlotId).GreaterThan(0).WithMessage("时段不合法");
        RuleFor(x => x.Remark).MaximumLength(512).WithMessage("备注过长");
    }
}

public class SlotBatchCreateRequestValidator : AbstractValidator<SlotBatchCreateRequest>
{
    public SlotBatchCreateRequestValidator()
    {
        RuleFor(x => x.StartAt).LessThan(x => x.EndAt).WithMessage("开始时间必须早于结束时间");
        RuleFor(x => x.Count).InclusiveBetween(1, 20).WithMessage("单次最多创建 20 个时段");
        RuleFor(x => x.IntervalMinutes).InclusiveBetween(30, 240).WithMessage("时段间隔须在 30~240 分钟");
        RuleFor(x => x.StartAt).Must(s => s > DateTime.UtcNow)
            .WithMessage("只能创建未来的时段");
    }
}

public class CounselorCreateRequestValidator : AbstractValidator<CounselorCreateRequest>
{
    public CounselorCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64).WithMessage("姓名不能为空且不超过 64 字");
        RuleFor(x => x.Gender).Must(g => g is null || new[] { "unknown", "female", "male" }.Contains(g))
            .WithMessage("性别取值不合法");
        RuleFor(x => x.Title).MaximumLength(64).WithMessage("头衔过长");
        RuleFor(x => x.LicenseNo).MaximumLength(128).WithMessage("资质编号过长");
        RuleFor(x => x.Specialties).MaximumLength(512).WithMessage("擅长方向过长");
    }
}
