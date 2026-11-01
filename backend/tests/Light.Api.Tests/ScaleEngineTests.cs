using Light.Api.Models.Scale;
using Light.Api.Services;
using Xunit;

namespace Light.Api.Tests;

/// <summary>
/// 量表引擎单元测试：计分、分级、预警触发（对应 PROJECT_BRIEF 自查表
/// "计分、分级有单元测试；第 9 题 ≥1 分必定触发预警"）。
/// 用内存配置构造，不依赖数据库。
/// </summary>
public sealed class ScaleEngineTests
{
    // ---------- 配置构造 ----------

    private static ScaleConfigDocument BuildPhq9()
    {
        var config = new ScaleConfigDocument
        {
            Code = "phq9",
            Name = "患者健康问卷-9（PHQ-9）",
            Scoring = new ScaleScoring { Type = "sum", Multiplier = 1m, Decimals = 0 },
            Disclaimer = "本量表结果仅为情绪自助提示，不构成任何医学诊断。",
        };
        for (var no = 1; no <= 9; no++)
        {
            config.Questions.Add(new ScaleQuestion
            {
                No = no,
                Title = $"第{no}题",
                Options = new List<ScaleOption>
                {
                    new() { Value = 0, Label = "完全不会" },
                    new() { Value = 1, Label = "好几天" },
                    new() { Value = 2, Label = "一半以上的天数" },
                    new() { Value = 3, Label = "几乎每天" },
                },
            });
        }
        config.Levels.Add(new ScaleLevel { Code = "minimal", Label = "目前未见明显情绪困扰的提示", Min = 0, Max = 4 });
        config.Levels.Add(new ScaleLevel { Code = "mild", Label = "存在轻度情绪困扰的提示", Min = 5, Max = 9 });
        config.Levels.Add(new ScaleLevel { Code = "moderate", Label = "存在中度情绪困扰的提示", Min = 10, Max = 14 });
        config.Levels.Add(new ScaleLevel { Code = "moderately_severe", Label = "存在中重度情绪困扰的提示", Min = 15, Max = 19 });
        config.Levels.Add(new ScaleLevel { Code = "severe", Label = "存在重度情绪困扰的提示", Min = 20, Max = 27 });
        config.CrisisRules.Add(new ScaleCrisisRule
        {
            Type = "questionAtLeast",
            QuestionNo = 9,
            MinValue = 1,
            Description = "PHQ-9 第 9 题得分 ≥1",
        });
        return config;
    }

    private static ScaleConfigDocument BuildGad7()
    {
        var config = new ScaleConfigDocument
        {
            Code = "gad7",
            Name = "广泛性焦虑量表-7（GAD-7）",
            Scoring = new ScaleScoring { Type = "sum", Multiplier = 1m, Decimals = 0 },
        };
        for (var no = 1; no <= 7; no++)
        {
            config.Questions.Add(new ScaleQuestion
            {
                No = no,
                Title = $"第{no}题",
                Options = new List<ScaleOption>
                {
                    new() { Value = 0, Label = "完全不会" },
                    new() { Value = 1, Label = "好几天" },
                    new() { Value = 2, Label = "一半以上的天数" },
                    new() { Value = 3, Label = "几乎每天" },
                },
            });
        }
        config.Levels.Add(new ScaleLevel { Code = "minimal", Label = "未见明显提示", Min = 0, Max = 4 });
        config.Levels.Add(new ScaleLevel { Code = "mild", Label = "轻度提示", Min = 5, Max = 9 });
        config.Levels.Add(new ScaleLevel { Code = "moderate", Label = "中度提示", Min = 10, Max = 14 });
        config.Levels.Add(new ScaleLevel { Code = "severe", Label = "重度提示", Min = 15, Max = 21 });
        return config;
    }

    /// <summary>SDS：20 题、选项 1~4、反向题 value 已在配置中反转、标准分 = 原始分 × 1.25。</summary>
    private static ScaleConfigDocument BuildSds()
    {
        var config = new ScaleConfigDocument
        {
            Code = "sds",
            Name = "抑郁自评量表（SDS）",
            Scoring = new ScaleScoring { Type = "sum", Multiplier = 1.25m, Decimals = 0 },
        };
        for (var no = 1; no <= 20; no++)
        {
            config.Questions.Add(new ScaleQuestion
            {
                No = no,
                Title = $"第{no}题",
                Options = new List<ScaleOption>
                {
                    new() { Value = 1, Label = "没有或很少时间" },
                    new() { Value = 2, Label = "少部分时间" },
                    new() { Value = 3, Label = "相当多时间" },
                    new() { Value = 4, Label = "绝大部分或全部时间" },
                },
            });
        }
        config.Levels.Add(new ScaleLevel { Code = "normal", Label = "未见明显提示", Min = 0, Max = 49 });
        config.Levels.Add(new ScaleLevel { Code = "mild", Label = "轻度提示", Min = 50, Max = 59 });
        config.Levels.Add(new ScaleLevel { Code = "moderate", Label = "中度提示", Min = 60, Max = 69 });
        config.Levels.Add(new ScaleLevel { Code = "severe", Label = "重度提示", Min = 70, Max = 100 });
        return config;
    }

    private static List<ScaleAnswerInput> AllAnswers(int questionCount, int value) =>
        Enumerable.Range(1, questionCount).Select(no => new ScaleAnswerInput(no, value)).ToList();

    // ---------- 计分 ----------

    [Fact]
    public void Phq9_AllZero_ReturnsMinimalLevelWithoutCrisis()
    {
        var result = ScaleEngine.Score(BuildPhq9(), AllAnswers(9, 0));
        Assert.Equal(0, result.TotalScore);
        Assert.Equal("minimal", result.LevelCode);
        Assert.False(result.IsCrisis);
        Assert.Empty(result.TriggeredCrisisRules);
    }

    [Fact]
    public void Phq9_BoundaryTotal10_ReturnsModerate()
    {
        // 总分恰好 10：8 题 1 分 + 第 9 题 2 分 = 10
        var answers = AllAnswers(8, 1).Append(new ScaleAnswerInput(9, 2)).ToList();
        var result = ScaleEngine.Score(BuildPhq9(), answers);
        Assert.Equal(10, result.TotalScore);
        Assert.Equal("moderate", result.LevelCode);
    }

    [Fact]
    public void Phq9_MaxAnswers_ReturnsSevere27()
    {
        var result = ScaleEngine.Score(BuildPhq9(), AllAnswers(9, 3));
        Assert.Equal(27, result.TotalScore);
        Assert.Equal("severe", result.LevelCode);
        Assert.True(result.IsCrisis); // 第 9 题 = 3 ≥ 1，必然触发
    }

    [Fact]
    public void Gad7_AllThree_ReturnsSevere21()
    {
        var result = ScaleEngine.Score(BuildGad7(), AllAnswers(7, 3));
        Assert.Equal(21, result.TotalScore);
        Assert.Equal("severe", result.LevelCode);
        Assert.False(result.IsCrisis); // GAD-7 无预警规则
    }

    [Fact]
    public void Sds_AllMinOption_RawSum20_Index25Normal()
    {
        var result = ScaleEngine.Score(BuildSds(), AllAnswers(20, 1));
        Assert.Equal(25, result.TotalScore); // 20 × 1.25 = 25
        Assert.Equal("normal", result.LevelCode);
    }

    [Fact]
    public void Sds_AllMaxOption_RawSum80_Index100Severe()
    {
        var result = ScaleEngine.Score(BuildSds(), AllAnswers(20, 4));
        Assert.Equal(100, result.TotalScore); // 80 × 1.25 = 100
        Assert.Equal("severe", result.LevelCode);
    }

    // ---------- 预警（危机干预核心，硬性规则 2） ----------

    [Fact]
    public void Phq9_Question9AtLeast1_AlwaysTriggersCrisis()
    {
        foreach (var q9Value in new[] { 1, 2, 3 })
        {
            var answers = AllAnswers(8, 0).Append(new ScaleAnswerInput(9, q9Value)).ToList();
            var result = ScaleEngine.Score(BuildPhq9(), answers);
            Assert.True(result.IsCrisis);
            Assert.Contains("PHQ-9 第 9 题得分 ≥1", result.TriggeredCrisisRules);
        }
    }

    [Fact]
    public void Phq9_Question9Zero_DoesNotTriggerCrisis()
    {
        var answers = AllAnswers(8, 3).Append(new ScaleAnswerInput(9, 0)).ToList();
        var result = ScaleEngine.Score(BuildPhq9(), answers);
        Assert.Equal(24, result.TotalScore);
        Assert.False(result.IsCrisis);
    }

    // ---------- 校验（3002） ----------

    [Fact]
    public void MissingAnswer_Throws()
    {
        var answers = AllAnswers(8, 0).ToList(); // 缺第 9 题
        var ex = Assert.Throws<ScaleAnswerInvalidException>(() => ScaleEngine.Score(BuildPhq9(), answers));
        Assert.Contains("9", ex.Message);
    }

    [Fact]
    public void UnknownQuestionNo_Throws()
    {
        var answers = AllAnswers(9, 0).Append(new ScaleAnswerInput(10, 1)).ToList();
        Assert.Throws<ScaleAnswerInvalidException>(() => ScaleEngine.Score(BuildPhq9(), answers));
    }

    [Fact]
    public void InvalidOptionValue_Throws()
    {
        var answers = AllAnswers(8, 0).Append(new ScaleAnswerInput(9, 9)).ToList();
        var ex = Assert.Throws<ScaleAnswerInvalidException>(() => ScaleEngine.Score(BuildPhq9(), answers));
        Assert.Contains("选项值", ex.Message);
    }

    [Fact]
    public void DuplicatedAnswer_Throws()
    {
        var answers = AllAnswers(9, 0).Append(new ScaleAnswerInput(1, 2)).ToList();
        Assert.Throws<ScaleAnswerInvalidException>(() => ScaleEngine.Score(BuildPhq9(), answers));
    }

    // ---------- 配置健壮性 ----------

    [Fact]
    public void LevelRangeNotCoveringTotal_Throws()
    {
        var config = new ScaleConfigDocument
        {
            Code = "broken",
            Scoring = new ScaleScoring { Type = "sum", Multiplier = 1m, Decimals = 0 },
        };
        config.Questions.Add(new ScaleQuestion
        {
            No = 1,
            Title = "题",
            Options = new List<ScaleOption> { new() { Value = 0, Label = "a" }, new() { Value = 5, Label = "b" } },
        });
        config.Levels.Add(new ScaleLevel { Code = "only", Label = "仅覆盖 0", Min = 0, Max = 4 });

        Assert.Throws<InvalidOperationException>(() =>
            ScaleEngine.Score(config, new List<ScaleAnswerInput> { new(1, 5) }));
    }

    [Fact]
    public void DecimalMultiplier_RoundsAwayFromZero()
    {
        var config = new ScaleConfigDocument
        {
            Code = "rounding",
            Scoring = new ScaleScoring { Type = "sum", Multiplier = 1.25m, Decimals = 0 },
        };
        config.Questions.Add(new ScaleQuestion
        {
            No = 1,
            Title = "题一",
            Options = new List<ScaleOption> { new() { Value = 1, Label = "a" }, new() { Value = 2, Label = "b" } },
        });
        config.Questions.Add(new ScaleQuestion
        {
            No = 2,
            Title = "题二",
            Options = new List<ScaleOption> { new() { Value = 1, Label = "a" }, new() { Value = 2, Label = "b" } },
        });
        config.Levels.Add(new ScaleLevel { Code = "low", Label = "低", Min = 0, Max = 10 });

        // 原始分 1 + 2 = 3 × 1.25 = 3.75 → 四舍五入（远离零）= 4
        var result = ScaleEngine.Score(config, new List<ScaleAnswerInput> { new(1, 1), new(2, 2) });
        Assert.Equal(4, result.TotalScore);
    }
}
