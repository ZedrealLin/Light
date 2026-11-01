using System.Text.Json;
using Light.Api.Common;
using Light.Api.Infrastructure.Crypto;
using Light.Api.Models.Dto;
using Light.Api.Repositories;
using Light.Api.Services;

namespace Light.Api.Services;

/// <summary>
/// 情绪日记业务：正文 AES-GCM 加密落库（硬性规则 3）、危机关键词命中建工单（crisis-flow T2）。
/// 明文只在服务层内存中短暂存在，绝不写入日志。
/// </summary>
public sealed class DiaryService
{
    private readonly DiaryRepository _diaries;
    private readonly IAesCipher _cipher;
    private readonly CrisisDetectionService _detection;
    private readonly CrisisAlertService _crisisAlerts;
    private readonly ILogger<DiaryService> _logger;

    public DiaryService(
        DiaryRepository diaries, IAesCipher cipher,
        CrisisDetectionService detection, CrisisAlertService crisisAlerts,
        ILogger<DiaryService> logger)
    {
        _diaries = diaries;
        _cipher = cipher;
        _detection = detection;
        _crisisAlerts = crisisAlerts;
        _logger = logger;
    }

    public async Task<DiaryDetailResponse> CreateAsync(
        long userId, DiaryUpsertRequest request, CancellationToken cancellationToken)
    {
        var existing = await _diaries.FindByDateAsync(userId, request.EntryDate, cancellationToken);
        if (existing is not null && existing.DeletedAt is null)
        {
            throw new BizException(ErrorCodes.DiaryExistsToday, "当日日记已存在，请走更新");
        }

        return await UpsertInternalAsync(existing?.Id, existing?.DeletedAt is not null, userId, request, cancellationToken);
    }

    public async Task<DiaryDetailResponse> UpdateAsync(
        long userId, long id, DiaryUpsertRequest request, CancellationToken cancellationToken)
    {
        var row = await _diaries.FindAsync(id, userId, cancellationToken)
            ?? throw new BizException(ErrorCodes.DiaryNotFound, "日记不存在");
        return await UpsertInternalAsync(row.Id, row.DeletedAt is not null, userId, request, cancellationToken);
    }

    private async Task<DiaryDetailResponse> UpsertInternalAsync(
        long? existingId, bool restoreDeleted, long userId,
        DiaryUpsertRequest request, CancellationToken cancellationToken)
    {
        var content = request.Content.Trim();
        var matched = _detection.Match(content);
        var isCrisis = matched.Count > 0;
        var tagsJson = request.Tags.Count == 0
            ? null
            : JsonSerializer.Serialize(request.Tags);

        long id;
        if (existingId is null)
        {
            id = await _diaries.InsertAsync(
                userId, request.EntryDate, request.MoodScore,
                _cipher.Encrypt(content), tagsJson, isCrisis, cancellationToken);
        }
        else
        {
            id = existingId.Value;
            await _diaries.UpdateAsync(
                id, request.MoodScore, _cipher.Encrypt(content), tagsJson, isCrisis,
                restoreDeleted, cancellationToken);
        }

        // 危机干预 T2：日记命中关键词 → 建预警工单（10 分钟窗口去重）
        if (isCrisis)
        {
            await _crisisAlerts.TryCreateKeywordAlertAsync(
                "diary", userId, id, null, null, matched, cancellationToken);
        }

        _logger.LogInformation(
            "日记已保存 diaryId={DiaryId} user={UserId} mood={Mood} isCrisis={IsCrisis}",
            id, userId, request.MoodScore, isCrisis);

        var row = await _diaries.FindAsync(id, userId, cancellationToken)
            ?? throw new BizException(ErrorCodes.DiaryNotFound, "日记不存在");
        return ToDetail(row, content);
    }

    public async Task<DiaryDetailResponse?> GetAsync(long userId, long id, CancellationToken cancellationToken)
    {
        var row = await _diaries.FindAsync(id, userId, cancellationToken);
        if (row is null || row.DeletedAt is not null)
        {
            return null;
        }
        return ToDetail(row, _cipher.Decrypt(row.ContentCipher));
    }

    public async Task<IReadOnlyList<DiaryListItemResponse>> ListAsync(
        long userId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (rows, _) = await _diaries.ListAsync(userId, from, to, page, pageSize, cancellationToken);
        return rows.Select(r => new DiaryListItemResponse(
            r.Id, r.EntryDate, r.MoodScore, ParseTags(r.TagsJson), r.IsCrisis, r.CreatedAt)).ToList();
    }

    public async Task SoftDeleteAsync(long userId, long id, CancellationToken cancellationToken)
    {
        var row = await _diaries.FindAsync(id, userId, cancellationToken)
            ?? throw new BizException(ErrorCodes.DiaryNotFound, "日记不存在");
        await _diaries.SoftDeleteAsync(row.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<DiaryTrendItem>> TrendAsync(long userId, int days, CancellationToken cancellationToken)
    {
        var rows = await _diaries.TrendAsync(userId, days, cancellationToken);
        return rows.Select(r => new DiaryTrendItem(r.Date, r.MoodScore)).ToList();
    }

    public async Task<DiaryReminderResponse> GetReminderAsync(long userId, CancellationToken cancellationToken)
    {
        var reminder = await _diaries.GetReminderAsync(userId, cancellationToken);
        return reminder is null
            ? new DiaryReminderResponse(false, "20:00", "1,2,3,4,5,6,7")
            : new DiaryReminderResponse(reminder.Value.Enabled, reminder.Value.RemindTime, reminder.Value.DaysOfWeek);
    }

    public Task UpsertReminderAsync(long userId, DiaryReminderRequest request, CancellationToken cancellationToken)
    {
        return _diaries.UpsertReminderAsync(
            userId, request.Enabled, request.RemindTime, request.DaysOfWeek.Replace(" ", ""), cancellationToken);
    }

    private static DiaryDetailResponse ToDetail(DiaryRow row, string content) => new(
        row.Id, row.EntryDate, row.MoodScore, ParseTags(row.TagsJson), content,
        row.IsCrisis, row.CreatedAt, row.UpdatedAt);

    private static IReadOnlyList<string> ParseTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return Array.Empty<string>();
        }
        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? [];
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
