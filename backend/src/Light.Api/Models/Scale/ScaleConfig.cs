using System.Text.Json.Serialization;

namespace Light.Api.Models.Scale;

/// <summary>
/// 量表配置文档（scales.config_json 的反序列化模型）。
/// 硬性规则 5：题目、选项、计分、分级、预警规则全部在配置里，新增量表不改代码。
/// </summary>
public sealed class ScaleConfigDocument
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    [JsonPropertyName("source")]
    public ScaleSource? Source { get; set; }

    [JsonPropertyName("questions")]
    public List<ScaleQuestion> Questions { get; set; } = new();

    [JsonPropertyName("scoring")]
    public ScaleScoring Scoring { get; set; } = new();

    [JsonPropertyName("levels")]
    public List<ScaleLevel> Levels { get; set; } = new();

    [JsonPropertyName("crisisRules")]
    public List<ScaleCrisisRule> CrisisRules { get; set; } = new();

    [JsonPropertyName("disclaimer")]
    public string? Disclaimer { get; set; }
}

/// <summary>来源标注（内容合规要求：信息来源 + 是否已人工核实）。</summary>
public sealed class ScaleSource
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public sealed class ScaleQuestion
{
    [JsonPropertyName("no")]
    public int No { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("options")]
    public List<ScaleOption> Options { get; set; } = new();
}

public sealed class ScaleOption
{
    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}

/// <summary>计分规则：type=sum；multiplier 用于 SDS 标准分（原始分 × 1.25）；decimals 为保留小数位。</summary>
public sealed class ScaleScoring
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "sum";

    [JsonPropertyName("multiplier")]
    public decimal Multiplier { get; set; } = 1m;

    [JsonPropertyName("decimals")]
    public int Decimals { get; set; }
}

/// <summary>分级区间：Min ≤ 总分 ≤ Max 命中；各区间的文案不得含诊断表述（硬性规则 1）。</summary>
public sealed class ScaleLevel
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("min")]
    public int Min { get; set; }

    [JsonPropertyName("max")]
    public int Max { get; set; }
}

/// <summary>
/// 预警规则（危机干预触发）。当前支持 type=questionAtLeast（某题得分 ≥ MinValue，如 PHQ-9 第 9 题 ≥1）。
/// 扩展新规则类型只改 ScaleEngine，不动表结构。
/// </summary>
public sealed class ScaleCrisisRule
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "questionAtLeast";

    [JsonPropertyName("questionNo")]
    public int QuestionNo { get; set; }

    [JsonPropertyName("minValue")]
    public int MinValue { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
