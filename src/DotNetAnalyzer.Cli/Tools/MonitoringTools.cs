using System.ComponentModel;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Analysis.CodeQuality;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Monitoring;
using DotNetAnalyzer.Core.Caching;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// 监控和增量分析工具
/// </summary>
[McpServerToolType]
public static class MonitoringTools
{
    private static readonly Dictionary<string, IFileWatcher> _activeWatchers = new();

    /// <summary>
    /// 启动文件监听
    /// </summary>
    /// <remarks>
    /// 监听项目文件变更，自动触发重新分析。
    /// </remarks>
    /// <param name="logger">日志记录器</param>
    /// <param name="projectPath">项目文件路径或目录</param>
    /// <param name="filter">文件过滤器（如 *.cs）</param>
    /// <returns>监听器状态</returns>
    [McpServerTool, Description("启动文件监听以实现增量分析")]
    public static string StartFileWatching(
        ILogger<FileSystemFileWatcher> logger,
        [Description("项目文件路径或目录")] string projectPath,
        [Description("文件过滤器（如 *.cs）")] string filter = "*.cs")
    {
        try
        {
            if (_activeWatchers.ContainsKey(projectPath))
            {
                return JsonSerializer.Serialize(new
                {
                    status = "already_watching",
                    message = $"已在监听路径: {projectPath}"
                }, JsonOptions.Default);
            }

            var watcher = new FileSystemFileWatcher(logger);

            watcher.FileChanged += (sender, args) =>
            {
                // 文件变更事件处理
                Console.WriteLine($"[FileWatcher] 文件变更: {args.FullPath} ({args.ChangeType})");
            };

            watcher.StartWatching(projectPath, filter);
            _activeWatchers[projectPath] = watcher;

            return JsonSerializer.Serialize(new
            {
                status = "started",
                path = projectPath,
                filter = filter,
                message = $"开始监听路径: {projectPath}"
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"启动文件监听失败: {ex.Message}"
            }, JsonOptions.Default);
        }
    }

    /// <summary>
    /// 停止文件监听
    /// </summary>
    /// <remarks>
    /// 停止指定路径的文件监听。
    /// </remarks>
    /// <param name="projectPath">项目文件路径或目录</param>
    /// <returns>操作结果</returns>
    [McpServerTool, Description("停止文件监听")]
    public static string StopFileWatching(
        [Description("项目文件路径或目录")] string projectPath)
    {
        try
        {
            if (!_activeWatchers.TryGetValue(projectPath, out var watcher))
            {
                return JsonSerializer.Serialize(new
                {
                    status = "not_watching",
                    message = $"未监听路径: {projectPath}"
                }, JsonOptions.Default);
            }

            watcher.StopWatching();
            watcher.Dispose();
            _activeWatchers.Remove(projectPath);

            return JsonSerializer.Serialize(new
            {
                status = "stopped",
                path = projectPath,
                message = $"已停止监听路径: {projectPath}"
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"停止文件监听失败: {ex.Message}"
            }, JsonOptions.Default);
        }
    }

    /// <summary>
    /// 分析变更影响
    /// </summary>
    /// <remarks>
    /// 分析文件变更对项目的影响范围。
    /// </remarks>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <param name="changedFilePath">变更的文件路径</param>
    /// <param name="changeType">变更类型</param>
    /// <returns>影响分析结果</returns>
    [McpServerTool, Description("分析代码变更的影响范围")]
    public static async Task<string> AnalyzeChangeImpact(
        IWorkspaceManager workspaceManager,
        ILogger<ChangeImpactAnalyzer> logger,
        [Description("项目文件路径（.csproj）")] string projectPath,
        [Description("变更的文件路径")] string changedFilePath,
        [Description("变更类型（MethodSignature, TypeMemberChange, TypeDeletion等）")] string changeType = "Other")
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);

            var analyzer = new ChangeImpactAnalyzer(logger);

            var changeTypeEnum = ParseChangeType(changeType);
            var result = await analyzer.AnalyzeAsync(project, changedFilePath, changeTypeEnum);

            var report = new
            {
                changedFile = result.ChangedFilePath,
                changeType = result.ChangeType.ToString(),
                impactLevel = result.GetImpactLevel().ToString(),
                impactScore = Math.Round(result.ImpactScore, 2),
                directImpactCount = result.DirectImpacts.Count,
                indirectImpactCount = result.IndirectImpacts.Count,
                affectedTests = result.AffectedTests,
                recommendedTestAreas = result.RecommendedTestAreas,
                directImpacts = result.DirectImpacts.Take(10).Select(i => new
                {
                    filePath = i.FilePath,
                    symbolName = i.SymbolName,
                    symbolKind = i.SymbolKind.ToString(),
                    impactScore = Math.Round(i.ImpactScore, 2),
                    isPublicApi = i.IsPublicApi
                })
            };

            return JsonSerializer.Serialize(new { data = report }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"分析变更影响失败: {ex.Message}"
            }, JsonOptions.Default);
        }
    }

    /// <summary>
    /// 获取缓存统计
    /// </summary>
    /// <remarks>
    /// 获取分析结果缓存的统计信息。
    /// </remarks>
    /// <param name="cache">缓存实例</param>
    /// <returns>缓存统计信息</returns>
    [McpServerTool, Description("获取分析缓存统计信息")]
    public static async Task<string> GetCacheStatistics(
        IAnalysisResultCache cache)
    {
        try
        {
            var stats = await cache.GetStatisticsAsync();

            var report = new
            {
                totalItems = stats.TotalItems,
                hitCount = stats.HitCount,
                missCount = stats.MissCount,
                hitRate = Math.Round(stats.HitRate * 100, 2),
                percentage = $"{Math.Round(stats.HitRate * 100, 2)}%",
                sizeInBytes = stats.SizeInBytes,
                sizeInMB = Math.Round(stats.SizeInBytes / 1024.0 / 1024.0, 2),
                expiredItems = stats.ExpiredItems,
                lastUpdated = stats.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"
            };

            return JsonSerializer.Serialize(new { data = report }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"获取缓存统计失败: {ex.Message}"
            }, JsonOptions.Default);
        }
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    /// <remarks>
    /// 清除所有分析结果缓存。
    /// </remarks>
    /// <param name="cache">缓存实例</param>
    /// <returns>操作结果</returns>
    [McpServerTool, Description("清除分析缓存")]
    public static async Task<string> ClearCache(IAnalysisResultCache cache)
    {
        try
        {
            await cache.ClearAsync();

            return JsonSerializer.Serialize(new
            {
                status = "cleared",
                message = "缓存已清除"
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"清除缓存失败: {ex.Message}"
            }, JsonOptions.Default);
        }
    }

    private static ChangeType ParseChangeType(string changeType)
    {
        return Enum.TryParse<ChangeType>(changeType, true, out var result)
            ? result
            : ChangeType.Other;
    }
}
