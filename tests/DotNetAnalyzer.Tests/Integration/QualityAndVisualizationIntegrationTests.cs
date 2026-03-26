using System.Text;
using System.Text.Json;
using DotNetAnalyzer.Cli.Tools;
using DotNetAnalyzer.Core.Analysis.CodeQuality;
using DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;
using DotNetAnalyzer.Core.Visualization;
using DotNetAnalyzer.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.Integration;

public class QualityAndVisualizationIntegrationTests
{
    [Fact]
    public async Task DetectCodeSmells_ShouldReturnStableSmellShapeFromRealProject()
    {
        using var tempProject = TempProject.CreateWithQualityAndGraphSample();
        using var workspaceManager = TestHelper.CreateWorkspaceManager();

        var analyzer = new CodeSmellAnalyzer(
            NullLogger<CodeSmellAnalyzer>.Instance,
            new ICodeSmellDetector[] { new LongMethodDetector() });

        var response = await CodeQualityTools.DetectCodeSmells(
            workspaceManager,
            analyzer,
            tempProject.ProjectPath,
            smellType: "long-method");

        using var document = JsonDocument.Parse(response);
        var data = document.RootElement.GetProperty("data");
        data.GetProperty("summary").GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        data.GetProperty("smells")[0].GetProperty("type").GetString().Should().Be("long-method");
        data.GetProperty("smells")[0].GetProperty("symbolName").GetString().Should().Be("Run");
    }

    [Fact]
    public async Task GenerateDependencyGraph_ShouldReturnStableNodeAndEdgeStructure()
    {
        using var tempProject = TempProject.CreateWithQualityAndGraphSample();
        using var workspaceManager = TestHelper.CreateWorkspaceManager();

        var response = await VisualizationTools.GenerateDependencyGraph(
            workspaceManager,
            NullLogger<DependencyGraphVisualizer>.Instance,
            tempProject.ProjectPath,
            format: "json");

        using var outer = JsonDocument.Parse(response);
        outer.RootElement.GetProperty("nodeCount").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        outer.RootElement.GetProperty("edgeCount").GetInt32().Should().BeGreaterThanOrEqualTo(2);

        using var inner = JsonDocument.Parse(outer.RootElement.GetProperty("data").GetString()!);
        var nodeNames = inner.RootElement.GetProperty("nodes").EnumerateArray().Select(node => node.GetProperty("name").GetString()).ToArray();
        nodeNames.Should().Contain(["Worker", "WorkerBase", "IWorker"]);
    }

    [Fact]
    public async Task LowCredibilityCapabilities_ShouldReturnCredibilityMetadata()
    {
        using var tempProject = TempProject.CreateWithQualityAndGraphSample();
        using var workspaceManager = TestHelper.CreateWorkspaceManager();

        var coverageResponse = await AnalysisTools.GetTestCoverage(workspaceManager, tempProject.ProjectPath);
        using var coverageJson = JsonDocument.Parse(coverageResponse);
        coverageJson.RootElement.GetProperty("credibility").GetProperty("level").GetString().Should().Be("heuristic");

        var impactResponse = await MonitoringTools.AnalyzeChangeImpact(
            workspaceManager,
            NullLogger<ChangeImpactAnalyzer>.Instance,
            tempProject.ProjectPath,
            tempProject.SourceFilePath);
        using var impactJson = JsonDocument.Parse(impactResponse);
        impactJson.RootElement.GetProperty("credibility").GetProperty("level").GetString().Should().Be("heuristic");

        var heatmapResponse = await VisualizationTools.GenerateHeatmap(
            workspaceManager,
            NullLogger<HeatmapGenerator>.Instance,
            NullLogger<CodeSmellAnalyzer>.Instance,
            Array.Empty<ICodeSmellDetector>(),
            tempProject.ProjectPath,
            heatmapType: "change-frequency",
            format: "json");
        using var heatmapJson = JsonDocument.Parse(heatmapResponse);
        heatmapJson.RootElement.GetProperty("credibility").GetProperty("level").GetString().Should().Be("experimental");
    }

    private sealed class TempProject : IDisposable
    {
        private TempProject(string directory, string projectPath, string sourceFilePath)
        {
            DirectoryPath = directory;
            ProjectPath = projectPath;
            SourceFilePath = sourceFilePath;
        }

        public string DirectoryPath { get; }
        public string ProjectPath { get; }
        public string SourceFilePath { get; }

        public static TempProject CreateWithQualityAndGraphSample()
        {
            var directory = Path.Combine(Path.GetTempPath(), "dotnet-analyzer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            var projectPath = Path.Combine(directory, "SampleProject.csproj");
            File.WriteAllText(projectPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""");

            var longMethodBody = string.Join(Environment.NewLine, Enumerable.Range(0, 60).Select(i => $"        total += {i};"));
            var source = $$"""
namespace Sample;

public interface IWorker
{
    void Run();
}

public abstract class WorkerBase
{
    public abstract void Run();
}

public sealed class Worker : WorkerBase, IWorker
{
    public override void Run()
    {
        var total = 0;
{{longMethodBody}}
        System.Console.WriteLine(total);
    }
}
""";

            var sourceFilePath = Path.Combine(directory, "Worker.cs");
            File.WriteAllText(sourceFilePath, source, Encoding.UTF8);

            return new TempProject(directory, projectPath, sourceFilePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
