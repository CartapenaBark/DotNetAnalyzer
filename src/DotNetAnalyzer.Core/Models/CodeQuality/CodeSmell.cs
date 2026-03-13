namespace DotNetAnalyzer.Core.Models.CodeQuality;

/// <summary>
/// 代码异味模型
/// </summary>
/// <remarks>
/// 表示检测到的一个代码异味，包含位置、类型、严重程度和修复建议等信息。
/// </remarks>
public class CodeSmell
{
    /// <summary>
    /// 异味类型（如 "long-method", "large-class"）
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// 异味显示名称（如 "长方法"）
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// 异味描述
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// 严重程度
    /// </summary>
    public CodeSmellSeverity Severity { get; set; }

    /// <summary>
    /// 异味位置
    /// </summary>
    public required CodeLocation Location { get; set; }

    /// <summary>
    /// 检测到的具体指标（如方法行数、类复杂度等）
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();

    /// <summary>
    /// 修复建议
    /// </summary>
    public required string Suggestion { get; set; }

    /// <summary>
    /// 估算的修复时间（小时）
    /// </summary>
    public double EstimatedFixTimeHours { get; set; }

    /// <summary>
    /// 相关的符号信息（如类型名、方法名）
    /// </summary>
    public string? SymbolName { get; set; }

    /// <summary>
    /// 检测时间
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 代码位置
/// </summary>
public class CodeLocation
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// 起始行号（从 0 开始）
    /// </summary>
    public int StartLine { get; set; }

    /// <summary>
    /// 起始列号（从 0 开始）
    /// </summary>
    public int StartColumn { get; set; }

    /// <summary>
    /// 结束行号（从 0 开始）
    /// </summary>
    public int EndLine { get; set; }

    /// <summary>
    /// 结束列号（从 0 开始）
    /// </summary>
    public int EndColumn { get; set; }

    /// <summary>
    /// 生成可读的位置字符串
    /// </summary>
    public string ToDisplayString()
    {
        var fileName = Path.GetFileName(FilePath);
        return $"{fileName}:{StartLine + 1}:{StartColumn + 1}";
    }
}

/// <summary>
/// 代码异味严重程度
/// </summary>
public enum CodeSmellSeverity
{
    /// <summary>
    /// 轻微
    /// </summary>
    Minor = 0,

    /// <summary>
    /// 重要
    /// </summary>
    Major = 1,

    /// <summary>
    /// 严重
    /// </summary>
    Critical = 2
}

/// <summary>
/// 代码异味集合
/// </summary>
public class CodeSmellCollection
{
    /// <summary>
    /// 所有代码异味
    /// </summary>
    public List<CodeSmell> Smells { get; set; } = new();

    /// <summary>
    /// 按严重程度分组
    /// </summary>
    public Dictionary<CodeSmellSeverity, List<CodeSmell>> BySeverity()
    {
        return Smells.GroupBy(s => s.Severity)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// 按类型分组
    /// </summary>
    public Dictionary<string, List<CodeSmell>> ByType()
    {
        return Smells.GroupBy(s => s.Type)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// 按文件分组
    /// </summary>
    public Dictionary<string, List<CodeSmell>> ByFile()
    {
        return Smells.GroupBy(s => s.Location.FilePath)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// 获取指定严重程度的异味
    /// </summary>
    public List<CodeSmell> GetBySeverity(CodeSmellSeverity severity)
    {
        return Smells.Where(s => s.Severity == severity).ToList();
    }

    /// <summary>
    /// 获取指定最小严重程度的异味
    /// </summary>
    public List<CodeSmell> GetWithMinSeverity(CodeSmellSeverity minSeverity)
    {
        return Smells.Where(s => s.Severity >= minSeverity).ToList();
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public CodeSmellStatistics GetStatistics()
    {
        return new CodeSmellStatistics
        {
            TotalCount = Smells.Count,
            BySeverity = BySeverity().ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Count),
            TotalEstimatedFixTime = Smells.Sum(s => s.EstimatedFixTimeHours),
            MostAffectedFiles = ByFile()
                .OrderByDescending(kvp => kvp.Value.Count)
                .Take(10)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count)
        };
    }
}

/// <summary>
/// 代码异味统计信息
/// </summary>
public class CodeSmellStatistics
{
    /// <summary>
    /// 总数量
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 按严重程度统计
    /// </summary>
    public Dictionary<CodeSmellSeverity, int> BySeverity { get; set; } = new();

    /// <summary>
    /// 总估算修复时间（小时）
    /// </summary>
    public double TotalEstimatedFixTime { get; set; }

    /// <summary>
    /// 最受影响的文件（Top 10）
    /// </summary>
    public Dictionary<string, int> MostAffectedFiles { get; set; } = new();
}
