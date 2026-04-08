using DotNetAnalyzer.Core.Analysis;
using DotNetAnalyzer.Core.Models.CodeQuality;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DotNetAnalyzer.Core.Visualization;

/// <summary>
/// 热力图生成器
/// </summary>
/// <remarks>
/// 生成代码复杂度和变更频率的热力图数据。
/// </remarks>
public class HeatmapGenerator
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<HeatmapGenerator> _logger;

    /// <summary>
    /// 初始化 <see cref="HeatmapGenerator"/> 的新实例
    /// </summary>
    public HeatmapGenerator(ILogger<HeatmapGenerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 基于 Git 历史生成变更频率热力图。
    /// </summary>
    /// <remarks>
    /// 调用 <see cref="GitHistoryProvider"/> 获取真实变更数据，
    /// 然后委托给 <see cref="GenerateChangeFrequencyHeatmap"/> 生成热力图。
    /// </remarks>
    /// <param name="gitProvider">Git 历史记录提供器。</param>
    /// <param name="repositoryPath">Git 仓库根目录路径。</param>
    /// <param name="periodDays">回溯天数（默认 30 天）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>热力图数据。</returns>
    public static async Task<HeatmapData> GenerateChangeFrequencyHeatmapFromGit(
        GitHistoryProvider gitProvider,
        string repositoryPath,
        int periodDays = 30,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gitProvider);

        var changeHistory = await gitProvider.GetChangeHistoryAsync(
            repositoryPath, periodDays, cancellationToken);

        var data = GenerateChangeFrequencyHeatmap(changeHistory);
        data.Title = $"Change frequency heatmap (last {periodDays} days)";

        return data;
    }

    /// <summary>
    /// 生成复杂度热力图
    /// </summary>
    /// <param name="smellCollection">代码异味集合</param>
    /// <returns>热力图数据</returns>
    public static HeatmapData GenerateComplexityHeatmap(CodeSmellCollection smellCollection)
    {
        var data = new HeatmapData
        {
            Title = "Code complexity heatmap",
            Type = HeatmapType.Complexity
        };

        // 按文件分组
        var byFile = smellCollection.ByFile();

        foreach (var kvp in byFile)
        {
            var fileName = Path.GetFileName(kvp.Key);
            var smells = kvp.Value;

            // 计算文件的总复杂度分数
            var complexityScore = smells.Sum(s =>
            {
                var severityMultiplier = s.Severity switch
                {
                    CodeSmellSeverity.Critical => 3,
                    CodeSmellSeverity.Major => 2,
                    CodeSmellSeverity.Minor => 1,
                    _ => 1
                };

                return severityMultiplier * (s.Metrics.TryGetValue("lineCount", out var lines) ? (int)lines : 1);
            });

            data.Cells.Add(new HeatmapCell
            {
                Label = fileName,
                Value = complexityScore,
                Tooltip = $"{fileName}: {smells.Count} code smells",
                Metadata = new Dictionary<string, object>
                {
                    ["filePath"] = kvp.Key,
                    ["smellCount"] = smells.Count,
                    ["criticalCount"] = smells.Count(s => s.Severity == CodeSmellSeverity.Critical),
                    ["majorCount"] = smells.Count(s => s.Severity == CodeSmellSeverity.Major),
                    ["minorCount"] = smells.Count(s => s.Severity == CodeSmellSeverity.Minor)
                }
            });
        }

        // 计算热力图颜色范围
        CalculateColorRange(data);

        return data;
    }

    /// <summary>
    /// 生成变更频率热力图
    /// </summary>
    /// <param name="changeHistory">变更历史数据</param>
    /// <returns>热力图数据</returns>
    public static HeatmapData GenerateChangeFrequencyHeatmap(List<ChangeRecord> changeHistory)
    {
        var data = new HeatmapData
        {
            Title = "Change frequency heatmap",
            Type = HeatmapType.ChangeFrequency
        };

        // 按文件分组统计变更次数
        var byFile = changeHistory.GroupBy(c => c.FilePath);

        foreach (var group in byFile)
        {
            var fileName = Path.GetFileName(group.Key);
            var changeCount = group.Count();
            var lastChange = group.Max(c => c.Timestamp);

            data.Cells.Add(new HeatmapCell
            {
                Label = fileName,
                Value = changeCount,
                Tooltip = $"{fileName}: {changeCount} changes",
                Metadata = new Dictionary<string, object>
                {
                    ["filePath"] = group.Key,
                    ["changeCount"] = changeCount,
                    ["lastChange"] = lastChange
                }
            });
        }

        CalculateColorRange(data);

        return data;
    }

    /// <summary>
    /// 生成热力图的 Mermaid 图表
    /// </summary>
    public static string GenerateMermaidChart(HeatmapData data)
    {
        var builder = new System.Text.StringBuilder();

        builder.AppendLine("```mermaid");
        builder.AppendLine("xychart-beta");
        builder.AppendLine($"    title \"{data.Title}\"");
        builder.AppendLine("    x-axis [");

        for (int i = 0; i < data.Cells.Count; i++)
        {
            var cell = data.Cells[i];
            var label = cell.Label.Length > 20 ? string.Concat(cell.Label.AsSpan(0, 17), "...") : cell.Label;
            builder.Append($"        \"{label}\"");

            if (i < data.Cells.Count - 1)
            {
                builder.AppendLine(",");
            }
            else
            {
                builder.AppendLine();
            }
        }

        builder.AppendLine("    ]");
        builder.AppendLine("    y-axis \"Hotspot\" 0 -->");

        var maxValue = data.Cells.Count > 0 ? data.Cells.Max(c => c.Value) : 100;
        builder.AppendLine($"        {maxValue}");

        builder.AppendLine("    bar [");

        for (int i = 0; i < data.Cells.Count; i++)
        {
            var cell = data.Cells[i];
            builder.Append($"        {cell.Value}");

            if (i < data.Cells.Count - 1)
            {
                builder.AppendLine(",");
            }
            else
            {
                builder.AppendLine();
            }
        }

        builder.AppendLine("    ]");
        builder.AppendLine("```");

        return builder.ToString();
    }

    /// <summary>
    /// 生成热力图的 JSON 数据
    /// </summary>
    public static string GenerateJsonData(HeatmapData data)
    {
        var jsonData = new
        {
            title = data.Title,
            type = data.Type.ToString(),
            minValue = data.MinValue,
            maxValue = data.MaxValue,
            cells = data.Cells.Select(c => new
            {
                label = c.Label,
                value = c.Value,
                color = c.Color,
                tooltip = c.Tooltip,
                metadata = c.Metadata
            })
        };

        return JsonSerializer.Serialize(jsonData, s_jsonOptions);
    }

    private static void CalculateColorRange(HeatmapData data)
    {
        if (data.Cells.Count == 0)
        {
            data.MinValue = 0;
            data.MaxValue = 100;
            return;
        }

        data.MinValue = data.Cells.Min(c => c.Value);
        data.MaxValue = data.Cells.Max(c => c.Value);

        // 为每个单元格计算颜色
        foreach (var cell in data.Cells)
        {
            cell.Color = CalculateHeatColor(cell.Value, data.MinValue, data.MaxValue);
        }
    }

    private static string CalculateHeatColor(double value, double min, double max)
    {
        if (max == min) return "#FFD700"; // 金色

        var ratio = (value - min) / (max - min);

        // 从绿色（低）到红色（高）
        if (ratio < 0.25)
        {
            return "#90EE90"; // 浅绿
        }
        else if (ratio < 0.5)
        {
            return "#FFD700"; // 金色
        }
        else if (ratio < 0.75)
        {
            return "#FFA500"; // 橙色
        }
        else
        {
            return "#FF6347"; // 红色
        }
    }
}

/// <summary>
/// 热力图数据
/// </summary>
public class HeatmapData
{
    public string Title { get; set; } = string.Empty;
    public HeatmapType Type { get; set; }
    public List<HeatmapCell> Cells { get; set; } = new();
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
}

/// <summary>
/// 热力图单元格
/// </summary>
public class HeatmapCell
{
    public required string Label { get; set; }
    public double Value { get; set; }
    public string Color { get; set; } = "#FFD700";
    public required string Tooltip { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 热力图类型
/// </summary>
public enum HeatmapType
{
    /// <summary>
    /// 复杂度热力图
    /// </summary>
    Complexity,

    /// <summary>
    /// 变更频率热力图
    /// </summary>
    ChangeFrequency
}

/// <summary>
/// 变更记录
/// </summary>
public class ChangeRecord
{
    public required string FilePath { get; set; }
    public DateTime Timestamp { get; set; }
    public ChangeType ChangeType { get; set; }
}
