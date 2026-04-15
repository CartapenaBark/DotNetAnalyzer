using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Analysis.Desktop;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Cli.Tools;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DotNetAnalyzer.Tests.Analysis.Desktop;

/// <summary>
/// DesktopTools MCP 工具测试。
/// </summary>
/// <remarks>
/// 使用 AdhocWorkspace 创建测试项目，验证桌面分析工具的 JSON 响应格式。
/// </remarks>
public class DesktopToolsTests
{
    private static async Task<Project> CreateProjectAsync(
        string sourceCode,
        string fileName = "Test.cs")
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var versionStamp = VersionStamp.Create();

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Threading.Tasks.Task).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Runtime.CompilerServices.TaskAwaiter)
                    .Assembly.Location),
        };

        var projectInfo = ProjectInfo.Create(
            projectId,
            versionStamp,
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            metadataReferences: references);

        workspace.AddProject(projectInfo);

        var documentInfo = DocumentInfo.Create(
            documentId,
            fileName,
            filePath: $"/{fileName}",
            sourceCodeKind: SourceCodeKind.Regular,
            loader: TextLoader.From(TextAndVersion.Create(
                SourceText.From(sourceCode),
                versionStamp)));

        workspace.AddDocument(documentInfo);

        return workspace.CurrentSolution.GetProject(projectId)!;
    }

    private static Mock<IWorkspaceManager>
        CreateMockWorkspaceManager(Project project)
    {
        var mock = new Mock<IWorkspaceManager>();
        mock.Setup(w => w.GetProjectAsync(
                It.IsAny<string>()))
            .ReturnsAsync(project);
        return mock;
    }

    #region DetectMvvmViolations

    [Fact]
    public async Task DetectMvvmViolations_CleanProject_ReturnsZeroViolations()
    {
        // Arrange
        var source = """
            public class CleanViewModel { }
            """;
        var project = await CreateProjectAsync(source);
        var workspaceMock = CreateMockWorkspaceManager(project);
        var detector = new MvvmViolationDetector(
            NullLogger<MvvmViolationDetector>.Instance,
            Options.Create(new AnalyzerOptions()));

        // Act
        var json = await DesktopTools.DetectMvvmViolations(
            workspaceMock.Object, detector, "/Test.csproj");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success")
            .GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data")
            .GetProperty("totalViolations")
            .GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task DetectMvvmViolations_NullProject_ReturnsError()
    {
        // Arrange
        var workspaceMock = new Mock<IWorkspaceManager>();
        workspaceMock.Setup(w => w.GetProjectAsync(
                It.IsAny<string>()))
            .ReturnsAsync((Project)null!);
        var detector = new MvvmViolationDetector(
            NullLogger<MvvmViolationDetector>.Instance,
            Options.Create(new AnalyzerOptions()));

        // Act
        var json = await DesktopTools.DetectMvvmViolations(
            workspaceMock.Object, detector, "/Missing.csproj");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success")
            .GetBoolean().Should().BeFalse();
    }

    #endregion

    #region DetectAsyncAntipatterns

    [Fact]
    public async Task DetectAsyncAntipatterns_CleanProject_ReturnsZeroIssues()
    {
        // Arrange
        var source = "public class Clean { }";
        var project = await CreateProjectAsync(source);
        var workspaceMock = CreateMockWorkspaceManager(project);
        var analyzer = new AsyncPatternAnalyzer(
            NullLogger<AsyncPatternAnalyzer>.Instance);

        // Act
        var json = await DesktopTools.DetectAsyncAntipatterns(
            workspaceMock.Object, analyzer, "/Test.csproj");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success")
            .GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data")
            .GetProperty("totalIssues")
            .GetInt32().Should().Be(0);
    }

    #endregion

    #region AnalyzeDiRegistration

    [Fact]
    public async Task AnalyzeDiRegistration_EmptyProject_ReturnsZeroRegistrations()
    {
        // Arrange
        var source = "public class Empty { }";
        var project = await CreateProjectAsync(source);
        var workspaceMock = CreateMockWorkspaceManager(project);
        var analyzer = new DependencyInjectionAnalyzer(
            NullLogger<DependencyInjectionAnalyzer>.Instance,
            Microsoft.Extensions.Options.Options.Create(
                new DotNetAnalyzer.Core.Configuration.AnalyzerOptions()));

        // Act
        var json = await DesktopTools.AnalyzeDiRegistration(
            workspaceMock.Object, analyzer, "/Test.csproj");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success")
            .GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data")
            .GetProperty("totalRegistrations")
            .GetInt32().Should().Be(0);
    }

    #endregion

    #region FindMissingDiRegistrations

    [Fact]
    public async Task FindMissingDiRegistrations_EmptyProject_ReturnsZeroMissing()
    {
        // Arrange
        var source = "public class Empty { }";
        var project = await CreateProjectAsync(source);
        var workspaceMock = CreateMockWorkspaceManager(project);
        var analyzer = new DependencyInjectionAnalyzer(
            NullLogger<DependencyInjectionAnalyzer>.Instance,
            Microsoft.Extensions.Options.Options.Create(
                new DotNetAnalyzer.Core.Configuration.AnalyzerOptions()));

        // Act
        var json = await DesktopTools.FindMissingDiRegistrations(
            workspaceMock.Object, analyzer, "/Test.csproj");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success")
            .GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data")
            .GetProperty("totalMissing")
            .GetInt32().Should().Be(0);
    }

    #endregion

    #region DetectMemoryLeaks

    [Fact]
    public async Task DetectMemoryLeaks_CleanProject_ReturnsZeroWarnings()
    {
        // Arrange
        var source = "public class Clean { }";
        var project = await CreateProjectAsync(source);
        var workspaceMock = CreateMockWorkspaceManager(project);
        var detector = new MemoryLeakDetector(
            NullLogger<MemoryLeakDetector>.Instance);

        // Act
        var json = await DesktopTools.DetectMemoryLeaks(
            workspaceMock.Object, detector, "/Test.csproj");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success")
            .GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data")
            .GetProperty("totalWarnings")
            .GetInt32().Should().Be(0);
    }

    #endregion

    #region Credibility 标记

    [Fact]
    public async Task DetectMvvmViolations_ResponseContainsCredibility()
    {
        var source = "public class Empty { }";
        var project = await CreateProjectAsync(source);
        var workspaceMock = CreateMockWorkspaceManager(project);
        var analyzer = new MvvmViolationDetector(
            NullLogger<MvvmViolationDetector>.Instance,
            Microsoft.Extensions.Options.Options.Create(
                new DotNetAnalyzer.Core.Configuration.AnalyzerOptions()));

        var json = await DesktopTools.DetectMvvmViolations(
            workspaceMock.Object, analyzer, "/Test.csproj");

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("data")
            .GetProperty("credibility")
            .GetProperty("level")
            .GetString().Should().Be("Verified");
    }

    [Fact]
    public async Task DetectMemoryLeaks_ResponseContainsCredibility()
    {
        var source = "public class Empty { }";
        var project = await CreateProjectAsync(source);
        var workspaceMock = CreateMockWorkspaceManager(project);
        var analyzer = new MemoryLeakDetector(
            NullLogger<MemoryLeakDetector>.Instance);

        var json = await DesktopTools.DetectMemoryLeaks(
            workspaceMock.Object, analyzer, "/Test.csproj");

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("data")
            .GetProperty("credibility")
            .GetProperty("level")
            .GetString().Should().Be("Verified");
    }

    [Fact]
    public async Task AnalyzeDiRegistration_ResponseContainsCredibility()
    {
        var source = "public class Empty { }";
        var project = await CreateProjectAsync(source);
        var workspaceMock = CreateMockWorkspaceManager(project);
        var analyzer = new DependencyInjectionAnalyzer(
            NullLogger<DependencyInjectionAnalyzer>.Instance,
            Microsoft.Extensions.Options.Options.Create(
                new DotNetAnalyzer.Core.Configuration.AnalyzerOptions()));

        var json = await DesktopTools.AnalyzeDiRegistration(
            workspaceMock.Object, analyzer, "/Test.csproj");

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("data")
            .GetProperty("credibility")
            .GetProperty("level")
            .GetString().Should().Be("Verified");
    }

    #endregion
}
