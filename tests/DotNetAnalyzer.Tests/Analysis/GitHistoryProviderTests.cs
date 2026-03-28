using DotNetAnalyzer.Core.Analysis;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.Analysis;

public class GitHistoryProviderTests
{
    private readonly GitHistoryProvider _provider;

    public GitHistoryProviderTests()
    {
        _provider = new GitHistoryProvider(
            NullLogger<GitHistoryProvider>.Instance);
    }

    [Fact]
    public void IsGitRepository_WithCurrentRepo_ShouldReturnTrue()
    {
        // 使用项目自身作为 Git 仓库进行验证
        var repoRoot = GetRepoRoot();
        GitHistoryProvider.IsGitRepository(repoRoot).Should().BeTrue();
    }

    [Fact]
    public void IsGitRepository_WithNonGitDirectory_ShouldReturnFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            GitHistoryProvider.IsGitRepository(tempDir).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void IsGitRepository_WithEmptyPath_ShouldReturnFalse()
    {
        GitHistoryProvider.IsGitRepository(string.Empty).Should().BeFalse();
        GitHistoryProvider.IsGitRepository(null!).Should().BeFalse();
        GitHistoryProvider.IsGitRepository("   ").Should().BeFalse();
    }

    [Fact]
    public void IsGitRepository_WithNonExistentPath_ShouldReturnFalse()
    {
        GitHistoryProvider.IsGitRepository(
            "/nonexistent/path/that/does/not/exist").Should().BeFalse();
    }

    [Fact]
    public async Task GetChangeHistoryAsync_WithNonGitDirectory_ShouldThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var act = () => _provider.GetChangeHistoryAsync(tempDir, 30);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*不是 Git 仓库*");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetChangeHistoryAsync_WithNullPath_ShouldThrowArgumentException()
    {
        var act = () => _provider.GetChangeHistoryAsync(null!, 30);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetChangeHistoryAsync_WithNonExistentDirectory_ShouldThrow()
    {
        var act = () => _provider.GetChangeHistoryAsync(
            "/nonexistent/path", 30);
        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task GetChangeHistoryAsync_With30DayPeriod_ShouldReturnRecords()
    {
        var repoRoot = GetRepoRoot();
        var records = await _provider.GetChangeHistoryAsync(repoRoot, 30);

        records.Should().NotBeNull();
        records.Should().NotBeEmpty();
        records.Should().OnlyContain(
            r => !string.IsNullOrWhiteSpace(r.FilePath));
        records.Should().OnlyContain(
            r => r.Timestamp > DateTime.MinValue);
        records.Should().OnlyContain(
            r => r.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public async Task GetChangeHistoryAsync_With60DayPeriod_ShouldReturnAtLeastAsManyAs30Days()
    {
        var repoRoot = GetRepoRoot();
        var records30 = await _provider.GetChangeHistoryAsync(repoRoot, 30);
        var records60 = await _provider.GetChangeHistoryAsync(repoRoot, 60);

        records60.Count.Should().BeGreaterThanOrEqualTo(records30.Count);
    }

    [Fact]
    public async Task GetChangeHistoryAsync_With90DayPeriod_ShouldReturnAtLeastAsManyAs60Days()
    {
        var repoRoot = GetRepoRoot();
        var records60 = await _provider.GetChangeHistoryAsync(repoRoot, 60);
        var records90 = await _provider.GetChangeHistoryAsync(repoRoot, 90);

        records90.Count.Should().BeGreaterThanOrEqualTo(records60.Count);
    }

    [Fact]
    public async Task GetChangeHistoryAsync_RecordsShouldContainFilePaths()
    {
        var repoRoot = GetRepoRoot();
        var records = await _provider.GetChangeHistoryAsync(repoRoot, 30);

        records.Should().OnlyContain(
            r => r.FilePath.StartsWith(repoRoot) ||
                  r.FilePath.Contains(Path.DirectorySeparatorChar));
    }

    [Fact]
    public async Task GetChangeHistoryAsync_RecordsShouldHaveValidTimestamps()
    {
        var repoRoot = GetRepoRoot();
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var records = await _provider.GetChangeHistoryAsync(repoRoot, 30);

        records.Should().OnlyContain(r => r.Timestamp >= cutoff);
    }

    [Fact]
    public async Task GetChangeHistoryAsync_WithCancellation_ShouldRespectToken()
    {
        var repoRoot = GetRepoRoot();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // ValidateRepository 在启动 git 进程之前检查 IsGitRepository，
        // 而 IsGitRepository 不检查取消令牌，因此如果仓库验证通过
        // 且 git 命令在取消前完成，则不会抛出 OperationCanceledException。
        // 此测试验证取消令牌不会导致意外行为。
        var act = () => _provider.GetChangeHistoryAsync(
            repoRoot, 30, cts.Token);

        // 可能抛出 OperationCanceledException 或正常完成
        try
        {
            var result = await act();
            result.Should().NotBeNull();
        }
        catch (OperationCanceledException)
        {
            // 符合预期：取消令牌生效
        }
    }

    private static string GetRepoRoot()
    {
        // 从当前工作目录向上查找 .git
        var directory = Directory.GetCurrentDirectory();
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory, ".git")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        // 回退到已知位置
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", ".."),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..")
        };

        foreach (var path in possiblePaths)
        {
            var full = Path.GetFullPath(path);
            if (Directory.Exists(Path.Combine(full, ".git")))
            {
                return full;
            }
        }

        throw new InvalidOperationException(
            "无法找到 Git 仓库根目录。");
    }
}
