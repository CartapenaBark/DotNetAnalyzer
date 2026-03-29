using DotNetAnalyzer.Core.ProjectManipulation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.ProjectManipulation;

/// <summary>
/// NuGetPackageService 单元测试。
/// </summary>
/// <remarks>
/// 仅测试参数校验逻辑（空参/无效参数抛异常）。
/// 网络查询依赖 NuGet.org API，不适合单元测试。
/// </remarks>
public class NuGetPackageServiceTests
{
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new NuGetPackageService(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetLatestVersionAsync_NullPackageId_Throws()
    {
        using var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        var act = () => service.GetLatestVersionAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetLatestVersionAsync_EmptyPackageId_Throws()
    {
        using var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        var act = () => service.GetLatestVersionAsync(string.Empty);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PackageExistsAsync_NullPackageId_Throws()
    {
        using var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        var act = () => service.PackageExistsAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PackageExistsAsync_EmptyPackageId_Throws()
    {
        using var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        var act = () => service.PackageExistsAsync(string.Empty);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchPackageAsync_NullTerm_Throws()
    {
        using var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        var act = () => service.SearchPackageAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchPackageAsync_EmptyTerm_Throws()
    {
        using var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        var act = () => service.SearchPackageAsync(string.Empty);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchPackageAsync_NegativeSkip_Throws()
    {
        using var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        var act = () => service.SearchPackageAsync(
            "test", skip: -1);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SearchPackageAsync_ZeroTake_Throws()
    {
        using var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        var act = () => service.SearchPackageAsync(
            "test", take: 0);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SearchPackageAsync_TakeExceedsMax_Throws()
    {
        using var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        var act = () => service.SearchPackageAsync(
            "test", take: 101);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetPackageInfoAsync_NullPackageId_Throws()
    {
        using var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        var act = () => service.GetPackageInfoAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPackageInfoAsync_EmptyPackageId_Throws()
    {
        using var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        var act = () => service.GetPackageInfoAsync(string.Empty);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Dispose_CalledTwice_NoException()
    {
        var service = new NuGetPackageService(
            NullLogger<NuGetPackageService>.Instance);
        service.Dispose();
        service.Dispose();
    }
}
