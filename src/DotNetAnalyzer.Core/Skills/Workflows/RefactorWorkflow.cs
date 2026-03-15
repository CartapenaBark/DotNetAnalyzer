using DotNetAnalyzer.Core.Skills.Models;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Skills.Workflows;

/// <summary>
/// dotnet-refactor Skill 的工作流实现
/// </summary>
/// <remarks>
/// 此工作流执行引导式代码重构流程：
/// <list type="bullet">
///   <item>1. 识别重构类型（提取方法、重命名、提取接口等）</item>
///   <item>2. 收集重构参数</item>
///   <item>3. 生成变更预览</item>
///   <item>4. 安全执行重构</item>
///   <item>5. 验证和测试</item>
/// </list>
/// </remarks>
public partial class RefactorWorkflow
{
    private readonly ILogger<RefactorWorkflow> _logger;
    private static readonly string[] item = new[] { "identify_refactoring_type" };
    private static readonly string[] CollectParametersDependsOn = new[] { "identify_refactoring_type" };
    private static readonly string[] PreviewChangesDependsOn = new[] { "collect_parameters" };
    private static readonly string[] ApplyRefactoringDependsOn = new[] { "preview_changes" };
    private static readonly string[] VerifyDependsOn = new[] { "apply_refactoring" };
    private static readonly string[] DefaultParameters = new[] { "value" };
    private static readonly string[] DefaultAffectedFiles = new[] { "File1.cs", "File2.cs" };
    private static readonly string[] DefaultMembers = new[] { "Method1", "Method2" };

    /// <summary>
    /// 初始化 <see cref="RefactorWorkflow"/> 类的新实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public RefactorWorkflow(ILogger<RefactorWorkflow> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 创建 dotnet-refactor Skill 定义
    /// </summary>
    public static SkillDefinition CreateSkillDefinition()
    {
        return new SkillDefinition
        {
            Name = "dotnet-refactor",
            DisplayName = ".NET 重构助手",
            Description = "引导式代码重构操作，支持提取方法、重命名、接口提取等",
            Version = "1.0.0",
            Category = "Refactoring",
            Tags = new[] { ".net", "csharp", "refactoring", "code-improvement", "extract", "rename" },
            Triggers = new SkillTriggers
            {
                Keywords = new[]
                {
                    "重构", "提取", "重命名", "优化", "extract", "refactor",
                    "extract method", "rename variable", "extract interface",
                    "introduce variable", "inline method"
                },
                Contexts = new[] { "code_selection", "method_body", "class_definition", "variable_usage" },
                Requires = new[] { "dotnet_project", "mcp_server:dotnet-analyzer", "code_selection" }
            },
            McpTools = new[]
            {
                "extract_method",
                "rename_symbol",
                "introduce_variable",
                "generate_interface_impl",
                "get_refactorings",
                "get_code_actions"
            },
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    // 1. 识别重构类型
                    new()
                    {
                        Name = "identify_refactoring_type",
                        Tool = "auto",
                        Description = "识别重构类型",
                        Required = true
                    },

                    // 2. 收集参数
                    new()
                    {
                        Name = "collect_parameters",
                        Tool = "internal",
                        Description = "收集重构参数",
                        Required = true,
                        DependsOn = CollectParametersDependsOn },

                    // 3. 生成预览
                    new()
                    {
                        Name = "preview_changes",
                        Tool = "internal",
                        Description = "生成变更预览",
                        Required = true,
                        DependsOn = PreviewChangesDependsOn
                    },

                    // 4. 执行重构
                    new()
                    {
                        Name = "apply_refactoring",
                        Tool = "mcp",
                        Description = "执行重构操作",
                        Required = false, // 需要用户确认
                        DependsOn = ApplyRefactoringDependsOn,
                        Condition = "options.confirmed == true"
                    },

                    // 5. 验证
                    new()
                    {
                        Name = "verify",
                        Tool = "internal",
                        Description = "验证结果",
                        Required = true,
                        DependsOn = VerifyDependsOn
                    }
                }
            },
            Outputs = new[]
            {
                new SkillOutput
                {
                    Format = "markdown",
                    Template = "RefactoringReport.md"
                }
            }
        };
    }

    /// <summary>
    /// 分析重构请求并生成重构计划
    /// </summary>
    public static RefactoringPlan AnalyzeRequest(WorkflowContext context)
    {
        var userInput = context.UserInput ?? string.Empty;
        var selectedCode = context.Data.TryGetValue("selected_code", out var code) ? code : null;

        var plan = new RefactoringPlan
        {
            RequestText = userInput,
            SelectedCode = selectedCode,
            CreatedAt = DateTime.UtcNow
        };

        // 识别重构类型
        plan.RefactoringType = RefactorWorkflow.IdentifyRefactoringType(userInput);

        // 提取参数
        plan.Parameters = RefactorWorkflow.ExtractParameters(userInput, context);

        return plan;
    }

    /// <summary>
    /// 识别重构类型
    /// </summary>
    private static string IdentifyRefactoringType(string input)
    {
        var lowerInput = input.ToLowerInvariant();

        // 提取方法相关
        if (lowerInput.Contains("提取") && lowerInput.Contains("方法"))
            return "extract_method";
        if (lowerInput.Contains("extract") && lowerInput.Contains("method"))
            return "extract_method";

        // 重命名相关
        if (lowerInput.Contains("重命名") || lowerInput.Contains("rename"))
        {
            if (lowerInput.Contains("方法") || lowerInput.Contains("method"))
                return "rename_method";
            if (lowerInput.Contains("变量") || lowerInput.Contains("variable"))
                return "rename_variable";
            if (lowerInput.Contains('类') || lowerInput.Contains("class"))
                return "rename_class";
            if (lowerInput.Contains("参数") || lowerInput.Contains("parameter"))
                return "rename_parameter";
            return "rename_symbol";
        }

        // 提取接口相关
        if (lowerInput.Contains("提取") && lowerInput.Contains("接口"))
            return "extract_interface";
        if (lowerInput.Contains("extract") && lowerInput.Contains("interface"))
            return "extract_interface";

        // 引入变量相关
        if (lowerInput.Contains("引入") && lowerInput.Contains("变量"))
            return "introduce_variable";
        if (lowerInput.Contains("introduce") && lowerInput.Contains("variable"))
            return "introduce_variable";

        // 内联方法相关
        if (lowerInput.Contains("内联") && lowerInput.Contains("方法"))
            return "inline_method";
        if (lowerInput.Contains("inline") && lowerInput.Contains("method"))
            return "inline_method";

        // 封装字段相关
        if (lowerInput.Contains("封装") && lowerInput.Contains("字段"))
            return "encapsulate_field";
        if (lowerInput.Contains("encapsulate") && lowerInput.Contains("field"))
            return "encapsulate_field";

        return "unknown";
    }

    /// <summary>
    /// 从用户输入中提取参数
    /// </summary>
    private static Dictionary<string, object> ExtractParameters(string input, WorkflowContext context)
    {
        var parameters = new Dictionary<string, object>();

        // 从用户输入中提取名称
        var nameMatch = MyRegex().Match(input);

        if (nameMatch.Success)
        {
            parameters["newName"] = nameMatch.Groups[1].Value;
        }

        // 提取可见性
        if (input.Contains("public") || input.Contains("公开"))
            parameters["visibility"] = "public";
        else if (input.Contains("private") || input.Contains("私有"))
            parameters["visibility"] = "private";
        else if (input.Contains("protected") || input.Contains("保护"))
            parameters["visibility"] = "protected";
        else if (input.Contains("internal") || input.Contains("内部"))
            parameters["visibility"] = "internal";

        // 添加上下文参数
        if (!string.IsNullOrEmpty(context.CurrentFile))
            parameters["filePath"] = context.CurrentFile;

        if (context.Data.TryGetValue("selection", out var selection))
            parameters["selection"] = selection;

        return parameters;
    }

    /// <summary>
    /// 生成重构预览
    /// </summary>
    public static RefactoringPreview GeneratePreview(RefactoringPlan plan)
    {
        var preview = new RefactoringPreview
        {
            RefactoringType = plan.RefactoringType,
            GeneratedAt = DateTime.UtcNow
        };

        // 根据重构类型生成不同的预览
        preview.Changes = plan.RefactoringType switch
        {
            "extract_method" => RefactorWorkflow.GenerateExtractMethodPreview(plan),
            "rename_symbol" => RefactorWorkflow.GenerateRenamePreview(plan),
            "extract_interface" => RefactorWorkflow.GenerateExtractInterfacePreview(plan),
            "introduce_variable" => RefactorWorkflow.GenerateIntroduceVariablePreview(plan),
            _ => new List<CodeChange>()
        };

        // 添加影响评估
        preview.Impact = RefactorWorkflow.AssessImpact(plan);

        return preview;
    }

    private static List<CodeChange> GenerateExtractMethodPreview(RefactoringPlan plan)
    {
        var changes = new List<CodeChange>();

        var methodName = plan.Parameters.TryGetValue("newName", out var name)
            ? name.ToString()
            : "NewMethod";

        changes.Add(new CodeChange
        {
            Type = "add",
            Description = $"添加新方法: {methodName}",
            Details = new
            {
                returnType = "void", // 可以从上下文推断
                parameters = DefaultParameters,
                accessibility = "private"
            }
        });

        changes.Add(new CodeChange
        {
            Type = "modify",
            Description = "替换选中的代码块",
            Details = new
            {
                original = "...", // 实际代码
                replacement = $"{methodName}();"
            }
        });

        return changes;
    }

    private static List<CodeChange> GenerateRenamePreview(RefactoringPlan plan)
    {
        var changes = new List<CodeChange>();

        var newName = plan.Parameters.TryGetValue("newName", out var name)
            ? name.ToString()
            : "NewName";

        changes.Add(new CodeChange
        {
            Type = "modify",
            Description = $"重命名符号为: {newName}",
            Details = new
            {
                affectedFiles = DefaultAffectedFiles, // 实际需要查找引用
                referenceCount = 2
            }
        });

        return changes;
    }

    private static List<CodeChange> GenerateExtractInterfacePreview(RefactoringPlan plan)
    {
        var changes = new List<CodeChange>();

        var interfaceName = plan.Parameters.TryGetValue("newName", out var name)
            ? name.ToString()
            : "IInterface";

        changes.Add(new CodeChange
        {
            Type = "add",
            Description = $"添加接口: {interfaceName}",
            Details = new
            {
                members = DefaultMembers // 从选中代码提取
            }
        });

        changes.Add(new CodeChange
        {
            Type = "modify",
            Description = $"让类实现接口: {interfaceName}"
        });

        return changes;
    }

    private static List<CodeChange> GenerateIntroduceVariablePreview(RefactoringPlan plan)
    {
        var changes = new List<CodeChange>();

        var varName = plan.Parameters.TryGetValue("newName", out var name)
            ? name.ToString()
            : "newValue";

        changes.Add(new CodeChange
        {
            Type = "add",
            Description = $"声明变量: {varName}",
            Details = new
            {
                type = "var", // 可以从表达式推断
                initializer = "..."
            }
        });

        changes.Add(new CodeChange
        {
            Type = "modify",
            Description = "替换表达式为变量引用"
        });

        return changes;
    }

    private static ImpactAssessment AssessImpact(RefactoringPlan plan)
    {
        return new ImpactAssessment
        {
            Complexity = "low", // 可以根据重构类型评估
            Risk = "low",
            FilesAffected = new[] { "current_file.cs" },
            References = new[] { "File1.cs", "File2.cs" },
            Suggestion = "此重构是安全的，建议执行"
        };
    }

    /// <summary>
    /// 生成重构报告
    /// </summary>
    public static string GenerateReport(RefactoringResult result)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# 🔧 重构操作报告");
        sb.AppendLine();
        sb.AppendLine($"**操作类型**: {result.RefactoringType}");
        sb.AppendLine($"**状态**: {(result.Success ? "成功" : "失败")}");
        sb.AppendLine($"**执行时间**: {result.ExecutedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (result.Success)
        {
            sb.AppendLine("## 📝 变更摘要");
            sb.AppendLine();
            sb.AppendLine($"- 修改文件数: {result.FilesModified}");
            sb.AppendLine($"- 新增方法数: {result.MethodsAdded}");
            sb.AppendLine($"- 修改的方法数: {result.MethodsModified}");
            sb.AppendLine();

            if (result.Changes != null && result.Changes.Count > 0)
            {
                sb.AppendLine("## 🔄 详细变更");
                sb.AppendLine();
                foreach (var change in result.Changes)
                {
                    sb.AppendLine($"### {change.Type}: {change.Description}");
                    if (!string.IsNullOrEmpty(change.File))
                    {
                        sb.AppendLine($"- **文件**: `{change.File}`");
                    }
                    if (change.Details != null)
                    {
                        sb.AppendLine($"- **详情**: {change.Details}");
                    }
                    sb.AppendLine();
                }
            }
        }
        else
        {
            sb.AppendLine("## ❌ 错误信息");
            sb.AppendLine();
            sb.AppendLine(result.ErrorMessage ?? "未知错误");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(result.Suggestion))
            {
                sb.AppendLine("## 💡 建议");
                sb.AppendLine();
                sb.AppendLine(result.Suggestion);
            }
        }

        return sb.ToString();
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"(?:名为|name|命名为|rename to|rename\s+)\s+[""']?([a-zA-Z_][a-zA-Z0-9_]*)[""']?")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}

/// <summary>
/// 重构计划
/// </summary>
public class RefactoringPlan
{
    /// <summary>
    /// 用户请求文本
    /// </summary>
    public string RequestText { get; set; } = string.Empty;

    /// <summary>
    /// 选中的代码
    /// </summary>
    public object? SelectedCode { get; set; }

    /// <summary>
    /// 重构类型
    /// </summary>
    public string RefactoringType { get; set; } = string.Empty;

    /// <summary>
    /// 提取的参数
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 重构预览
/// </summary>
public class RefactoringPreview
{
    /// <summary>
    /// 重构类型
    /// </summary>
    public string RefactoringType { get; set; } = string.Empty;

    /// <summary>
    /// 变更列表
    /// </summary>
    public List<CodeChange> Changes { get; set; } = new();

    /// <summary>
    /// 影响评估
    /// </summary>
    public ImpactAssessment? Impact { get; set; }

    /// <summary>
    /// 生成时间
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// 代码变更
/// </summary>
public class CodeChange
{
    /// <summary>
    /// 变更类型（add, modify, delete）
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 变更描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 文件路径
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// 变更详情（JSON）
    /// </summary>
    public object? Details { get; set; }
}

/// <summary>
/// 影响评估
/// </summary>
public class ImpactAssessment
{
    /// <summary>
    /// 复杂度评估
    /// </summary>
    public string Complexity { get; set; } = "unknown";

    /// <summary>
    /// 风险评估
    /// </summary>
    public string Risk { get; set; } = "unknown";

    /// <summary>
    /// 受影响的文件
    /// </summary>
    public string[] FilesAffected { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 受影响的引用
    /// </summary>
    public string[] References { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 建议
    /// </summary>
    public string? Suggestion { get; set; }
}

/// <summary>
/// 重构结果
/// </summary>
public class RefactoringResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 重构类型
    /// </summary>
    public string RefactoringType { get; set; } = string.Empty;

    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// 修改的文件数
    /// </summary>
    public int FilesModified { get; set; }

    /// <summary>
    /// 新增的方法数
    /// </summary>
    public int MethodsAdded { get; set; }

    /// <summary>
    /// 修改的方法数
    /// </summary>
    public int MethodsModified { get; set; }

    /// <summary>
    /// 变更列表
    /// </summary>
    public List<CodeChange>? Changes { get; set; }

    /// <summary>
    /// 错误信息（失败时）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 建议（失败时）
    /// </summary>
    public string? Suggestion { get; set; }
}
