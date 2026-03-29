using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using DotNetAnalyzer.Core.Security.Models;
using System.Collections.Concurrent;

namespace DotNetAnalyzer.Core.Security;

/// <summary>
/// 安全分析引擎 — 协调所有安全检测器，使用与 CodeSmellAnalyzer 相同的并行文档扫描模式
/// </summary>
public partial class SecurityAnalysisEngine
{
    private readonly ILogger<SecurityAnalysisEngine> _logger;
    private readonly IEnumerable<ISecurityDetector> _detectors;
    private readonly SemaphoreSlim _semaphore;

    [LoggerMessage(
        LogLevel.Information,
        "开始安全扫描: {ProjectPath}")]
    private static partial void LogScanStarted(
        ILogger logger, string projectPath);

    [LoggerMessage(
        LogLevel.Information,
        "安全扫描完成: {ProjectPath}, 耗时: {Duration}s, 发现 {Count} 个安全问题")]
    private static partial void LogScanCompleted(
        ILogger logger, string projectPath, double duration, int count);

    [LoggerMessage(
        LogLevel.Warning,
        "安全检测器 {DetectorName} 在分析 {Document} 时超时")]
    private static partial void LogDetectorTimeout(
        ILogger logger, string detectorName, string document);

    [LoggerMessage(
        LogLevel.Error,
        "安全检测器 {DetectorName} 在分析 {Document} 时失败")]
    private static partial void LogDetectorFailed(
        ILogger logger, Exception ex, string detectorName, string document);

    /// <summary>
    /// 初始化 <see cref="SecurityAnalysisEngine"/> 的新实例
    /// </summary>
    public SecurityAnalysisEngine(
        ILogger<SecurityAnalysisEngine> logger,
        IEnumerable<ISecurityDetector> detectors)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _detectors = detectors ?? throw new ArgumentNullException(nameof(detectors));
        _semaphore = new SemaphoreSlim(Environment.ProcessorCount);
    }

    /// <summary>
    /// 扫描项目中的所有安全漏洞
    /// </summary>
    public async Task<SecurityReport> AnalyzeAsync(
        Project project,
        SecurityAnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SecurityAnalysisOptions();

        LogScanStarted(_logger, project.FilePath ?? string.Empty);
        var startTime = DateTime.UtcNow;

        var findings = new ConcurrentBag<SecurityFinding>();
        var documents = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs") == true)
            .ToList();

        var scannedFiles = documents.Count;

        var analysisTasks = documents.Select(async doc =>
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                var docFindings = await AnalyzeDocumentAsync(
                    doc, options, cancellationToken);

                foreach (var finding in docFindings)
                {
                    if (finding.Severity >= options.MinSeverity)
                    {
                        findings.Add(finding);
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        });

        await Task.WhenAll(analysisTasks);

        var duration = DateTime.UtcNow - startTime;
        LogScanCompleted(
            _logger, project.FilePath ?? string.Empty,
            duration.TotalSeconds, findings.Count);

        return new SecurityReport
        {
            ProjectPath = project.FilePath ?? string.Empty,
            Findings = findings.ToList(),
            DurationMs = (long)duration.TotalMilliseconds,
            ScannedFiles = scannedFiles
        };
    }

    /// <summary>
    /// 分析单个文档中的安全漏洞
    /// </summary>
    public async Task<IReadOnlyList<SecurityFinding>> AnalyzeDocumentAsync(
        Document document,
        SecurityAnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SecurityAnalysisOptions();

        var result = new List<SecurityFinding>();

        foreach (var detector in _detectors)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // 检查排除规则
            if (options.ExcludedRules.Contains(detector.RuleId))
            {
                continue;
            }

            // 检查包含规则
            if (options.IncludedRules != null &&
                !options.IncludedRules.Contains(detector.RuleId))
            {
                continue;
            }

            try
            {
                using var cts = CancellationTokenSource
                    .CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(options.TimeoutMilliseconds);

                try
                {
                    var findings = await detector.DetectAsync(
                        document, options, cts.Token);

                    foreach (var finding in findings)
                    {
                        if (finding.Severity >= options.MinSeverity)
                        {
                            result.Add(finding);
                        }
                    }
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    LogDetectorTimeout(
                        _logger, detector.Name,
                        document.FilePath ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                LogDetectorFailed(
                    _logger, ex, detector.Name,
                    document.FilePath ?? string.Empty);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取所有已注册的安全规则信息
    /// </summary>
    public IReadOnlyList<SecurityRuleInfo> GetRules()
    {
        return _detectors.Select(d => new SecurityRuleInfo
        {
            RuleId = d.RuleId,
            Name = d.Name,
            Description = d.Description,
            OwaspCategory = d.OwaspCategory,
            CweId = d.CweId,
            DefaultSeverity = d.DefaultSeverity
        }).ToList();
    }
}

/// <summary>
/// 安全规则信息
/// </summary>
public sealed class SecurityRuleInfo
{
    public required string RuleId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string OwaspCategory { get; init; }
    public required string CweId { get; init; }
    public required SecuritySeverity DefaultSeverity { get; init; }
}
