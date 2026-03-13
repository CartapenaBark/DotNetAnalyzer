using DotNetAnalyzer.Core.Skills.Models;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Skills.Workflows;

/// <summary>
/// dotnet-diagnose Skill 的工作流实现
/// </summary>
/// <remarks>
/// 此工作流执行错误和异常诊断流程：
/// <list type="bullet">
///   <item>1. 分析错误类型</item>
///   <item>2. 收集错误信息</item>
///   <item>3. 定位问题代码</item>
///   <item>4. 提供解决方案</item>
/// </list>
/// </remarks>
public class DiagnoseWorkflow
{
    private readonly ILogger<DiagnoseWorkflow> _logger;

    /// <summary>
    /// 初始化 <see cref="DiagnoseWorkflow"/> 类的新实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public DiagnoseWorkflow(ILogger<DiagnoseWorkflow> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 创建 dotnet-diagnose Skill 定义
    /// </summary>
    public SkillDefinition CreateSkillDefinition()
    {
        return new SkillDefinition
        {
            Name = "dotnet-diagnose",
            DisplayName = ".NET 问题诊断",
            Description = "深度诊断 .NET 代码错误、异常和性能问题",
            Version = "1.0.0",
            Category = "Debugging",
            Tags = new[] { ".net", "csharp", "debugging", "errors", "diagnostics", "troubleshooting" },
            Triggers = new SkillTriggers
            {
                Keywords = new[]
                {
                    "错误", "异常", "bug", "诊断", "debug", "为什么报错",
                    "error", "exception", "diagnose", "troubleshoot", "fix",
                    "编译错误", "运行时错误", "null 引用", "空引用"
                },
                Contexts = new[] { "error_message", "exception_stack", "compiler_output", "test_failure" },
                Requires = new[] { "dotnet_project", "mcp_server:dotnet-analyzer" }
            },
            McpTools = new[]
            {
                "get_diagnostics",
                "analyze_code",
                "resolve_symbol",
                "get_definition_and_references",
                "get_semantic_model"
            },
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    // 1. 分析错误类型
                    new()
                    {
                        Name = "analyze_error_type",
                        Tool = "auto",
                        Description = "分析错误类型",
                        Required = true
                    },

                    // 2. 收集错误信息
                    new()
                    {
                        Name = "collect_error_info",
                        Tool = "internal",
                        Description = "收集错误信息",
                        Required = true,
                        DependsOn = new[] { "analyze_error_type" }
                    },

                    // 3. 定位问题
                    new()
                    {
                        Name = "locate_problem",
                        Tool = "mcp",
                        Description = "定位问题代码",
                        Required = true,
                        DependsOn = new[] { "collect_error_info" }
                    },

                    // 4. 查找解决方案
                    new()
                    {
                        Name = "find_solution",
                        Tool = "internal",
                        Description = "提供解决方案",
                        Required = true,
                        DependsOn = new[] { "locate_problem" }
                    }
                }
            },
            Outputs = new[]
            {
                new SkillOutput
                {
                    Format = "markdown",
                    Template = "DiagnosisReport.md"
                }
            }
        };
    }

    /// <summary>
    /// 分析错误请求
    /// </summary>
    public DiagnosisPlan AnalyzeRequest(WorkflowContext context)
    {
        var userInput = context.UserInput ?? string.Empty;

        var plan = new DiagnosisPlan
        {
            Query = userInput,
            CreatedAt = DateTime.UtcNow
        };

        // 分析错误类型
        plan.ErrorType = IdentifyErrorType(userInput);

        // 提取错误详情
        plan.ErrorDetails = ExtractErrorDetails(userInput);

        return plan;
    }

    /// <summary>
    /// 识别错误类型
    /// </summary>
    private static string IdentifyErrorType(string input)
    {
        var lowerInput = input.ToLowerInvariant();

        // NullReferenceException
        if (lowerInput.Contains("null") || lowerInput.Contains("空引用") ||
            lowerInput.Contains("nullreference") || lowerInput.Contains("对象引用"))
            return "NullReferenceException";

        // ArgumentException
        if (lowerInput.Contains("参数") && lowerInput.Contains("无效") ||
            lowerInput.Contains("argument") || lowerInput.Contains("argumentoutofrange"))
            return "ArgumentException";

        // InvalidOperationException
        if (lowerInput.Contains("无效操作") || lowerInput.Contains("无效的状态") ||
            lowerInput.Contains("invalidoperation"))
            return "InvalidOperationException";

        // IOException
        if (lowerInput.Contains("文件") && (lowerInput.Contains("未找到") || lowerInput.Contains("不存在")) ||
            lowerInput.Contains("io") || lowerInput.Contains("file not found"))
            return "IOException";

        // FormatException
        if (lowerInput.Contains("格式") && lowerInput.Contains("错误") ||
            lowerInput.Contains("format"))
            return "FormatException";

        // TimeoutException
        if (lowerInput.Contains("超时") || lowerInput.Contains("timeout"))
            return "TimeoutException";

        // Compilation Error
        if (lowerInput.Contains("编译") || lowerInput.Contains("compile") ||
            lowerInput.Contains("cs") && lowerInput.Contains("error"))
            return "CompilationError";

        // Type Load Exception
        if (lowerInput.Contains("类型") && lowerInput.Contains("加载") ||
            lowerInput.Contains("typeload") || lowerInput.Contains("could not load type"))
            return "TypeLoadException";

        return "UnknownError";
    }

    /// <summary>
    /// 提取错误详情
    /// </summary>
    private static ErrorDetails ExtractErrorDetails(string input)
    {
        var details = new ErrorDetails();

        // 提取文件名
        var fileMatch = System.Text.RegularExpressions.Regex.Match(
            input,
            @"([a-zA-Z_][a-zA-Z0-9_/\\]*\.cs)");

        if (fileMatch.Success)
        {
            details.File = fileMatch.Groups[1].Value;
        }

        // 提取行号
        var lineMatch = System.Text.RegularExpressions.Regex.Match(
            input,
            @"(?:行|line|:)\s*(\d+)");

        if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out var line))
        {
            details.Line = line;
        }

        // 提取错误代码
        var codeMatch = System.Text.RegularExpressions.Regex.Match(
            input,
            @"\b(CS\d+)\b");

        if (codeMatch.Success)
        {
            details.ErrorCode = codeMatch.Groups[1].Value;
        }

        // 提取错误消息
        var messageMatch = System.Text.RegularExpressions.Regex.Match(
            input,
            @"(?:错误|error|:)\s*(.+?)(?:\n|$)");

        if (messageMatch.Success)
        {
            details.Message = messageMatch.Groups[1].Value.Trim();
        }

        return details;
    }

    /// <summary>
    /// 生成诊断报告
    /// </summary>
    public string GenerateReport(DiagnosisResult result)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("## 🐛 诊断报告");
        sb.AppendLine();
        sb.AppendLine($"**错误类型**: {result.ErrorType}");
        sb.AppendLine($"**诊断时间**: {result.DiagnosedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // 问题定位
        sb.AppendLine("## 📍 问题定位");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(result.File))
        {
            sb.AppendLine($"**文件**: `{result.File}`");
        }

        if (result.Line > 0)
        {
            sb.AppendLine($"**行号**: {result.Line}");
        }

        if (!string.IsNullOrEmpty(result.ErrorCode))
        {
            sb.AppendLine($"**错误代码**: {result.ErrorCode}");
        }

        if (!string.IsNullOrEmpty(result.Message))
        {
            sb.AppendLine($"**错误消息**: {result.Message}");
        }

        sb.AppendLine();

        // 根本原因
        sb.AppendLine("## 🔍 根本原因");
        sb.AppendLine();
        foreach (var cause in result.RootCauses)
        {
            sb.AppendLine($"- {cause}");
        }
        sb.AppendLine();

        // 解决方案
        sb.AppendLine("## 💡 解决方案");
        sb.AppendLine();
        foreach (var solution in result.Solutions)
        {
            sb.AppendLine($"### {solution.Title}");
            sb.AppendLine();
            sb.AppendLine(solution.Description);
            sb.AppendLine();

            if (!string.IsNullOrEmpty(solution.CodeExample))
            {
                sb.AppendLine("**示例代码**:");
                sb.AppendLine("```csharp");
                sb.AppendLine(solution.CodeExample);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        // 预防措施
        if (result.Preventions.Count > 0)
        {
            sb.AppendLine("## 🛡️ 预防措施");
            sb.AppendLine();
            foreach (var prevention in result.Preventions)
            {
                sb.AppendLine($"- {prevention}");
            }
            sb.AppendLine();
        }

        // 相关资源
        if (result.Resources.Count > 0)
        {
            sb.AppendLine("## 📚 相关资源");
            sb.AppendLine();
            foreach (var resource in result.Resources)
            {
                sb.AppendLine($"- [{resource.Title}]({resource.Url})");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 分析常见错误并生成诊断结果
    /// </summary>
    public static DiagnosisResult DiagnoseCommonError(string errorType, ErrorDetails details)
    {
        var result = new DiagnosisResult
        {
            ErrorType = errorType,
            DiagnosedAt = DateTime.UtcNow,
            File = details.File,
            Line = details.Line,
            ErrorCode = details.ErrorCode,
            Message = details.Message
        };

        // 根据错误类型生成诊断信息
        switch (errorType)
        {
            case "NullReferenceException":
                result.RootCauses = new List<string>
                {
                    "变量或属性未初始化",
                    "对象引用在赋值前被访问",
                    "集合或数组访问时元素不存在",
                    "方法返回 null 但未检查"
                };
                result.Solutions = new List<Solution>
                {
                    new Solution
                    {
                        Title = "添加 null 检查",
                        Description = "在使用对象前检查其是否为 null",
                        CodeExample = @"if (obj != null)
{
    obj.DoSomething();
}"
                    },
                    new Solution
                    {
                        Title = "使用 null-条件运算符",
                        Description = "简化 null 检查语法",
                        CodeExample = @"obj?.DoSomething();
// 或
var value = obj?.Property ?? defaultValue;"
                    },
                    new Solution
                    {
                        Title = "使用 null-forgiving 运算符",
                        Description = "从 C# 8.0 开始，可以消除 null 检查",
                        CodeExample = @"obj?.DoSomething();"
                    }
                };
                result.Preventions = new List<string>
                {
                    "始终初始化引用类型",
                    "使用可空值类型（T?）明确标识可为 null 的类型",
                    "启用 nullable 引用类型检查（#nullable enable）"
                };
                break;

            case "ArgumentException":
                result.RootCauses = new List<string>
                {
                    "传递了 null 或无效的参数",
                    "参数值超出有效范围",
                    "参数格式不正确"
                };
                result.Solutions = new List<Solution>
                {
                    new Solution
                    {
                        Title = "添加参数验证",
                        Description = "在方法开头验证参数",
                        CodeExample = @"public void DoWork(string value)
{
    if (string.IsNullOrEmpty(value))
        throw new ArgumentException(nameof(value));

    // 方法逻辑
}"
                    }
                };
                result.Preventions = new List<string>
                {
                    "使用契约（Code Contracts）或验证库",
                    "在 XML 文档注释中说明参数要求"
                };
                break;

            case "CompilationError":
                result.RootCauses = new List<string>
                {
                    "语法错误",
                    "缺少 using 引用",
                    "类型不匹配",
                    "方法签名不匹配"
                };
                result.Solutions = new List<Solution>
                {
                    new Solution
                    {
                        Title = "检查语法",
                        Description = "查看错误代码并修复语法问题"
                    },
                    new Solution
                    {
                        Title = "添加 using 引用",
                        Description = "添加缺失的命名空间引用",
                        CodeExample = @"using System;
using System.Collections.Generic;
using System.Linq;"
                    }
                };
                break;

            case "IOException":
                result.RootCauses = new List<string>
                {
                    "文件不存在",
                    "路径错误",
                    "权限不足",
                    "文件被占用"
                };
                result.Solutions = new List<Solution>
                {
                    new Solution
                    {
                        Title = "检查文件路径",
                        Description = "确认文件路径正确，使用绝对路径或相对于程序集基目录的路径",
                        CodeExample = @"var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ""data.txt"");
if (File.Exists(path))
{
    // 处理文件
}"
                    },
                    new Solution
                    {
                        Title = "检查文件权限",
                        Description = "确保程序有权限访问文件"
                    }
                };
                break;

            default:
                result.RootCauses = new List<string>
                {
                    "需要更多信息来诊断问题"
                };
                result.Solutions = new List<Solution>
                {
                    new Solution
                    {
                        Title = "提供更多上下文",
                        Description = "请提供完整的错误消息、堆栈跟踪和相关代码"
                    }
                };
                break;
        }

        return result;
    }
}

/// <summary>
/// 诊断计划
/// </summary>
public class DiagnosisPlan
{
    /// <summary>
    /// 用户查询
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 错误类型
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// 错误详情
    /// </summary>
    public ErrorDetails ErrorDetails { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 错误详情
/// </summary>
public class ErrorDetails
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// 行号
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// 错误代码
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// 诊断结果
/// </summary>
public class DiagnosisResult
{
    /// <summary>
    /// 错误类型
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// 诊断时间
    /// </summary>
    public DateTime DiagnosedAt { get; set; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// 行号
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// 错误代码
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 根本原因列表
    /// </summary>
    public List<string> RootCauses { get; set; } = new();

    /// <summary>
    /// 解决方案列表
    /// </summary>
    public List<Solution> Solutions { get; set; } = new();

    /// <summary>
    /// 预防措施列表
    /// </summary>
    public List<string> Preventions { get; set; } = new();

    /// <summary>
    /// 相关资源
    /// </summary>
    public List<Resource> Resources { get; set; } = new();
}

/// <summary>
/// 解决方案
/// </summary>
public class Solution
{
    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 代码示例
    /// </summary>
    public string? CodeExample { get; set; }
}

/// <summary>
/// 资源链接
/// </summary>
public class Resource
{
    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
