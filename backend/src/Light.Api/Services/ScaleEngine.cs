using Light.Api.Models.Scale;

namespace Light.Api.Services;

/// <summary>单题作答输入。</summary>
public sealed record ScaleAnswerInput(int No, int Value);

/// <summary>计分结果：总分、分级、是否触发预警（预警规则明细仅入工单快照，不对外展示）。</summary>
public sealed record ScaleScoreResult(
    int TotalScore,
    string LevelCode,
    string LevelLabel,
    bool IsCrisis,
    IReadOnlyList<string> TriggeredCrisisRules);

/// <summary>答案不合法（对应业务错误码 3002）。</summary>
public sealed class ScaleAnswerInvalidException : Exception
{
    public ScaleAnswerInvalidException(string message) : base(message) { }
}

/// <summary>
/// 量表计分 / 分级 / 预警引擎：纯函数、可单测，规则全部来自配置（硬性规则 5）。
/// 反向题不在此处理——配置的选项 value 已经反转（见 0002_scale_seed.sql SDS）。
/// </summary>
public static class ScaleEngine
{
    public static ScaleScoreResult Score(ScaleConfigDocument config, IReadOnlyList<ScaleAnswerInput> answers)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(answers);

        Validate(config, answers);

        var rawSum = answers.Sum(a => (decimal)a.Value);
        var total = decimal.Round(
            rawSum * config.Scoring.Multiplier,
            config.Scoring.Decimals,
            MidpointRounding.AwayFromZero);

        // 总分入库列 total_score 为整数（CONVENTIONS/ER：INT）；如未来配置出现小数位，应同步改列
        if (total != decimal.Truncate(total))
        {
            throw new InvalidOperationException($"量表 {config.Code} 计分出现小数总分 {total}，请检查 decimals 配置");
        }
        var intTotal = (int)total;

        var level = config.Levels.FirstOrDefault(l => intTotal >= l.Min && intTotal <= l.Max)
            ?? throw new InvalidOperationException($"量表 {config.Code} 的分级区间未覆盖总分 {intTotal}（配置不完整）");

        var triggered = EvaluateCrisis(config, answers);

        return new ScaleScoreResult(intTotal, level.Code, level.Label, triggered.Count > 0, triggered);
    }

    private static void Validate(ScaleConfigDocument config, IReadOnlyList<ScaleAnswerInput> answers)
    {
        if (config.Questions.Count == 0)
        {
            throw new InvalidOperationException($"量表 {config.Code} 配置没有任何题目");
        }

        var questionNos = config.Questions.Select(q => q.No).ToList();
        if (questionNos.Distinct().Count() != questionNos.Count)
        {
            throw new InvalidOperationException($"量表 {config.Code} 题号重复（配置不完整）");
        }

        var questionNoSet = questionNos.ToHashSet();
        var answerGroups = answers.GroupBy(a => a.No).ToList();

        // 多余作答 / 重复作答
        var unknown = answerGroups.Where(g => !questionNoSet.Contains(g.Key)).Select(g => g.Key).ToList();
        if (unknown.Count > 0)
        {
            throw new ScaleAnswerInvalidException($"答案包含未知题号：{string.Join(",", unknown.OrderBy(x => x))}");
        }
        var duplicated = answerGroups.Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicated.Count > 0)
        {
            throw new ScaleAnswerInvalidException($"题号被重复作答：{string.Join(",", duplicated.OrderBy(x => x))}");
        }

        // 缺答
        var answered = answerGroups.Select(g => g.Key).ToHashSet();
        var missing = questionNoSet.Except(answered).OrderBy(x => x).ToList();
        if (missing.Count > 0)
        {
            throw new ScaleAnswerInvalidException($"缺少题目答案：{string.Join(",", missing)}");
        }

        // 选项值合法性
        var optionMap = config.Questions.ToDictionary(q => q.No, q => q.Options.Select(o => o.Value).ToHashSet());
        foreach (var answer in answers)
        {
            if (!optionMap[answer.No].Contains(answer.Value))
            {
                throw new ScaleAnswerInvalidException($"第 {answer.No} 题的选项值 {answer.Value} 不在可选项中");
            }
        }
    }

    private static List<string> EvaluateCrisis(ScaleConfigDocument config, IReadOnlyList<ScaleAnswerInput> answers)
    {
        var triggered = new List<string>();
        if (config.CrisisRules.Count == 0)
        {
            return triggered;
        }

        var valueMap = answers.ToDictionary(a => a.No, a => a.Value);
        foreach (var rule in config.CrisisRules)
        {
            if (rule.Type == "questionAtLeast"
                && valueMap.TryGetValue(rule.QuestionNo, out var value)
                && value >= rule.MinValue)
            {
                triggered.Add(rule.Description ?? $"第 {rule.QuestionNo} 题得分 ≥ {rule.MinValue}");
            }
        }
        return triggered;
    }
}
