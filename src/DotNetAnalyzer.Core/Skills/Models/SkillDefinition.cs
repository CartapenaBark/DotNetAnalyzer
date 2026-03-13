namespace DotNetAnalyzer.Core.Skills.Models;

/// <summary>
/// Skill 定义模型
/// </summary>
public class SkillDefinition
{
    /// <summary>
    /// Skill 名称（唯一标识符）
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Skill 描述
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 分类
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 触发关键词
    /// </summary>
    public SkillTriggers Triggers { get; set; } = new();

    /// <summary>
    /// MCP 工具列表
    /// </summary>
    public string[] McpTools { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 工作流定义
    /// </summary>
    public SkillWorkflow Workflow { get; set; } = new();

    /// <summary>
    /// 输出定义
    /// </summary>
    public SkillOutput[] Outputs { get; set; } = Array.Empty<SkillOutput>();
}

/// <summary>
/// Skill 触发器定义
/// </summary>
public class SkillTriggers
{
    /// <summary>
    /// 关键词列表
    /// </summary>
    public string[] Keywords { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 上下文要求
    /// </summary>
    public string[] Contexts { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 前置条件
    /// </summary>
    public string[] Requires { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Skill 工作流定义
/// </summary>
public class SkillWorkflow
{
    /// <summary>
    /// 工作流步骤
    /// </summary>
    public List<WorkflowStep> Steps { get; set; } = new();
}

/// <summary>
/// Skill 输出定义
/// </summary>
public class SkillOutput
{
    /// <summary>
    /// 输出格式（markdown、json、html 等）
    /// </summary>
    public required string Format { get; set; }

    /// <summary>
    /// 模板文件路径
    /// </summary>
    public string? Template { get; set; }

    /// <summary>
    /// JSON Schema 文件路径
    /// </summary>
    public string? Schema { get; set; }
}
