namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// 分析器顶层配置模型，绑定到 Analyzer 配置节。
/// </summary>
public class AnalyzerOptions
{
    /// <summary>
    /// 规则排除和严重级别覆盖配置。
    /// </summary>
    public RulesOptions Rules { get; set; } = new();

    /// <summary>
    /// MVVM 检测相关配置。
    /// </summary>
    public MvvmOptions Mvvm { get; set; } = new();

    /// <summary>
    /// 依赖注入分析相关配置。
    /// </summary>
    public DiOptions Di { get; set; } = new();

    /// <summary>
    /// 检测阈值配置。
    /// </summary>
    public ThresholdsOptions Thresholds { get; set; } = new();
}

/// <summary>
/// 规则排除和严重级别覆盖配置。
/// </summary>
public class RulesOptions
{
    /// <summary>
    /// 要排除的规则 ID 列表（如 ["MVVM001", "MEM002"]）。
    /// </summary>
    public string[] Exclude { get; set; } = [];

    /// <summary>
    /// 按规则 ID 覆盖严重级别（如 { "MEM002": "Info" }）。
    /// </summary>
    public Dictionary<string, string> Severity { get; set; } = new();
}

/// <summary>
/// MVVM 检测相关配置。
/// </summary>
public class MvvmOptions
{
    /// <summary>
    /// ViewModel 命名后缀列表，默认 ["ViewModel"]。
    /// </summary>
    public string[] ViewModelSuffixes { get; set; } = ["ViewModel"];

    /// <summary>
    /// 额外的 UI 命名空间列表（如 ["System.Windows.Controls"]）。
    /// </summary>
    public string[] AdditionalUiNamespaces { get; set; } = [];

    /// <summary>
    /// 要排除的业务逻辑指示器关键词（如 ["async", "Task.Run"]）。
    /// </summary>
    public string[] ExcludedBusinessIndicators { get; set; } = [];
}

/// <summary>
/// 依赖注入分析相关配置。
/// </summary>
public class DiOptions
{
    /// <summary>
    /// 是否启用 Captive Dependency 检测（DI004），默认 true。
    /// </summary>
    public bool CaptiveDependency { get; set; } = true;
}

/// <summary>
/// 检测阈值配置。
/// </summary>
public class ThresholdsOptions
{
    /// <summary>
    /// 圈复杂度阈值，默认 15。
    /// </summary>
    public int MaxCyclomaticComplexity { get; set; } = 15;

    /// <summary>
    /// 方法最大行数阈值，默认 50。
    /// </summary>
    public int MaxMethodLines { get; set; } = 50;

    /// <summary>
    /// 类最大行数阈值，默认 500。
    /// </summary>
    public int MaxClassLines { get; set; } = 500;
}
