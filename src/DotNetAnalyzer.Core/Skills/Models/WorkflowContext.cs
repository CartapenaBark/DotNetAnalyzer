namespace DotNetAnalyzer.Core.Skills.Models;

/// <summary>
/// 工作流执行上下文
/// </summary>
public class WorkflowContext
{
    /// <summary>
    /// 项目路径
    /// </summary>
    public string? ProjectPath { get; set; }

    /// <summary>
    /// 解决方案路径
    /// </summary>
    public string? SolutionPath { get; set; }

    /// <summary>
    /// 当前文件路径
    /// </summary>
    public string? CurrentFile { get; set; }

    /// <summary>
    /// 用户输入
    /// </summary>
    public string? UserInput { get; set; }

    /// <summary>
    /// 步骤间传递的数据
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// 选项和配置
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; set; }
}
