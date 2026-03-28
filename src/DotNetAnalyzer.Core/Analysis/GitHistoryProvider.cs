using System.Diagnostics;
using DotNetAnalyzer.Core.Models.CodeQuality;
using DotNetAnalyzer.Core.Visualization;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Analysis;

/// <summary>
/// Git 历史记录提供器，通过调用 git log 获取文件变更频率数据。
/// </summary>
/// <remarks>
/// 使用 <c>System.Diagnostics.Process</c> 调用 git 命令行，
/// 解析输出以构建 <see cref="ChangeRecord"/> 列表。
/// </remarks>
public partial class GitHistoryProvider
{
    private readonly ILogger<GitHistoryProvider> _logger;

    [LoggerMessage(
        LogLevel.Debug,
        "执行 git log: {Arguments} 在 {RepositoryPath}")]
    private static partial void LogGitCommand(
        ILogger logger, string arguments, string repositoryPath);

    [LoggerMessage(
        LogLevel.Information,
        "在最近 {PeriodDays} 天内未找到变更记录")]
    private static partial void LogNoRecordsFound(
        ILogger logger, int periodDays);

    [LoggerMessage(
        LogLevel.Information,
        "解析到 {RecordCount} 条变更记录")]
    private static partial void LogRecordsParsed(
        ILogger logger, int recordCount);

    /// <summary>
    /// 初始化 <see cref="GitHistoryProvider"/> 的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public GitHistoryProvider(ILogger<GitHistoryProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取指定仓库在给定时间段内的文件变更历史。
    /// </summary>
    /// <param name="repositoryPath">仓库根目录路径。</param>
    /// <param name="periodDays">回溯天数（默认 30 天）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>变更记录列表。</returns>
    /// <exception cref="InvalidOperationException">
    /// 当 <paramref name="repositoryPath"/> 不是 Git 仓库时抛出。
    /// </exception>
    public async Task<List<ChangeRecord>> GetChangeHistoryAsync(
        string repositoryPath,
        int periodDays = 30,
        CancellationToken cancellationToken = default)
    {
        ValidateRepository(repositoryPath);

        var arguments = $"log --format=format:\"%at\" --name-only " +
            $"--since=\"{periodDays} days ago\"";

        LogGitCommand(_logger, arguments, repositoryPath);

        var output = await RunGitCommandAsync(
            repositoryPath, arguments, cancellationToken);

        if (string.IsNullOrWhiteSpace(output))
        {
            LogNoRecordsFound(_logger, periodDays);
            return [];
        }

        var records = ParseGitLogOutput(output, repositoryPath);
        LogRecordsParsed(_logger, records.Count);

        return records;
    }

    /// <summary>
    /// 检查指定目录是否为 Git 仓库。
    /// </summary>
    /// <param name="repositoryPath">待检查的目录路径。</param>
    /// <returns>如果目录是 Git 仓库则为 <c>true</c>，否则为 <c>false</c>。</returns>
    public static bool IsGitRepository(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return false;
        }

        // 快速路径：检查 .git 目录是否存在
        var gitDir = Path.Combine(repositoryPath, ".git");
        if (Directory.Exists(gitDir))
        {
            return true;
        }

        // 慢路径：使用 git rev-parse 确认（适用于 worktree 等场景）
        try
        {
            using var process = new Process();
            process.StartInfo = CreateStartInfo(
                repositoryPath, "rev-parse --is-inside-work-tree");
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 && output == "true";
        }
        catch
        {
            return false;
        }
    }

    private static void ValidateRepository(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            throw new ArgumentException(
                "仓库路径不能为空。",
                nameof(repositoryPath));
        }

        if (!Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(
                $"仓库目录不存在: {repositoryPath}");
        }

        if (!IsGitRepository(repositoryPath))
        {
            throw new InvalidOperationException(
                $"指定目录不是 Git 仓库（未找到 .git 目录或 git 命令执行失败）: {repositoryPath}");
        }
    }

    private static async Task<string> RunGitCommandAsync(
        string repositoryPath,
        string arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = CreateStartInfo(repositoryPath, arguments);

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(outputTask, errorTask);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException(
                $"git 命令执行失败（退出码 {process.ExitCode}）: {error}");
        }

        return await outputTask;
    }

    private static ProcessStartInfo CreateStartInfo(
        string repositoryPath, string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = repositoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
    }

    private static List<ChangeRecord> ParseGitLogOutput(
        string output, string repositoryPath)
    {
        var records = new List<ChangeRecord>();
        var lines = output.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);

        DateTime? currentTimestamp = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (string.IsNullOrEmpty(line))
            {
                currentTimestamp = null;
                continue;
            }

            // 尝试解析 Unix 时间戳行
            if (long.TryParse(line, out var unixSeconds))
            {
                currentTimestamp = DateTimeOffset
                    .FromUnixTimeSeconds(unixSeconds)
                    .DateTime;
                continue;
            }

            // 跳过二进制文件等非路径行
            if (line.Contains("=>") ||
                line.StartsWith("Binary file"))
            {
                continue;
            }

            if (currentTimestamp == null)
            {
                continue;
            }

            var fullPath = Path.Combine(repositoryPath, line);
            records.Add(new ChangeRecord
            {
                FilePath = fullPath,
                Timestamp = currentTimestamp.Value,
                ChangeType = ChangeType.TypeMemberChange
            });
        }

        return records;
    }
}
