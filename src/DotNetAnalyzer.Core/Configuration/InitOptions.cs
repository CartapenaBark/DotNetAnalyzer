namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// Init 命令选项
/// </summary>
public class InitOptions
{
    /// <summary>
    /// 配置范围：project（项目级）| user（用户级）
    /// </summary>
    public string Scope { get; set; } = "project";

    /// <summary>
    /// 输出目录
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// 覆盖现有配置
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// 配置后验证连接
    /// </summary>
    public bool Verify { get; set; } = true;

    /// <summary>
    /// 详细输出
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// 跳过所有提示，使用默认值
    /// </summary>
    public bool Yes { get; set; }

    /// <summary>
    /// 预览将要执行的操作
    /// </summary>
    public bool DryRun { get; set; }
}
