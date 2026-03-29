using DotNetAnalyzer.Core.DependencyHealth;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetAnalyzer.Tests.DependencyHealth;

public class NuGetAssetsFileParserTests : IDisposable
{
    private readonly Mock<ILogger<NuGetAssetsFileParser>> _loggerMock = new();
    private readonly string _tempDir;

    public NuGetAssetsFileParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DotNetAnalyzer_Tests_Assets_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private NuGetAssetsFileParser CreateParser()
    {
        return new NuGetAssetsFileParser(_loggerMock.Object);
    }

    [Fact]
    public async Task ParseAsync_WithValidAssetsFile_ReturnsLibraries()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "project.assets.json");
        var content = """
            {
              "version": 3,
              "targets": {
                ".NETCoreApp,Version=v8.0": {}
              },
              "libraries": {
                "Newtonsoft.Json/13.0.3": {
                  "type": "package",
                  "path": "newtonsoft.json/13.0.3",
                  "sha512": "abc123",
                  "dependencies": {
                    "System.Runtime": "4.3.0"
                  }
                },
                "MyProject/1.0.0": {
                  "type": "project",
                  "path": "../MyProject/MyProject.csproj",
                  "sha512": ""
                }
              }
            }
            """;
        await File.WriteAllTextAsync(path, content);

        var parser = CreateParser();

        // Act
        var result = await parser.ParseAsync(path);

        // Assert
        Assert.NotNull(result);
        result.Libraries.Should().HaveCount(2);
        result.Libraries.Should().Contain(l => l.Name == "Newtonsoft.Json" && l.Version == "13.0.3");
        result.Libraries.Should().Contain(l => l.Type == "project");
        result.PackageDependencies.Should().Contain(d =>
            d.ParentPackage == "Newtonsoft.Json" && d.DependencyPackage == "System.Runtime");
    }

    [Fact]
    public async Task ParseAsync_FileNotFound_ReturnsNull()
    {
        // Arrange
        var parser = CreateParser();

        // Act
        var result = await parser.ParseAsync("/nonexistent/project.assets.json");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ParseAsync_EmptyLibraries_ReturnsEmptyResult()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "empty.assets.json");
        var content = """{"version":3,"targets":{},"libraries":{}}""";
        await File.WriteAllTextAsync(path, content);

        var parser = CreateParser();

        // Act
        var result = await parser.ParseAsync(path);

        // Assert
        Assert.NotNull(result);
        result.Libraries.Should().BeEmpty();
        result.PackageDependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_NoLibrariesProperty_ReturnsEmptyResult()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "nolibs.assets.json");
        var content = """{"version":3,"targets":{}}""";
        await File.WriteAllTextAsync(path, content);

        var parser = CreateParser();

        // Act
        var result = await parser.ParseAsync(path);

        // Assert
        Assert.NotNull(result);
        result.Libraries.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "invalid.assets.json");
        await File.WriteAllTextAsync(path, "not valid json {{{");

        var parser = CreateParser();

        // Act
        var result = await parser.ParseAsync(path);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ParseAsync_WithTargetFramework_SetsTargetOnLibraries()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "target.assets.json");
        var content = """
            {
              "version": 3,
              "targets": {
                "net8.0": {}
              },
              "libraries": {
                "SomePackage/1.0.0": {
                  "type": "package",
                  "path": "somepackage/1.0.0",
                  "sha512": "xyz"
                }
              }
            }
            """;
        await File.WriteAllTextAsync(path, content);

        var parser = CreateParser();

        // Act
        var result = await parser.ParseAsync(path);

        // Assert
        Assert.NotNull(result);
        result.Libraries.Should().HaveCount(1);
        result.Libraries[0].Target.Should().Be("net8.0");
    }
}
