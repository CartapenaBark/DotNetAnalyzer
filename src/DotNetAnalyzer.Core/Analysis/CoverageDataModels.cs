namespace DotNetAnalyzer.Core.Analysis;

/// <summary>
/// Cobertura XML 覆盖率数据的顶层模型，包含整体统计和每个文件/方法的明细。
/// </summary>
public class CoverageData
{
    /// <summary>
    /// 整体行覆盖率（0.0 ~ 1.0）。
    /// </summary>
    public double LineRate { get; set; }

    /// <summary>
    /// 整体分支覆盖率（0.0 ~ 1.0）。
    /// </summary>
    public double BranchRate { get; set; }

    /// <summary>
    /// 每个文件的覆盖率明细。
    /// </summary>
    public List<FileCoverageData> Files { get; set; } = [];
}

/// <summary>
/// 单个文件的覆盖率数据，包含行/分支覆盖率与方法级明细。
/// </summary>
public class FileCoverageData
{
    /// <summary>
    /// 文件名（来自 Cobertura class/@filename）。
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 该文件的行覆盖率（0.0 ~ 1.0）。
    /// </summary>
    public double LineRate { get; set; }

    /// <summary>
    /// 该文件的分支覆盖率（0.0 ~ 1.0）。
    /// </summary>
    public double BranchRate { get; set; }

    /// <summary>
    /// 该文件中被命中的行总数。
    /// </summary>
    public int CoveredLines { get; set; }

    /// <summary>
    /// 该文件中可执行行总数。
    /// </summary>
    public int TotalLines { get; set; }

    /// <summary>
    /// 方法级覆盖率明细。
    /// </summary>
    public List<MethodCoverageData> Methods { get; set; } = [];
}

/// <summary>
/// 单个方法的覆盖率数据。
/// </summary>
public class MethodCoverageData
{
    /// <summary>
    /// 方法名。
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// 方法行覆盖率（0.0 ~ 1.0）。
    /// </summary>
    public double LineRate { get; set; }

    /// <summary>
    /// 方法分支覆盖率（0.0 ~ 1.0）。
    /// </summary>
    public double BranchRate { get; set; }

    /// <summary>
    /// 方法中被命中的行数。
    /// </summary>
    public int CoveredLines { get; set; }

    /// <summary>
    /// 方法中可执行行总数。
    /// </summary>
    public int TotalLines { get; set; }
}
