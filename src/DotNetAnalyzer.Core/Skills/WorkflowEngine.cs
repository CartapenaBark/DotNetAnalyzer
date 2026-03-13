using DotNetAnalyzer.Core.Skills.Models;
using DotNetAnalyzer.Core.Skills.Executors;
using DotNetAnalyzer.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DotNetAnalyzer.Core.Skills;

/// <summary>
/// Skill 工作流引擎，负责执行 Skill 定义的多步骤工作流
/// </summary>
/// <remarks>
/// 此类提供以下功能：
/// <list type="bullet">
///   <item>执行 Skill 定义的工作流步骤</item>
///   <item>管理步骤间的上下文数据传递</item>
///   <item>支持不同类型的步骤执行器（auto、internal、mcp）</item>
///   <item>处理步骤依赖关系和条件执行</item>
///   <item>生成结构化的工作流执行结果</item>
/// </list>
/// </remarks>
public class WorkflowEngine
{
    private readonly IWorkspaceManager _workspaceManager;
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, IStepExecutor> _executors;

    /// <summary>
    /// 初始化 <see cref="WorkflowEngine"/> 类的新实例
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="loggerFactory">日志工厂</param>
    public WorkflowEngine(
        IWorkspaceManager workspaceManager,
        ILogger<WorkflowEngine> logger,
        ILoggerFactory loggerFactory)
    {
        _workspaceManager = workspaceManager;
        _logger = logger;
        _loggerFactory = loggerFactory;

        // 注册步骤执行器
        _executors = new Dictionary<string, IStepExecutor>(StringComparer.OrdinalIgnoreCase)
        {
            ["auto"] = new AutoStepExecutor(_loggerFactory.CreateLogger<AutoStepExecutor>()),
            ["internal"] = new InternalStepExecutor(_loggerFactory.CreateLogger<InternalStepExecutor>()),
            ["mcp"] = new McpStepExecutor(workspaceManager, _loggerFactory.CreateLogger<McpStepExecutor>())
        };
    }

    /// <summary>
    /// 执行 Skill 工作流
    /// </summary>
    /// <param name="skill">Skill 定义</param>
    /// <param name="context">工作流上下文</param>
    /// <returns>工作流执行结果</returns>
    /// <exception cref="ArgumentNullException">
    /// skill 或 context 为 null
    /// </exception>
    public async Task<WorkflowResult> ExecuteAsync(
        SkillDefinition skill,
        WorkflowContext context)
    {
        if (skill == null)
        {
            throw new ArgumentNullException(nameof(skill));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new WorkflowResult();

        _logger.LogInformation("开始执行工作流: {SkillName}", skill.DisplayName);

        try
        {
            // 执行工作流步骤
            foreach (var step in skill.Workflow.Steps)
            {
                // 检查取消请求
                if (context.CancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("工作流执行被取消: {SkillName}", skill.DisplayName);
                    break;
                }

                // 检查条件
                if (!ShouldExecuteStep(step, context))
                {
                    _logger.LogDebug("跳过步骤: {StepName} (条件不满足)", step.Name);
                    continue;
                }

                // 检查依赖
                if (!await ValidateDependenciesAsync(step, result))
                {
                    _logger.LogWarning("跳过步骤: {StepName} (依赖未满足)", step.Name);
                    continue;
                }

                _logger.LogDebug("执行步骤: {StepName} - {Description}", step.Name, step.Description);

                var stepStopwatch = Stopwatch.StartNew();
                StepResult stepResult;

                try
                {
                    stepResult = await ExecuteStepAsync(step, context);
                    stepStopwatch.Stop();
                    stepResult.Duration = stepStopwatch.Elapsed;

                    result.Steps.Add(stepResult);

                    if (!stepResult.Success && step.Required)
                    {
                        _logger.LogError("必需步骤失败: {StepName} - {Error}", step.Name, stepResult.Error);
                        break;
                    }

                    // 将步骤结果传递给下一步
                    if (stepResult.Data != null)
                    {
                        context.Data[step.Name] = stepResult.Data;
                    }
                }
                catch (Exception ex)
                {
                    stepStopwatch.Stop();
                    _logger.LogError(ex, "步骤执行异常: {StepName}", step.Name);

                    stepResult = StepResult.CreateFailure(
                        step.Name,
                        ex.Message,
                        stepStopwatch.Elapsed);

                    result.Steps.Add(stepResult);

                    if (step.Required)
                    {
                        break;
                    }
                }
            }

            // 生成输出
            result.Output = await GenerateOutputAsync(skill, context, result);
            stopwatch.Stop();
            result.TotalDuration = stopwatch.Elapsed;

            _logger.LogInformation(
                "工作流执行完成: {SkillName} - 耗时: {Duration}ms, 成功步骤: {SuccessCount}/{TotalCount}",
                skill.DisplayName,
                stopwatch.ElapsedMilliseconds,
                result.SuccessfulSteps.Count(),
                result.Steps.Count);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "工作流执行失败: {SkillName}", skill.DisplayName);
            throw;
        }
    }

    /// <summary>
    /// 执行单个工作流步骤
    /// </summary>
    private async Task<StepResult> ExecuteStepAsync(
        WorkflowStep step,
        WorkflowContext context)
    {
        if (_executors.TryGetValue(step.Tool, out var executor))
        {
            return await executor.ExecuteAsync(step, context);
        }

        return StepResult.CreateFailure(step.Name, $"未找到步骤执行器: {step.Tool}");
    }

    /// <summary>
    /// 检查步骤是否应该执行（条件判断）
    /// </summary>
    private bool ShouldExecuteStep(WorkflowStep step, WorkflowContext context)
    {
        if (string.IsNullOrEmpty(step.Condition))
        {
            return true;
        }

        // 简单的条件表达式解析
        // 支持格式: "key == value" 或 "key exists"
        try
        {
            var condition = step.Condition.Trim();

            if (condition.EndsWith(" exists", StringComparison.OrdinalIgnoreCase))
            {
                var key = condition.Replace(" exists", "", StringComparison.OrdinalIgnoreCase).Trim();
                return context.Data.ContainsKey(key);
            }

            // 支持更多条件表达式...
            return true;
        }
        catch
        {
            return true; // 条件解析失败时默认执行
        }
    }

    /// <summary>
    /// 验证步骤依赖是否满足
    /// </summary>
    private async Task<bool> ValidateDependenciesAsync(WorkflowStep step, WorkflowResult result)
    {
        if (step.DependsOn == null || step.DependsOn.Length == 0)
        {
            return true;
        }

        foreach (var dependency in step.DependsOn)
        {
            var dependencyStep = result.Steps.FirstOrDefault(s => s.StepName == dependency);
            if (dependencyStep == null)
            {
                _logger.LogWarning("依赖步骤不存在: {Dependency}", dependency);
                return false;
            }

            if (!dependencyStep.Success)
            {
                _logger.LogWarning("依赖步骤未成功执行: {Dependency}", dependency);
                return false;
            }
        }

        return await Task.FromResult(true);
    }

    /// <summary>
    /// 生成工作流输出
    /// </summary>
    private async Task<object> GenerateOutputAsync(
        SkillDefinition skill,
        WorkflowContext context,
        WorkflowResult result)
    {
        // 默认输出：返回所有步骤结果
        return new
        {
            skill = skill.Name,
            success = result.Success,
            steps = result.Steps.Select(s => new
            {
                name = s.StepName,
                success = s.Success,
                data = s.Data,
                error = s.Error
            }),
            duration = result.TotalDuration.TotalMilliseconds
        };
    }
}
