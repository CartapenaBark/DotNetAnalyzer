namespace DotNetAnalyzer.Core.Skills.Models;

/// <summary>
/// 工作流步骤定义
/// </summary>
public class WorkflowStep
{
    /// <summary>
    /// 步骤名称（唯一标识符）
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 步骤描述
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// 工具类型：auto、internal、mcp
    /// </summary>
    public required string Tool { get; set; }

    /// <summary>
    /// 步骤参数（可选）
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// 是否必需（失败时是否继续）
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// 依赖的前置步骤名称（可选）
    /// </summary>
    public string[]? DependsOn { get; set; }

    /// <summary>
    /// 条件执行表达式（可选）
    /// </summary>
    public string? Condition { get; set; }
}
