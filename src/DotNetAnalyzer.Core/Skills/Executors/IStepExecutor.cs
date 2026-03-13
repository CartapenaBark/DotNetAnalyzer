using DotNetAnalyzer.Core.Skills.Models;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Skills.Executors;

/// <summary>
/// 步骤执行器接口
/// </summary>
public interface IStepExecutor
{
    /// <summary>
    /// 执行工作流步骤
    /// </summary>
    /// <param name="step">步骤定义</param>
    /// <param name="context">工作流上下文</param>
    /// <returns>步骤执行结果</returns>
    Task<StepResult> ExecuteAsync(WorkflowStep step, WorkflowContext context);
}

/// <summary>
/// 自动步骤执行器（用于自动检测项目等场景）
/// </summary>
internal sealed class AutoStepExecutor : IStepExecutor
{
    private readonly ILogger<AutoStepExecutor> _logger;

    public AutoStepExecutor(ILogger<AutoStepExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowStep step, WorkflowContext context)
    {
        _logger.LogDebug("执行自动步骤: {StepName}", step.Name);

        try
        {
            // 根据步骤名称执行相应的自动操作
            var result = step.Name.ToLowerInvariant() switch
            {
                "detect_project" => await DetectProjectAsync(context),
                "identify_refactoring_type" => await IdentifyRefactoringTypeAsync(context),
                "analyze_error_type" => await AnalyzeErrorTypeAsync(context),
                _ => await ExecuteGenericAutoStepAsync(step, context)
            };

            return StepResult.CreateSuccess(step.Name, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自动步骤执行失败: {StepName}", step.Name);
            return StepResult.CreateFailure(step.Name, ex.Message);
        }
    }

    private async Task<object> DetectProjectAsync(WorkflowContext context)
    {
        // 自动检测项目文件
        var currentDir = Directory.GetCurrentDirectory();

        // 查找 .sln 文件
        var solutionFiles = Directory.GetFiles(currentDir, "*.sln")
            .Concat(Directory.GetFiles(currentDir, "*.slnx"));

        if (solutionFiles.Any())
        {
            var solutionPath = solutionFiles.First();
            context.SolutionPath = solutionPath;
            context.ProjectPath = solutionPath;

            return new
            {
                type = "solution",
                path = solutionPath,
                name = Path.GetFileNameWithoutExtension(solutionPath)
            };
        }

        // 查找 .csproj 文件
        var projectFiles = Directory.GetFiles(currentDir, "*.csproj", SearchOption.AllDirectories);
        if (projectFiles.Length > 0)
        {
            var projectPath = projectFiles.First();
            context.ProjectPath = projectPath;

            return new
            {
                type = "project",
                path = projectPath,
                name = Path.GetFileNameWithoutExtension(projectPath)
            };
        }

        throw new InvalidOperationException("未找到 .NET 项目或解决方案文件");
    }

    private async Task<object> IdentifyRefactoringTypeAsync(WorkflowContext context)
    {
        // 识别重构类型（从用户输入）
        var input = context.UserInput ?? string.Empty;

        var refactoringType = input.ToLowerInvariant() switch
        {
            var s when s.Contains("提取") || s.Contains("extract") => "extract_method",
            var s when s.Contains("重命名") || s.Contains("rename") => "rename_symbol",
            var s when s.Contains("接口") || s.Contains("interface") => "extract_interface",
            _ => "unknown"
        };

        return new { type = refactoringType };
    }

    private async Task<object> AnalyzeErrorTypeAsync(WorkflowContext context)
    {
        // 分析错误类型（从用户输入）
        var input = context.UserInput ?? string.Empty;

        var errorType = input.ToLowerInvariant() switch
        {
            var s when s.Contains("null") || s.Contains("空引用") => "NullReferenceException",
            var s when s.Contains("参数") || s.Contains("argument") => "ArgumentException",
            _ => "Exception"
        };

        return new { type = errorType };
    }

    private async Task<object> ExecuteGenericAutoStepAsync(WorkflowStep step, WorkflowContext context)
    {
        _logger.LogInformation("执行通用自动步骤: {StepName}", step.Name);

        return new
        {
            executed = true,
            step = step.Name
        };
    }
}
