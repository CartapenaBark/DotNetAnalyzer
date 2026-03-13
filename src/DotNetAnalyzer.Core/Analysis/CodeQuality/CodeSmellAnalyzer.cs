using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using DotNetAnalyzer.Core.Models.CodeQuality;
using System.Collections.Concurrent;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality;

/// <summary>
/// 代码异味分析器主协调器
/// </summary>
/// <remarks>
/// 负责协调所有代码异味检测器，管理分析流程，聚合分析结果。
/// 使用策略模式，可以动态添加或移除检测器。
/// </remarks>
public class CodeSmellAnalyzer
{
    private readonly ILogger<CodeSmellAnalyzer> _logger;
    private readonly IEnumerable<ICodeSmellDetector> _detectors;
    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    /// 初始化 <see cref="CodeSmellAnalyzer"/> 的新实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="detectors">代码异味检测器集合</param>
    public CodeSmellAnalyzer(
        ILogger<CodeSmellAnalyzer> logger,
        IEnumerable<ICodeSmellDetector> detectors)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _detectors = detectors ?? throw new ArgumentNullException(nameof(detectors));
        _semaphore = new SemaphoreSlim(Environment.ProcessorCount);
    }

    /// <summary>
    /// 分析项目中的所有代码异味
    /// </summary>
    /// <param name="project">要分析的项目</param>
    /// <param name="options">分析选项（可为 null，使用默认选项）</param>
    /// <param name="progress">进度报告回调（可为 null）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>代码异味集合</returns>
    public async Task<CodeSmellCollection> AnalyzeAsync(
        Project project,
        CodeAnalysisOptions? options = null,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CodeAnalysisOptions();

        _logger.LogInformation("开始分析项目: {ProjectPath}", project.FilePath);
        var startTime = DateTime.UtcNow;

        var result = new ConcurrentBag<CodeSmell>();
        var documents = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs") == true)
            .ToList();

        var totalDocuments = documents.Count;
        var completedDocuments = 0;

        var analysisTasks = documents.Select(async doc =>
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                var smells = await AnalyzeDocumentAsync(doc, options, cancellationToken);

                foreach (var smell in smells)
                {
                    if (smell.Severity >= options.MinSeverity)
                    {
                        result.Add(smell);
                    }
                }

                Interlocked.Increment(ref completedDocuments);

                progress?.Report(new AnalysisProgress
                {
                    CompletedDocuments = completedDocuments,
                    TotalDocuments = totalDocuments,
                    CurrentFile = doc.FilePath ?? "",
                    Percentage = (int)((double)completedDocuments / totalDocuments * 100)
                });
            }
            finally
            {
                _semaphore.Release();
            }
        });

        await Task.WhenAll(analysisTasks);

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "分析完成: {ProjectPath}, 耗时: {Duration}s, 发现 {Count} 个代码异味",
            project.FilePath,
            duration.TotalSeconds,
            result.Count);

        return new CodeSmellCollection { Smells = result.ToList() };
    }

    /// <summary>
    /// 分析单个文档中的代码异味
    /// </summary>
    /// <param name="document">要分析的文档</param>
    /// <param name="options">分析选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检测到的代码异味列表</returns>
    public async Task<IReadOnlyList<CodeSmell>> AnalyzeDocumentAsync(
        Document document,
        CodeAnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CodeAnalysisOptions();

        var result = new List<CodeSmell>();

        foreach (var detector in _detectors)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (!detector.SupportsOptions(options))
                {
                    continue;
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(options.TimeoutMilliseconds);

                try
                {
                    var smells = await detector.DetectAsync(document, options);

                    foreach (var smell in smells)
                    {
                        if (smell.Severity >= options.MinSeverity)
                        {
                            result.Add(smell);
                        }
                    }
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "检测器 {DetectorName} 在分析 {Document} 时超时",
                        detector.Name,
                        document.FilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "检测器 {DetectorName} 在分析 {Document} 时失败",
                    detector.Name,
                    document.FilePath);
            }
        }

        return result;
    }

    /// <summary>
    /// 分析指定文档中的特定类型代码异味
    /// </summary>
    /// <param name="document">要分析的文档</param>
    /// <param name="smellType">代码异味类型</param>
    /// <param name="options">分析选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检测到的代码异味列表</returns>
    public async Task<IReadOnlyList<CodeSmell>> AnalyzeSpecificSmellAsync(
        Document document,
        string smellType,
        CodeAnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CodeAnalysisOptions();

        var detector = _detectors.FirstOrDefault(d =>
            d.Name.Equals(smellType, StringComparison.OrdinalIgnoreCase));

        if (detector == null)
        {
            _logger.LogWarning("未找到代码异味检测器: {SmellType}", smellType);
            return Array.Empty<CodeSmell>();
        }

        try
        {
            var smells = await detector.DetectAsync(document, options);
            return smells.Where(s => s.Severity >= options.MinSeverity).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "检测器 {DetectorName} 在分析 {Document} 时失败",
                detector.Name,
                document.FilePath);
            return Array.Empty<CodeSmell>();
        }
    }

    /// <summary>
    /// 获取所有已注册的检测器
    /// </summary>
    public IReadOnlyList<ICodeSmellDetector> GetDetectors()
    {
        return _detectors.ToList().AsReadOnly();
    }

    /// <summary>
    /// 获取指定类型的检测器
    /// </summary>
    public ICodeSmellDetector? GetDetector(string smellType)
    {
        return _detectors.FirstOrDefault(d =>
            d.Name.Equals(smellType, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// 分析进度信息
/// </summary>
public class AnalysisProgress
{
    /// <summary>
    /// 已完成的文档数
    /// </summary>
    public int CompletedDocuments { get; set; }

    /// <summary>
    /// 总文档数
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// 当前处理的文件
    /// </summary>
    public required string CurrentFile { get; set; }

    /// <summary>
    /// 完成百分比（0-100）
    /// </summary>
    public int Percentage { get; set; }
}
