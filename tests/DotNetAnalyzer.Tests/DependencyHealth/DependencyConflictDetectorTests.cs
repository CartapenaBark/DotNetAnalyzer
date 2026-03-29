using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.DependencyHealth;
using DotNetAnalyzer.Core.DependencyHealth.Models;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetAnalyzer.Tests.DependencyHealth;

public class DependencyConflictDetectorTests : IDisposable
{
    private readonly Mock<IWorkspaceManager> _workspaceManagerMock = new();
    private readonly Mock<ILogger<DependencyConflictDetector>> _loggerMock = new();
    private readonly string _tempDir;

    public DependencyConflictDetectorTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "DotNetAnalyzer_Tests_Conflict_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private DependencyConflictDetector CreateDetector()
    {
        return new DependencyConflictDetector(
            _workspaceManagerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DetectConflictsAsync_WithConflictingVersions_ReturnsConflicts()
    {
        // Arrange - create real .csproj files with conflicting package versions
        var projectA = CreateProjectFile("A", ["Newtonsoft.Json/13.0.3"]);
        var projectB = CreateProjectFile("B", ["Newtonsoft.Json/12.0.3"]);
        var slnPath = Path.Combine(_tempDir, "Test.sln");

        var solution = CreateSolutionWithFilePaths(projectA, projectB);

        _workspaceManagerMock.Setup(w => w.GetSolutionAsync(slnPath))
            .ReturnsAsync(solution);

        var detector = CreateDetector();

        // Act
        var report = await detector.DetectConflictsAsync(slnPath);

        // Assert
        report.Conflicts.Should().HaveCount(1);
        report.Conflicts[0].PackageId.Should().Be("Newtonsoft.Json");
        report.Conflicts[0].SuggestedVersion.Should().Be("13.0.3");
        report.TotalConflicts.Should().Be(1);
    }

    [Fact]
    public async Task DetectConflictsAsync_WithConsistentVersions_ReturnsNoConflicts()
    {
        // Arrange
        var projectA = CreateProjectFile("A", ["Newtonsoft.Json/13.0.3"]);
        var projectB = CreateProjectFile("B", ["Newtonsoft.Json/13.0.3"]);
        var slnPath = Path.Combine(_tempDir, "Test.sln");

        var solution = CreateSolutionWithFilePaths(projectA, projectB);

        _workspaceManagerMock.Setup(w => w.GetSolutionAsync(slnPath))
            .ReturnsAsync(solution);

        var detector = CreateDetector();

        // Act
        var report = await detector.DetectConflictsAsync(slnPath);

        // Assert
        report.Conflicts.Should().BeEmpty();
        report.TotalConflicts.Should().Be(0);
    }

    [Fact]
    public async Task DetectConflictsAsync_WhenSolutionLoadFails_ReturnsEmptyReport()
    {
        // Arrange
        _workspaceManagerMock.Setup(w => w.GetSolutionAsync("/test/Test.sln"))
            .ThrowsAsync(new Exception("Load failed"));

        var detector = CreateDetector();

        // Act
        var report = await detector.DetectConflictsAsync("/test/Test.sln");

        // Assert
        report.Conflicts.Should().BeEmpty();
        report.SolutionPath.Should().Be("/test/Test.sln");
    }

    [Fact]
    public async Task DetectConflictsAsync_WithThreeWayVersionConflict_ReturnsHighestAsSuggested()
    {
        // Arrange
        var projectA = CreateProjectFile("A", ["Lib/1.0.0"]);
        var projectB = CreateProjectFile("B", ["Lib/2.0.0"]);
        var projectC = CreateProjectFile("C", ["Lib/1.5.0"]);
        var slnPath = Path.Combine(_tempDir, "Test.sln");

        var solution = CreateSolutionWithFilePaths(projectA, projectB, projectC);

        _workspaceManagerMock.Setup(w => w.GetSolutionAsync(slnPath))
            .ReturnsAsync(solution);

        var detector = CreateDetector();

        // Act
        var report = await detector.DetectConflictsAsync(slnPath);

        // Assert
        report.Conflicts.Should().HaveCount(1);
        report.Conflicts[0].Versions.Should().HaveCount(3);
        report.Conflicts[0].SuggestedVersion.Should().Be("2.0.0");
    }

    /// <summary>
    /// Creates a real .csproj file with the given package references.
    /// </summary>
    private string CreateProjectFile(string name, List<string> packages)
    {
        var projectDir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(projectDir);

        var packageRefs = string.Join(
            Environment.NewLine,
            packages.Select(p =>
            {
                var parts = p.Split('/');
                return $@"    <PackageReference Include=""{parts[0]}"" Version=""{parts[1]}"" />";
            }));

        var csproj = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
{packageRefs}
  </ItemGroup>
</Project>";

        var csprojPath = Path.Combine(projectDir, $"{name}.csproj");
        File.WriteAllText(csprojPath, csproj);
        return csprojPath;
    }

    /// <summary>
    /// Creates a Solution using AdhocWorkspace so that Project.FilePath
    /// returns real file paths that DependencyConflictDetector can read.
    /// </summary>
    private static Solution CreateSolutionWithFilePaths(params string[] projectPaths)
    {
        using var workspace = new AdhocWorkspace();

        foreach (var path in projectPaths)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            workspace.AddProject(
                ProjectInfo.Create(
                    ProjectId.CreateNewId(),
                    VersionStamp.Create(),
                    name,
                    name,
                    LanguageNames.CSharp,
                    filePath: path));
        }

        return workspace.CurrentSolution;
    }
}
