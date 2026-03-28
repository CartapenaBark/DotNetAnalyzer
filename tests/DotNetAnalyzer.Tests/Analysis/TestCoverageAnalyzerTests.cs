using DotNetAnalyzer.Core.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotNetAnalyzer.Tests.Analysis;

/// <summary>
/// TestCoverageAnalyzer 集成测试。
/// 验证真实覆盖率文件解析和启发式回退行为。
/// </summary>
public class TestCoverageAnalyzerTests : IDisposable
{
    private readonly Mock<ILogger<TestCoverageAnalyzer>> _analyzerLoggerMock;
    private readonly Mock<ILogger<CoverageDataParser>> _parserLoggerMock;
    private readonly CoverageDataParser _parser;
    private readonly TestCoverageAnalyzer _analyzer;
    private readonly string _tempDir;

    public TestCoverageAnalyzerTests()
    {
        _analyzerLoggerMock = new Mock<ILogger<TestCoverageAnalyzer>>();
        _parserLoggerMock = new Mock<ILogger<CoverageDataParser>>();
        _parser = new CoverageDataParser(_parserLoggerMock.Object);
        _analyzer = new TestCoverageAnalyzer(
            _analyzerLoggerMock.Object, _parser);

        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "dotnet-analyzer-coverage-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    /// <summary>
    /// 当项目中存在 coverage.cobertura.xml 时，
    /// AnalyzeAsync 应返回 verified 结果。
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_WithCoverageFile_ShouldReturnVerifiedResult()
    {
        // Arrange
        var (project, coveragePath) = CreateProjectWithCoverage(
            sourceFiles: new Dictionary<string, string>
            {
                ["Calculator.cs"] = CreateCalculatorSource(),
                ["CalculatorTests.cs"] = CreateTestSource()
            },
            coverageXml: CreateValidCoberturaXml());

        try
        {
            // Act
            var result = await _analyzer.AnalyzeAsync(project);

            // Assert
            result.Should().NotBeNull();
            result.Credibility.Should().Be("verified");
            result.LineCoverage.Should().BeApproximately(85.0, 0.1);
            result.BranchCoverage.Should().BeApproximately(72.0, 0.1);
            result.MethodCoverage.Should().BeGreaterThan(0);
            result.FileCoverages.Should().NotBeEmpty();
        }
        finally
        {
            CleanupProject(coveragePath);
        }
    }

    /// <summary>
    /// AnalyzeWithCoverageDataAsync 在覆盖文件存在时
    /// 应返回 Credibility = "verified"。
    /// </summary>
    [Fact]
    public async Task AnalyzeWithCoverageDataAsync_WithFile_ShouldBeVerified()
    {
        // Arrange
        var (project, coveragePath) = CreateProjectWithCoverage(
            sourceFiles: new Dictionary<string, string>
            {
                ["Service.cs"] = CreateServiceSource()
            },
            coverageXml: CreateValidCoberturaXml());

        try
        {
            // Act
            var result = await _analyzer.AnalyzeWithCoverageDataAsync(
                project);

            // Assert
            result.Should().NotBeNull();
            result!.Credibility.Should().Be("verified");
        }
        finally
        {
            CleanupProject(coveragePath);
        }
    }

    /// <summary>
    /// AnalyzeWithCoverageDataAsync 在无覆盖文件时应返回 null。
    /// </summary>
    [Fact]
    public async Task AnalyzeWithCoverageDataAsync_NoFile_ShouldReturnNull()
    {
        // Arrange - 项目目录中没有覆盖率文件
        var project = CreateProjectWithoutCoverage(
            new Dictionary<string, string>
            {
                ["Calculator.cs"] = CreateCalculatorSource()
            });

        // Act
        var result = await _analyzer.AnalyzeWithCoverageDataAsync(
            project);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// 无覆盖文件时 AnalyzeAsync 应回退到启发式结果。
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_NoCoverageFile_ShouldFallbackToHeuristic()
    {
        // Arrange - 项目目录中没有覆盖率文件
        var project = CreateProjectWithoutCoverage(
            new Dictionary<string, string>
            {
                ["Calculator.cs"] = CreateCalculatorSource(),
                ["CalculatorTests.cs"] = CreateTestSource()
            });

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.Should().NotBeNull();
        result.Credibility.Should().Be("heuristic");
        result.FileCoverages.Should().NotBeEmpty();
    }

    /// <summary>
    /// 使用损坏的 XML 覆盖文件时应回退到启发式结果。
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_InvalidCoverageFile_ShouldFallback()
    {
        // Arrange
        var project = CreateProjectWithInvalidCoverage(
            new Dictionary<string, string>
            {
                ["Calculator.cs"] = CreateCalculatorSource()
            });

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.Should().NotBeNull();
        result.Credibility.Should().Be("heuristic");
    }

    /// <summary>
    /// 启发式模式下测试文件应显示 100% 覆盖率。
    /// </summary>
    [Fact]
    public async Task AnalyzeHeuristicAsync_TestFile_ShouldShowFullCoverage()
    {
        // Arrange
        var project = CreateProjectWithoutCoverage(
            new Dictionary<string, string>
            {
                ["CalculatorTests.cs"] = CreateTestSource()
            });

        // Act
        var result = await _analyzer.AnalyzeHeuristicAsync(project);

        // Assert
        result.Should().NotBeNull();
        result.Credibility.Should().Be("heuristic");
        result.FileCoverages.Should().HaveCount(1);
        result.FileCoverages[0].CoveragePercentage.Should().Be(100.0);
    }

    // ---- 辅助方法 ----

    private (Project project, string projectDir) CreateProjectWithCoverage(
        Dictionary<string, string> sourceFiles,
        string coverageXml)
    {
        var csprojPath = Path.Combine(_tempDir, "TestProject.csproj");
        File.WriteAllText(csprojPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        foreach (var (fileName, content) in sourceFiles)
        {
            File.WriteAllText(
                Path.Combine(_tempDir, fileName), content);
        }

        // 写入覆盖率文件
        var coveragePath = Path.Combine(
            _tempDir, "coverage.cobertura.xml");
        File.WriteAllText(coveragePath, coverageXml);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            csprojPath);

        workspace.AddProject(projectInfo);

        foreach (var (fileName, content) in sourceFiles)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            var docInfo = DocumentInfo.Create(
                documentId,
                fileName,
                filePath: Path.Combine(_tempDir, fileName),
                loader: TextLoader.From(
                    TextAndVersion.Create(
                        SourceText.From(content),
                        VersionStamp.Create())));

            workspace.AddDocument(docInfo);
        }

        var project = workspace.CurrentSolution.GetProject(projectId)!;
        return (project, _tempDir);
    }

    private Project CreateProjectWithoutCoverage(
        Dictionary<string, string> sourceFiles)
    {
        var dir = Path.Combine(_tempDir, "NoCoverage");
        Directory.CreateDirectory(dir);

        var csprojPath = Path.Combine(dir, "TestProject.csproj");
        File.WriteAllText(csprojPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        foreach (var (fileName, content) in sourceFiles)
        {
            File.WriteAllText(Path.Combine(dir, fileName), content);
        }

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            csprojPath);

        workspace.AddProject(projectInfo);

        foreach (var (fileName, content) in sourceFiles)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            var docInfo = DocumentInfo.Create(
                documentId,
                fileName,
                filePath: Path.Combine(dir, fileName),
                loader: TextLoader.From(
                    TextAndVersion.Create(
                        SourceText.From(content),
                        VersionStamp.Create())));

            workspace.AddDocument(docInfo);
        }

        return workspace.CurrentSolution.GetProject(projectId)!;
    }

    private Project CreateProjectWithInvalidCoverage(
        Dictionary<string, string> sourceFiles)
    {
        var dir = Path.Combine(_tempDir, "InvalidCoverage");
        Directory.CreateDirectory(dir);

        var csprojPath = Path.Combine(dir, "TestProject.csproj");
        File.WriteAllText(csprojPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        foreach (var (fileName, content) in sourceFiles)
        {
            File.WriteAllText(Path.Combine(dir, fileName), content);
        }

        // 写入损坏的覆盖率文件
        var coveragePath = Path.Combine(
            dir, "coverage.cobertura.xml");
        File.WriteAllText(coveragePath, "<<<INVALID XML>>>");

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            csprojPath);

        workspace.AddProject(projectInfo);

        foreach (var (fileName, content) in sourceFiles)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            var docInfo = DocumentInfo.Create(
                documentId,
                fileName,
                filePath: Path.Combine(dir, fileName),
                loader: TextLoader.From(
                    TextAndVersion.Create(
                        SourceText.From(content),
                        VersionStamp.Create())));

            workspace.AddDocument(docInfo);
        }

        return workspace.CurrentSolution.GetProject(projectId)!;
    }

    private static void CleanupProject(string projectDir)
    {
        // 不做任何操作 —— Dispose 会统一清理
    }

    private static string CreateCalculatorSource()
    {
        return """
            namespace Sample;

            public class Calculator
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }

                public int Subtract(int a, int b)
                {
                    return a - b;
                }
            }
            """;
    }

    private static string CreateTestSource()
    {
        return """
            using Xunit;

            namespace Sample.Tests;

            public class CalculatorTests
            {
                [Fact]
                public void Add_ReturnsSum()
                {
                    var calc = new Calculator();
                    Assert.Equal(4, calc.Add(2, 2));
                }
            }
            """;
    }

    private static string CreateServiceSource()
    {
        return """
            namespace Sample;

            public class Service
            {
                public string GetData()
                {
                    return "data";
                }
            }
            """;
    }

    private static string CreateValidCoberturaXml()
    {
        return """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.85" branch-rate="0.72" version="1.9">
              <packages>
                <package name="Sample" line-rate="0.85" branch-rate="0.72">
                  <classes>
                    <class name="Calculator" filename="Calculator.cs"
                           line-rate="0.90" branch-rate="0.80">
                      <methods>
                        <method name="Add" line-rate="1.0"
                                branch-rate="1.0">
                          <lines>
                            <line number="5" hits="10"/>
                          </lines>
                        </method>
                        <method name="Subtract" line-rate="0.5"
                                branch-rate="0.5">
                          <lines>
                            <line number="10" hits="3"/>
                            <line number="11" hits="0"/>
                          </lines>
                        </method>
                      </methods>
                      <lines>
                        <line number="5" hits="10"/>
                        <line number="10" hits="3"/>
                        <line number="11" hits="0"/>
                      </lines>
                    </class>
                    <class name="CalculatorTests" filename="CalculatorTests.cs"
                           line-rate="1.0" branch-rate="1.0">
                      <methods/>
                      <lines>
                        <line number="1" hits="5"/>
                        <line number="2" hits="5"/>
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
    }
}
