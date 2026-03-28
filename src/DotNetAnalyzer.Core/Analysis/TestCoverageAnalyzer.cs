using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Analysis;

/// <summary>
/// 测试覆盖率分析器。
/// 优先从 coverage.cobertura.xml 读取真实覆盖率数据（verified），
/// 当覆盖文件不存在时回退到启发式估算（heuristic）。
/// </summary>
public partial class TestCoverageAnalyzer
{
    private readonly ILogger<TestCoverageAnalyzer> _logger;
    private readonly CoverageDataParser _parser;

    [LoggerMessage(
        LogLevel.Debug,
        "无法确定项目目录: {ProjectPath}")]
    private static partial void LogProjectDirUnknown(
        ILogger logger, string projectPath);

    [LoggerMessage(
        LogLevel.Information,
        "成功解析覆盖率文件: {CoverageFile}")]
    private static partial void LogCoverageFileParsed(
        ILogger logger, string coverageFile);

    [LoggerMessage(
        LogLevel.Information,
        "覆盖率文件未找到，使用启发式估算: {ProjectPath}")]
    private static partial void LogHeuristicFallback(
        ILogger logger, string projectPath);

    /// <summary>
    /// 初始化 <see cref="TestCoverageAnalyzer"/> 的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="parser">Cobertura XML 解析器。</param>
    public TestCoverageAnalyzer(
        ILogger<TestCoverageAnalyzer> logger,
        CoverageDataParser parser)
    {
        _logger = logger;
        _parser = parser;
    }

    /// <summary>
    /// 分析项目的测试覆盖率（向后兼容入口）。
    /// 尝试读取 coverage.cobertura.xml，不存在时回退到启发式估算。
    /// </summary>
    /// <param name="project">要分析的 Roslyn 项目。</param>
    /// <returns>包含覆盖率结果和可信度标记的分析结果。</returns>
    public async Task<TestCoverageResult> AnalyzeAsync(Project project)
    {
        var realResult = await AnalyzeWithCoverageDataAsync(project);
        if (realResult != null)
        {
            return realResult;
        }

        return await AnalyzeHeuristicAsync(project);
    }

    /// <summary>
    /// 尝试从项目的 coverage.cobertura.xml 读取真实覆盖率数据。
    /// </summary>
    /// <param name="project">要分析的 Roslyn 项目。</param>
    /// <returns>
    /// 成功时返回 Credibility = "verified" 的结果；
    /// 覆盖文件不存在时返回 <c>null</c>。
    /// </returns>
    public async Task<TestCoverageResult?> AnalyzeWithCoverageDataAsync(
        Project project)
    {
        // 确定覆盖率文件路径：项目目录下的 coverage.cobertura.xml
        var projectDir = Path.GetDirectoryName(project.FilePath);
        if (string.IsNullOrEmpty(projectDir))
        {
            LogProjectDirUnknown(_logger, project.FilePath ?? string.Empty);
            return null;
        }

        var coverageFilePath = Path.Combine(
            projectDir, "coverage.cobertura.xml");

        var coverageData = _parser.ParseFile(coverageFilePath);
        if (coverageData == null)
        {
            return null;
        }

        LogCoverageFileParsed(_logger, coverageFilePath);

        var fileCoverages = new List<FileCoverage>();

        foreach (var fileData in coverageData.Files)
        {
            fileCoverages.Add(new FileCoverage
            {
                FilePath = fileData.FileName,
                TotalMethods = fileData.Methods.Count,
                CoveredMethods = fileData.Methods.Count(
                    m => m.LineRate > 0),
                CoveragePercentage = fileData.LineRate * 100.0,
                UncoveredMethods = fileData.Methods
                    .Where(m => m.LineRate == 0)
                    .Select(m => m.MethodName)
                    .ToList()
            });
        }

        // 从文档列表中获取非测试文件总数
        var sourceDocuments = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs") == true)
            .ToList();

        var totalUncoveredLines = 0;
        foreach (var fileData in coverageData.Files)
        {
            totalUncoveredLines += fileData.TotalLines -
                fileData.CoveredLines;
        }

        return new TestCoverageResult
        {
            LineCoverage = coverageData.LineRate * 100.0,
            BranchCoverage = coverageData.BranchRate * 100.0,
            MethodCoverage = CalculateMethodCoverage(coverageData),
            TotalFiles = sourceDocuments.Count,
            UncoveredLines = totalUncoveredLines,
            FileCoverages = fileCoverages,
            Credibility = "verified"
        };
    }

    /// <summary>
    /// 基于文件命名的启发式覆盖率估算（回退模式）。
    /// </summary>
    /// <param name="project">要分析的 Roslyn 项目。</param>
    /// <returns>Credibility = "heuristic" 的估算结果。</returns>
    public async Task<TestCoverageResult> AnalyzeHeuristicAsync(Project project)
    {
        LogHeuristicFallback(_logger, project.FilePath ?? string.Empty);

        var result = new TestCoverageResult
        {
            Credibility = "heuristic"
        };

        // 获取所有源代码文件
        var sourceDocuments = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs") == true)
            .ToList();

        // 获取所有测试文件
        var testDocuments = sourceDocuments
            .Where(d => IsTestDocument(d))
            .ToList();

        // 分析覆盖率
        var totalLines = 0;
        var coveredLines = 0;
        var uncoveredLines = 0;
        var fileCoverages = new List<FileCoverage>();

        foreach (var document in sourceDocuments)
        {
            if (document.FilePath == null) continue;

            var tree = await document.GetSyntaxTreeAsync();
            if (tree == null) continue;

            var root = await tree.GetRootAsync();
            var lines = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>();

            var fileCoverage = new FileCoverage
            {
                FilePath = document.FilePath,
                TotalMethods = lines.Count(),
                CoveredMethods = 0,
                UncoveredMethods = new List<string>()
            };

            // 简化实现：假设测试文件覆盖自己
            if (IsTestDocument(document))
            {
                fileCoverage.CoveredMethods = lines.Count();
                fileCoverage.CoveragePercentage = 100.0;
                coveredLines += lines.Count();
            }
            else
            {
                // 检查是否有对应的测试文件
                var hasTest = testDocuments.Any(t =>
                    t.FilePath?.Contains(
                        GetTestNameFromSource(document.FilePath)) == true);

                if (hasTest)
                {
                    fileCoverage.CoveredMethods =
                        (int)(lines.Count() * 0.8); // 假设 80% 覆盖
                    fileCoverage.CoveragePercentage = 80.0;
                    coveredLines += fileCoverage.CoveredMethods;
                    uncoveredLines += lines.Count() -
                        fileCoverage.CoveredMethods;
                }
                else
                {
                    fileCoverage.CoveredMethods = 0;
                    fileCoverage.CoveragePercentage = 0.0;
                    uncoveredLines += lines.Count();
                }
            }

            totalLines += lines.Count();
            fileCoverages.Add(fileCoverage);
        }

        result.FileCoverages = fileCoverages;
        result.LineCoverage = totalLines > 0
            ? (double)coveredLines / totalLines * 100
            : 0;
        result.BranchCoverage = result.LineCoverage * 0.9;
        result.MethodCoverage = totalLines > 0
            ? (double)coveredLines / totalLines * 100
            : 0;
        result.TotalFiles = fileCoverages.Count;
        result.UncoveredLines = uncoveredLines;

        return result;
    }

    /// <summary>
    /// 从覆盖率数据计算方法覆盖率百分比。
    /// </summary>
    private static double CalculateMethodCoverage(CoverageData coverageData)
    {
        var totalMethods = 0;
        var coveredMethods = 0;

        foreach (var file in coverageData.Files)
        {
            foreach (var method in file.Methods)
            {
                totalMethods++;
                if (method.LineRate > 0)
                {
                    coveredMethods++;
                }
            }
        }

        return totalMethods > 0
            ? (double)coveredMethods / totalMethods * 100.0
            : 0.0;
    }

    private static bool IsTestDocument(Document document)
    {
        var filePath = document.FilePath ?? string.Empty;
        return filePath.Contains("Test") ||
               filePath.Contains("Spec") ||
               filePath.Contains(".Tests.");
    }

    private static string GetTestNameFromSource(string sourcePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        return $"{fileName}Tests";
    }
}

/// <summary>
/// 测试覆盖率结果。
/// </summary>
public class TestCoverageResult
{
    /// <summary>
    /// 行覆盖率百分比（0 ~ 100）。
    /// </summary>
    public double LineCoverage { get; set; }

    /// <summary>
    /// 分支覆盖率百分比（0 ~ 100）。
    /// </summary>
    public double BranchCoverage { get; set; }

    /// <summary>
    /// 方法覆盖率百分比（0 ~ 100）。
    /// </summary>
    public double MethodCoverage { get; set; }

    /// <summary>
    /// 分析的文件总数。
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// 未覆盖的行数。
    /// </summary>
    public int UncoveredLines { get; set; }

    /// <summary>
    /// 每个文件的覆盖率明细。
    /// </summary>
    public List<FileCoverage> FileCoverages { get; set; } = [];

    /// <summary>
    /// 结果可信度级别："verified" 或 "heuristic"。
    /// </summary>
    public string Credibility { get; set; } = "heuristic";
}

/// <summary>
/// 单个文件的覆盖率信息。
/// </summary>
public class FileCoverage
{
    /// <summary>
    /// 文件路径。
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 文件中的方法总数。
    /// </summary>
    public int TotalMethods { get; set; }

    /// <summary>
    /// 被覆盖的方法数。
    /// </summary>
    public int CoveredMethods { get; set; }

    /// <summary>
    /// 覆盖率百分比。
    /// </summary>
    public double CoveragePercentage { get; set; }

    /// <summary>
    /// 未覆盖的方法名列表。
    /// </summary>
    public List<string> UncoveredMethods { get; set; } = [];
}
