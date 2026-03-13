using DotNetAnalyzer.Core.Skills.Models;
using DotNetAnalyzer.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace DotNetAnalyzer.Core.Skills.Executors;

/// <summary>
/// MCP 工具步骤执行器
/// </summary>
internal sealed class McpStepExecutor : IStepExecutor
{
    private readonly IWorkspaceManager _workspaceManager;
    private readonly ILogger<McpStepExecutor> _logger;

    public McpStepExecutor(IWorkspaceManager workspaceManager, ILogger<McpStepExecutor> logger)
    {
        _workspaceManager = workspaceManager;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowStep step, WorkflowContext context)
    {
        _logger.LogDebug("执行 MCP 工具步骤: {StepName}, Tool: {Tool}", step.Name, step.Tool);

        try
        {
            // MCP 工具名称存储在 step.Tool 中（如 "get_diagnostics"）
            var toolName = step.Tool;

            _logger.LogInformation("调用 MCP 工具: {ToolName}", toolName);

            // 执行 MCP 工具
            var result = await ExecuteMcpToolAsync(toolName, step, context);

            return StepResult.CreateSuccess(step.Name, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP 工具执行失败: {StepName}", step.Name);
            return StepResult.CreateFailure(step.Name, ex.Message);
        }
    }

    private async Task<object> ExecuteMcpToolAsync(
        string toolName,
        WorkflowStep step,
        WorkflowContext context)
    {
        // 解析工具参数
        var parameters = ResolveParameters(step.Parameters, context);

        _logger.LogDebug("工具参数: {Parameters}", System.Text.Json.JsonSerializer.Serialize(parameters));

        // 根据工具名称调用相应的 Roslyn 分析器
        // 注意：这里提供的是一个简化的实现
        // 实际实现需要根据工具名称调用相应的 MCP 工具

        var result = toolName.ToLowerInvariant() switch
        {
            "get_diagnostics" => await GetDiagnosticsAsync(parameters),
            "analyze_code" => await AnalyzeCodeAsync(parameters),
            "get_code_metrics" => await GetCodeMetricsAsync(parameters),
            "find_dead_code" => await FindDeadCodeAsync(parameters),
            "analyze_performance" => await AnalyzePerformanceAsync(parameters),
            "find_references" => await FindReferencesAsync(parameters),
            "go_to_definition" => await GoToDefinitionAsync(parameters),
            "extract_method" => await ExtractMethodAsync(parameters),
            "rename_symbol" => await RenameSymbolAsync(parameters),
            _ => await ExecuteUnknownToolAsync(toolName, parameters)
        };

        return result;
    }

    private async Task<object> GetDiagnosticsAsync(Dictionary<string, object> parameters)
    {
        _logger.LogDebug("获取诊断信息");

        // 占位实现：返回模拟数据
        return new
        {
            errors = Array.Empty<object>(),
            warnings = new[]
            {
                new { file = "Sample.cs", line = 10, message = "未使用的变量", code = "CS0169" }
            },
            info = Array.Empty<object>()
        };
    }

    private async Task<object> AnalyzeCodeAsync(Dictionary<string, object> parameters)
    {
        _logger.LogDebug("分析代码结构");

        return new
        {
            syntaxTree = new
            {
                nodes = new[]
                {
                    new { type = "ClassDeclaration", name = "SampleClass" },
                    new { type = "MethodDeclaration", name = "DoWork" }
                }
            },
            symbols = new
            {
                classes = new[] { "SampleClass" },
                methods = new[] { "DoWork" }
            }
        };
    }

    private async Task<object> GetCodeMetricsAsync(Dictionary<string, object> parameters)
    {
        _logger.LogDebug("获取代码度量");

        return new
        {
            complexity = 3.2,
            maintainability = 72,
            duplication = 15,
            coverage = 45
        };
    }

    private async Task<object> FindDeadCodeAsync(Dictionary<string, object> parameters)
    {
        _logger.LogDebug("查找死代码");

        return new
        {
            unusedMethods = new[]
            {
                new { name = "OldMethod", file = "Legacy.cs", line = 42 }
            },
            unusedClasses = Array.Empty<object>(),
            unusedFields = Array.Empty<object>()
        };
    }

    private async Task<object> AnalyzePerformanceAsync(Dictionary<string, object> parameters)
    {
        _logger.LogDebug("分析性能");

        return new
        {
            issues = new[]
            {
                new
                {
                    type = "StringConcatenation",
                    severity = "Warning",
                    location = "Utils.cs:25",
                    description = "使用 StringBuilder 替代字符串连接"
                }
            }
        };
    }

    private async Task<object> FindReferencesAsync(Dictionary<string, object> parameters)
    {
        _logger.LogDebug("查找引用");

        return new
        {
            symbol = "MyMethod",
            references = new[]
            {
                new { file = "Caller1.cs", line = 15 },
                new { file = "Caller2.cs", line = 23 }
            }
        };
    }

    private async Task<object> GoToDefinitionAsync(Dictionary<string, object> parameters)
    {
        _logger.LogDebug("跳转到定义");

        return new
        {
            symbol = "MyMethod",
            definition = new { file = "Definition.cs", line = 10, column = 5 }
        };
    }

    private async Task<object> ExtractMethodAsync(Dictionary<string, object> parameters)
    {
        _logger.LogDebug("提取方法");

        return new
        {
            preview = new
            {
                methodName = "ExtractedMethod",
                parameters = new[] { "value" },
                returnType = "void"
            }
        };
    }

    private async Task<object> RenameSymbolAsync(Dictionary<string, object> parameters)
    {
        _logger.LogDebug("重命名符号");

        return new
        {
            preview = new
            {
                oldName = "oldName",
                newName = "newName",
                affectedFiles = new[] { "File1.cs", "File2.cs" }
            }
        };
    }

    private async Task<object> ExecuteUnknownToolAsync(string toolName, Dictionary<string, object> parameters)
    {
        _logger.LogWarning("未知的 MCP 工具: {ToolName}", toolName);

        return new
        {
            tool = toolName,
            parameters = parameters,
            message = "工具已调用（占位实现）"
        };
    }

    private Dictionary<string, object> ResolveParameters(
        Dictionary<string, object>? parameters,
        WorkflowContext context)
    {
        var resolved = new Dictionary<string, object>();

        if (parameters == null)
        {
            return resolved;
        }

        foreach (var kvp in parameters)
        {
            resolved[kvp.Key] = McpStepExecutor.ResolveParameterValue(kvp.Value, context);
        }

        return resolved;
    }

    private static object ResolveParameterValue(object value, WorkflowContext context)
    {
        // 如果值是字符串，检查是否需要替换占位符
        if (value is string strValue)
        {
            // 替换 {{projectPath}} 等占位符
            return strValue
                .Replace("{{projectPath}}", context.ProjectPath ?? string.Empty)
                .Replace("{{solutionPath}}", context.SolutionPath ?? string.Empty)
                .Replace("{{currentFile}}", context.CurrentFile ?? string.Empty)
                .Replace("{{userInput}}", context.UserInput ?? string.Empty);
        }

        return value;
    }
}
