using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetAnalyzer.Core.Architecture.Models;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Reporting;

/// <summary>
/// SARIF (Static Analysis Results Interchange Format) v2.1.0 报告生成器。
/// </summary>
/// <remarks>
/// 将架构违规和代码异味转换为标准 SARIF JSON 格式，
/// 以便与 GitHub Code Scanning、Azure DevOps 等平台集成。
/// </remarks>
public static class SarifReportGenerator
{
    private static readonly JsonSerializerOptions s_sarifOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 从架构规则检查报告生成 SARIF JSON
    /// </summary>
    /// <param name="report">架构规则检查报告</param>
    /// <param name="projectPath">项目路径，用于 SARIF 的 artifacts</param>
    /// <returns>SARIF v2.1.0 格式的 JSON 字符串</returns>
    public static string GenerateFromArchitectureReport(
        ArchitectureReport report,
        string projectPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(projectPath);

        var results = report.Violations
            .Select(v => CreateSarifResult(v))
            .ToList();

        var sarifLog = BuildSarifObject(
            projectPath,
            report.Violations
                .Select(v => CreateSarifResult(v))
                .ToList(),
            report.Violations
                .GroupBy(v => v.RuleName)
                .Select(g =>
                {
                    var first = g.First();
                    return new
                    {
                        id = NormalizeRuleId(first.RuleName),
                        name = first.RuleName,
                        shortDescription = new
                        {
                            text = first.RuleName
                        },
                        fullDescription = new
                        {
                            text = first.Message
                        },
                        defaultConfiguration = new
                        {
                            level = MapSeverityToLevel(first.Severity)
                        },
                        helpUri =
                            $"https://github.com/CartapenaBark/DotNetAnalyzer#{NormalizeRuleId(first.RuleName)}"
                    };
                })
                .ToArray());

        return JsonSerializer.Serialize(sarifLog, s_sarifOptions);
    }

    /// <summary>
    /// 从代码异味集合生成 SARIF JSON
    /// </summary>
    /// <param name="smells">代码异味集合</param>
    /// <param name="projectPath">项目路径，用于 SARIF 的 artifacts</param>
    /// <returns>SARIF v2.1.0 格式的 JSON 字符串</returns>
    public static string GenerateFromCodeSmells(
        CodeSmellCollection smells,
        string projectPath)
    {
        ArgumentNullException.ThrowIfNull(smells);
        ArgumentNullException.ThrowIfNull(projectPath);

        var results = smells.Smells
            .Select(s => CreateSarifResult(s))
            .ToList();

        var sarifLog = BuildSarifObject(
            projectPath,
            results,
            smells.Smells
                .GroupBy(s => s.Type)
                .Select(g =>
                {
                    var first = g.First();
                    return new
                    {
                        id = NormalizeRuleId(first.Type),
                        name = first.DisplayName,
                        shortDescription = new
                        {
                            text = first.DisplayName
                        },
                        fullDescription = new
                        {
                            text = first.Description
                        },
                        defaultConfiguration = new
                        {
                            level = MapCodeSmellSeverityToLevel(
                                first.Severity)
                        },
                        helpUri =
                            $"https://github.com/CartapenaBark/DotNetAnalyzer#{NormalizeRuleId(first.Type)}"
                    };
                })
                .ToArray());

        return JsonSerializer.Serialize(sarifLog, s_sarifOptions);
    }

    private static object CreateSarifResult(
        ArchitectureViolation violation)
    {
        return new
        {
            ruleId = NormalizeRuleId(violation.RuleName),
            level = MapSeverityToLevel(violation.Severity),
            message = new
            {
                text = violation.Message
            },
            locations = new[]
            {
                new
                {
                    physicalLocation = new
                    {
                        artifactLocation = new
                        {
                            uri = violation.FilePath
                        },
                        region = new
                        {
                            startLine = violation.LineNumber + 1,
                            startColumn = 1
                        }
                    }
                }
            },
            properties = violation.Suggestion != null
                ? new
                {
                    suggestion = violation.Suggestion
                }
                : null
        };
    }

    private static object CreateSarifResult(CodeSmell smell)
    {
        return new
        {
            ruleId = NormalizeRuleId(smell.Type),
            level = MapCodeSmellSeverityToLevel(smell.Severity),
            message = new
            {
                text = smell.Description
            },
            locations = new[]
            {
                new
                {
                    physicalLocation = new
                    {
                        artifactLocation = new
                        {
                            uri = smell.Location.FilePath
                        },
                        region = new
                        {
                            startLine = smell.Location.StartLine + 1,
                            startColumn = smell.Location.StartColumn + 1,
                            endLine = smell.Location.EndLine + 1,
                            endColumn = smell.Location.EndColumn + 1
                        }
                    }
                }
            },
            properties = new
            {
                suggestion = smell.Suggestion,
                estimatedFixTimeHours = smell.EstimatedFixTimeHours,
                symbolName = smell.SymbolName
            }
        };
    }

    /// <summary>
    /// 将架构违规严重程度映射到 SARIF level
    /// </summary>
    public static string MapSeverityToLevel(string severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "error" => "error",
            "warning" or "warn" => "warning",
            "info" or "information" => "note",
            _ => "warning"
        };
    }

    /// <summary>
    /// 将代码异味严重程度映射到 SARIF level
    /// </summary>
    public static string MapCodeSmellSeverityToLevel(
        CodeSmellSeverity severity)
    {
        return severity switch
        {
            CodeSmellSeverity.Critical => "error",
            CodeSmellSeverity.Major => "warning",
            CodeSmellSeverity.Minor => "note",
            _ => "warning"
        };
    }

    /// <summary>
    /// 将规则名称标准化为 SARIF ruleId（小写、连字符分隔）
    /// </summary>
    public static string NormalizeRuleId(string ruleName)
    {
        if (string.IsNullOrEmpty(ruleName))
        {
            return "unknown";
        }

        // 空格替换为连字符，小写处理
        return ruleName
            .ToLowerInvariant()
            .Replace(' ', '-');
    }

    private static string GetToolVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return !string.IsNullOrEmpty(informationalVersion)
            ? informationalVersion.Split('+', 2)[0]
            : version;
    }

    /// <summary>
    /// 使用 Dictionary 构建 SARIF 对象，因为 $schema 不是合法的 C# 匿名类型属性名
    /// </summary>
    private static Dictionary<string, object> BuildSarifObject(
        string projectPath,
        List<object> results,
        object[] rules)
    {
        return new Dictionary<string, object>
        {
            ["$schema"] =
                "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/main/sarif-2.1/schema/sarif-schema-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["tool"] = new Dictionary<string, object>
                    {
                        ["driver"] = new Dictionary<string, object>
                        {
                            ["name"] = "DotNetAnalyzer",
                            ["version"] = GetToolVersion(),
                            ["informationUri"] =
                                "https://github.com/CartapenaBark/DotNetAnalyzer",
                            ["rules"] = rules
                        }
                    },
                    ["artifacts"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["location"] = new Dictionary<string, object>
                            {
                                ["uri"] = projectPath
                            }
                        }
                    },
                    ["results"] = results
                }
            }
        };
    }
}
