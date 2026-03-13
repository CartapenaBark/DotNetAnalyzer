namespace DotNetAnalyzer.Core.Skills.Models;

/// <summary>
/// 步骤执行结果
/// </summary>
public class StepResult
{
    /// <summary>
    /// 步骤名称
    /// </summary>
    public required string StepName { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 步骤输出数据
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 执行时长
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// 执行时间戳
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static StepResult CreateSuccess(string stepName, object? data = null, TimeSpan duration = default)
    {
        return new StepResult
        {
            StepName = stepName,
            Success = true,
            Data = data,
            Duration = duration
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static StepResult CreateFailure(string stepName, string error, TimeSpan duration = default)
    {
        return new StepResult
        {
            StepName = stepName,
            Success = false,
            Error = error,
            Duration = duration
        };
    }
}
