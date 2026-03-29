using DotNetAnalyzer.Core.DependencyHealth;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetAnalyzer.Tests.DependencyHealth;

public class ProjectFileDependencyExtractorTests : IDisposable
{
    private readonly Mock<ILogger<ProjectFileDependencyExtractor>> _loggerMock = new();
    private readonly string _tempDir;

    public ProjectFileDependencyExtractorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DotNetAnalyzer_Tests_Extractor_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CreateCsproj(string content)
    {
        var path = Path.Combine(_tempDir, "Test.csproj");
        File.WriteAllText(path, content);
        return path;
    }

    private ProjectFileDependencyExtractor CreateExtractor()
    {
        return new ProjectFileDependencyExtractor(_loggerMock.Object);
    }

    [Fact]
    public async Task ExtractAsync_WithPackageReferences_ReturnsAll()
    {
        // Arrange
        var csproj = CreateCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                <PackageReference Include="Serilog" Version="3.1.1" />
              </ItemGroup>
            </Project>
            """);

        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractAsync(csproj);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.PackageId == "Newtonsoft.Json" && r.Version == "13.0.3");
        result.Should().Contain(r => r.PackageId == "Serilog" && r.Version == "3.1.1");
    }

    [Fact]
    public async Task ExtractAsync_WithVersionElement_ReturnsVersion()
    {
        // Arrange
        var csproj = CreateCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Extensions.Logging">
                  <Version>8.0.0</Version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractAsync(csproj);

        // Assert
        result.Should().HaveCount(1);
        result[0].Version.Should().Be("8.0.0");
    }

    [Fact]
    public async Task ExtractAsync_WithPrivateAssets_ReturnsPrivateAssets()
    {
        // Arrange
        var csproj = CreateCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Test.Package" Version="1.0.0">
                  <PrivateAssets>all</PrivateAssets>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractAsync(csproj);

        // Assert
        result.Should().HaveCount(1);
        result[0].PrivateAssets.Should().Be("all");
    }

    [Fact]
    public async Task ExtractAsync_WithCondition_ReturnsCondition()
    {
        // Arrange
        var csproj = CreateCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="ConditionPackage" Version="1.0.0" Condition="'$(Configuration)' == 'Debug'" />
              </ItemGroup>
            </Project>
            """);

        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractAsync(csproj);

        // Assert
        result.Should().HaveCount(1);
        result[0].Condition.Should().Be("'$(Configuration)' == 'Debug'");
    }

    [Fact]
    public async Task ExtractAsync_FileNotFound_ReturnsEmpty()
    {
        // Arrange
        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractAsync("/nonexistent/path.csproj");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_NoPackageReferences_ReturnsEmpty()
    {
        // Arrange
        var csproj = CreateCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);

        var extractor = CreateExtractor();

        // Act
        var result = await extractor.ExtractAsync(csproj);

        // Assert
        result.Should().BeEmpty();
    }
}
