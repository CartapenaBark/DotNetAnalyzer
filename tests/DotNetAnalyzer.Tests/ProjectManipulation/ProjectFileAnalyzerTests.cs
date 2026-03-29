using DotNetAnalyzer.Core.ProjectManipulation;
using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.ProjectManipulation.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.ProjectManipulation;

/// <summary>
/// ProjectFileAnalyzer 单元测试。
/// </summary>
/// <remarks>
/// 使用真实 .csproj 临时文件验证只读分析操作。
/// 基于 ProjectRootElement.Open() 不需要 MSBuild SDK。
/// </remarks>
public class ProjectFileAnalyzerTests : IDisposable
{
    private readonly ProjectFileAnalyzer _analyzer;
    private readonly List<string> _tempFiles = [];

    public ProjectFileAnalyzerTests()
    {
        _analyzer = new ProjectFileAnalyzer(
            NullLogger<ProjectFileAnalyzer>.Instance);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
        _tempFiles.Clear();
    }

    private string CreateTempCsproj(string content)
    {
        var file = Path.Combine(
            Path.GetTempPath(),
            $"PFA_{Guid.NewGuid():N}.csproj");
        File.WriteAllText(file, content);
        _tempFiles.Add(file);
        return file;
    }

    private static string SingleTfmCsproj =>
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private static string MultiTfmCsproj =>
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
          </PropertyGroup>
        </Project>
        """;

    private static string WithReferencesCsproj =>
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
            <PackageReference Include="Serilog" Version="3.1.0" PrivateAssets="all" />
          </ItemGroup>
          <ItemGroup>
            <ProjectReference Include="..\Other\Other.csproj" />
          </ItemGroup>
        </Project>
        """;

    #region Constructor

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new ProjectFileAnalyzer(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetPackageReferences

    [Fact]
    public async Task GetPackageReferencesAsync_NullPath_ThrowsArgumentException()
    {
        var act = () => _analyzer.GetPackageReferencesAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPackageReferencesAsync_EmptyPath_ThrowsArgumentException()
    {
        var act = () => _analyzer.GetPackageReferencesAsync(string.Empty);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPackageReferencesAsync_WithPackages_ReturnsBoth()
    {
        var csproj = CreateTempCsproj(WithReferencesCsproj);
        var result = await _analyzer.GetPackageReferencesAsync(csproj);

        result.Should().HaveCount(2);
        result.Select(p => p.PackageId).Should().Contain(
            "Newtonsoft.Json", "Serilog");
        result.First(p => p.PackageId == "Newtonsoft.Json")
            .Version.Should().Be("13.0.3");
        result.First(p => p.PackageId == "Serilog")
            .PrivateAssets.Should().Be("all");
    }

    [Fact]
    public async Task GetPackageReferencesAsync_NoPackages_ReturnsEmpty()
    {
        var csproj = CreateTempCsproj(SingleTfmCsproj);
        var result = await _analyzer.GetPackageReferencesAsync(csproj);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPackageReferencesAsync_NonExistentFile_ThrowsException()
    {
        var act = () => _analyzer.GetPackageReferencesAsync(
            "/nonexistent/Project.csproj");
        await act.Should().ThrowAsync<PathValidationException>();
    }

    #endregion

    #region GetProjectReferences

    [Fact]
    public async Task GetProjectReferencesAsync_NullPath_ThrowsArgumentException()
    {
        var act = () => _analyzer.GetProjectReferencesAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetProjectReferencesAsync_EmptyPath_ThrowsArgumentException()
    {
        var act = () => _analyzer.GetProjectReferencesAsync(string.Empty);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetProjectReferencesAsync_WithReferences_ReturnsPaths()
    {
        var csproj = CreateTempCsproj(WithReferencesCsproj);
        var result = await _analyzer.GetProjectReferencesAsync(csproj);

        result.Should().ContainSingle();
        result[0].Should().Be(@"..\Other\Other.csproj");
    }

    [Fact]
    public async Task GetProjectReferencesAsync_NoReferences_ReturnsEmpty()
    {
        var csproj = CreateTempCsproj(SingleTfmCsproj);
        var result = await _analyzer.GetProjectReferencesAsync(csproj);

        result.Should().BeEmpty();
    }

    #endregion

    #region GetTargetFrameworks

    [Fact]
    public async Task GetTargetFrameworksAsync_NullPath_ThrowsArgumentException()
    {
        var act = () => _analyzer.GetTargetFrameworksAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetTargetFrameworksAsync_EmptyPath_ThrowsArgumentException()
    {
        var act = () => _analyzer.GetTargetFrameworksAsync(string.Empty);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetTargetFrameworksAsync_SingleTfm_ReturnsOne()
    {
        var csproj = CreateTempCsproj(SingleTfmCsproj);
        var result = await _analyzer.GetTargetFrameworksAsync(csproj);

        result.Should().ContainSingle();
        result[0].Should().Be("net8.0");
    }

    [Fact]
    public async Task GetTargetFrameworksAsync_MultiTfm_ReturnsBoth()
    {
        var csproj = CreateTempCsproj(MultiTfmCsproj);
        var result = await _analyzer.GetTargetFrameworksAsync(csproj);

        result.Should().HaveCount(2);
        result.Should().Contain("net8.0", "net9.0");
    }

    #endregion

    #region GetProperties

    [Fact]
    public async Task GetPropertiesAsync_NullPath_ThrowsArgumentException()
    {
        var act = () => _analyzer.GetPropertiesAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPropertiesAsync_EmptyPath_ThrowsArgumentException()
    {
        var act = () => _analyzer.GetPropertiesAsync(string.Empty);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPropertiesAsync_SimpleProject_ReturnsProperties()
    {
        var csproj = CreateTempCsproj(SingleTfmCsproj);
        var result = await _analyzer.GetPropertiesAsync(csproj);

        result.Should().NotBeEmpty();
        result.Should().Contain(p =>
            p.Name == "TargetFramework" &&
            p.Value == "net8.0");
    }

    #endregion
}
