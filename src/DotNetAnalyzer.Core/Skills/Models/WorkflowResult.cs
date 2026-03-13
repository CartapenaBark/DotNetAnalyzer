namespace DotNetAnalyzer.Core.Skills.Models;

/// <summary>
/// 工作流执行结果
/// </summary>
public class WorkflowResult
{
    /// <summary>
    /// 是否成功（所有必需步骤成功）
    /// </summary>
    public bool Success => Steps.All(s => s.Success);

    /// <summary>
    /// 所有步骤结果
    /// </summary>
    public List<StepResult> Steps { get; set; } = new();

    /// <summary>
    /// 最终输出
    /// </summary>
    public object? Output { get; set; }

    /// <summary>
    /// 总执行时长
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// 执行时间戳
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 获取失败的步骤
    /// </summary>
    public IEnumerable<StepResult> FailedSteps => Steps.Where(s => !s.Success);

    /// <summary>
    /// 获取成功的步骤
    /// </summary>
    public IEnumerable<StepResult> SuccessfulSteps => Steps.Where(s => s.Success);
}
