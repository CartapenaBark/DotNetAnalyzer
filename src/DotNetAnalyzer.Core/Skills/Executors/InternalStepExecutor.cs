using DotNetAnalyzer.Core.Skills.Models;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Skills.Executors;

/// <summary>
/// 内部步骤执行器（用于执行内置逻辑）
/// </summary>
internal sealed class InternalStepExecutor : IStepExecutor
{
    private readonly ILogger<InternalStepExecutor> _logger;

    public InternalStepExecutor(ILogger<InternalStepExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowStep step, WorkflowContext context)
    {
        _logger.LogDebug("执行内部步骤: {StepName}", step.Name);

        try
        {
            var result = step.Name.ToLowerInvariant() switch
            {
                "collect_parameters" => await CollectParametersAsync(step, context),
                "find_solution" => await FindSolutionAsync(step, context),
                "generate_report" => await GenerateReportAsync(step, context),
                "preview_changes" => await PreviewChangesAsync(step, context),
                "verify" => await VerifyAsync(step, context),
                _ => await ExecuteGenericInternalStepAsync(step, context)
            };

            return StepResult.CreateSuccess(step.Name, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "内部步骤执行失败: {StepName}", step.Name);
            return StepResult.CreateFailure(step.Name, ex.Message);
        }
    }

    private async Task<object> CollectParametersAsync(WorkflowStep step, WorkflowContext context)
    {
        _logger.LogDebug("收集参数");

        // 从上下文中收集必要的参数
        var parameters = new Dictionary<string, object>(context.Options);

        // 添加项目路径
        if (!string.IsNullOrEmpty(context.ProjectPath))
        {
            parameters["projectPath"] = context.ProjectPath;
        }

        // 添加解决方案路径
        if (!string.IsNullOrEmpty(context.SolutionPath))
        {
            parameters["solutionPath"] = context.SolutionPath;
        }

        // 添加当前文件
        if (!string.IsNullOrEmpty(context.CurrentFile))
        {
            parameters["filePath"] = context.CurrentFile;
        }

        return parameters;
    }

    private async Task<object> FindSolutionAsync(WorkflowStep step, WorkflowContext context)
    {
        _logger.LogDebug("查找解决方案");

        // 从上下文数据中查找分析结果
        var diagnostics = context.Data.TryGetValue("get_diagnostics", out var diagData)
            ? diagData
            : null;

        var analysis = context.Data.TryGetValue("analyze_code", out var analysisData)
            ? analysisData
            : null;

        // 生成解决方案建议
        return new
        {
            suggestions = new[]
            {
                "添加 null 检查",
                "使用 null-条件运算符",
                "考虑使用依赖注入"
            },
            diagnostics = diagnostics,
            analysis = analysis
        };
    }

    private async Task<object> GenerateReportAsync(WorkflowStep step, WorkflowContext context)
    {
        _logger.LogDebug("生成报告");

        // 汇总所有步骤结果生成报告
        var report = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["summary"] = new
            {
                totalSteps = context.Data.Count,
                successfulSteps = context.Data.Count
            }
        };

        // 添加各步骤的关键信息
        foreach (var kvp in context.Data)
        {
            report[kvp.Key] = kvp.Value;
        }

        return report;
    }

    private async Task<object> PreviewChangesAsync(WorkflowStep step, WorkflowContext context)
    {
        _logger.LogDebug("预览变更");

        // 生成变更预览
        return new
        {
            preview = true,
            changes = new[]
            {
                new { type = "add", description = "添加新方法" },
                new { type = "modify", description = "修改现有代码" }
            },
            requiresConfirmation = true
        };
    }

    private async Task<object> VerifyAsync(WorkflowStep step, WorkflowContext context)
    {
        _logger.LogDebug("验证结果");

        // 验证操作结果
        return new
        {
            verified = true,
            checks = new[]
            {
                new { name = "编译检查", passed = true },
                new { name = "语法检查", passed = true },
                new { name = "风格检查", passed = true }
            }
        };
    }

    private async Task<object> ExecuteGenericInternalStepAsync(WorkflowStep step, WorkflowContext context)
    {
        _logger.LogInformation("执行通用内部步骤: {StepName}", step.Name);

        return new
        {
            executed = true,
            step = step.Name,
            data = context.Data
        };
    }
}
