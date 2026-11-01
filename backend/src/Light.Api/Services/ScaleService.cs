using System.Text.Json;
using Light.Api.Common;
using Light.Api.Infrastructure.Crypto;
using Light.Api.Models.Dto;
using Light.Api.Models.Scale;
using Light.Api.Repositories;
using Light.Api.Services;

namespace Light.Api.Services;

/// <summary>
/// 量表业务：列表 / 详情 / 提交作答（加密落库）/ 结果取回。
/// 危机预警工单在 1.4 接入 CrisisAlertService；当前先把 is_crisis 落库并在响应中带 crisis 块。
/// </summary>
public sealed class ScaleService
{
    private readonly ScaleRepository _scales;
    private readonly ScaleSubmissionRepository _submissions;
    private readonly IAesCipher _cipher;
    private readonly ILogger<ScaleService> _logger;

    public const string CrisisHelpUrl = "/crisis-help";

    public ScaleService(
        ScaleRepository scales,
        ScaleSubmissionRepository submissions,
        IAesCipher cipher,
        ILogger<ScaleService> logger)
    {
        _scales = scales;
        _submissions = submissions;
        _cipher = cipher;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ScaleListItemResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var summaries = await _scales.GetActiveSummariesAsync(cancellationToken);
        return summaries.Select(s => new ScaleListItemResponse(
            s.Code, s.Name, s.Description, s.QuestionCount, s.EstimatedMinutes,
            s.SourceName, s.SourceVerified)).ToList();
    }

    public async Task<ScaleDetailResponse?> GetDetailAsync(string code, CancellationToken cancellationToken)
    {
        var config = await _scales.GetActiveByCodeAsync(code, cancellationToken);
        if (config is null)
        {
            return null;
        }
        return ToDetail(config);
    }

    /// <summary>提交作答：引擎计分 → 答案加密 → 落库。命中预警时 Crisis 非空（工单在 1.4 建）。</summary>
    public async Task<ScaleSubmissionResponse> SubmitAsync(
        long? userId, ScaleSubmitRequest request, CancellationToken cancellationToken)
    {
        var row = await _scales.GetActiveRowAsync(request.ScaleCode, cancellationToken)
            ?? throw new ScaleNotFoundException(request.ScaleCode);

        var answers = request.Answers.Select(a => new ScaleAnswerInput(a.No, a.Value)).ToList();
        var result = ScaleEngine.Score(row.Config, answers);

        // 答案明文只在这里短暂存在，立即加密；不写日志
        var answersJson = JsonSerializer.Serialize(request.Answers);
        var cipher = _cipher.Encrypt(answersJson);

        var inserted = await _submissions.InsertAsync(
            userId, row.Id, row.Config.Code, row.Version,
            cipher, result.TotalScore, result.LevelCode, result.LevelLabel, result.IsCrisis,
            cancellationToken);

        _logger.LogInformation(
            "量表作答已提交 code={Code} submissionPublicId={PublicId} totalScore={TotalScore} level={Level} isCrisis={IsCrisis}",
            row.Config.Code, inserted.PublicId, result.TotalScore, result.LevelCode, result.IsCrisis);

        return new ScaleSubmissionResponse(
            inserted.PublicId,
            result.TotalScore,
            result.LevelCode,
            result.LevelLabel,
            row.Config.Disclaimer ?? "本量表结果仅为情绪自助提示，不构成任何医学诊断。",
            result.IsCrisis ? new SubmissionCrisisResponse(CrisisHelpUrl) : null);
    }

    public async Task<ScaleSubmissionResponse?> GetResultAsync(string publicId, CancellationToken cancellationToken)
    {
        var row = await _submissions.GetByPublicIdAsync(publicId, cancellationToken);
        if (row is null)
        {
            return null;
        }
        return new ScaleSubmissionResponse(
            row.PublicId,
            row.TotalScore,
            row.LevelCode,
            row.LevelLabel,
            "本量表结果仅为情绪自助提示，不构成任何医学诊断。",
            row.IsCrisis ? new SubmissionCrisisResponse(CrisisHelpUrl) : null);
    }

    private static ScaleDetailResponse ToDetail(ScaleConfigDocument config) => new(
        config.Code,
        config.Name,
        config.Description,
        config.Instructions,
        config.Source?.Name,
        config.Source?.Verified ?? false,
        config.Disclaimer,
        config.Questions
            .OrderBy(q => q.No)
            .Select(q => new ScaleQuestionResponse(
                q.No,
                q.Title,
                q.Options.Select(o => new ScaleOptionResponse(o.Value, o.Label)).ToList()))
            .ToList());
}

/// <summary>量表不存在或未启用（对外 3001）。</summary>
public sealed class ScaleNotFoundException : Exception
{
    public ScaleNotFoundException(string code)
        : base($"量表不存在或未启用：{code}")
    {
    }
}
