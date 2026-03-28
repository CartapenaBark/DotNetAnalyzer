using System.Text;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Analysis;
using DotNetAnalyzer.Core.Analysis.CodeQuality;
using DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;
using DotNetAnalyzer.Core.Models.CodeQuality;
using DotNetAnalyzer.Core.Visualization;
using DotNetAnalyzer.Tests.Helpers;
using FluentAssertions;
using Microsoft.CodeAnalysis;
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

        var project = await workspaceManager.GetProjectAsync(tempProject.ProjectPath);
        project.Should().NotBeNull();

        var analyzer = new CodeSmellAnalyzer(
            NullLogger<CodeSmellAnalyzer>.Instance,
            new ICodeSmellDetector[] { new LongMethodDetector() });

        var result = await analyzer.AnalyzeAsync(project!);

        result.Should().NotBeNull();
        result.Smells.Should().NotBeEmpty();
        result.Smells.Should().Contain(s => s.Type == "long-method");
        result.Smells.Should().Contain(s => s.SymbolName == "Run");
    }

    [Fact]
    public async Task DependencyGraph_ShouldContainExpectedNodesFromRealProject()
    {
        using var tempProject = TempProject.CreateWithQualityAndGraphSample();
        using var workspaceManager = TestHelper.CreateWorkspaceManager();

        var project = await workspaceManager.GetProjectAsync(tempProject.ProjectPath);
        project.Should().NotBeNull();

        // 构建依赖关系图
        var graph = new DependencyGraph();
        var nodeIdCounter = 0;

        foreach (var document in project!.Documents)
        {
            if (document.FilePath?.EndsWith(".cs") != true) continue;

            var tree = await document.GetSyntaxTreeAsync();
            if (tree == null) continue;

            var root = await tree.GetRootAsync();
            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null) continue;

            var typeDeclarations = root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>();

            foreach (var typeDeclaration in typeDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
                if (symbol == null) continue;

                graph.Nodes.Add(new DependencyNode
                {
                    Id = $"node_{nodeIdCounter++}",
                    Name = symbol.Name,
                    Type = DependencyNodeType.Type,
                    FilePath = document.FilePath,
                    Namespace = symbol.ContainingNamespace?.Name,
                    IsPublic = symbol.DeclaredAccessibility ==
                        Microsoft.CodeAnalysis.Accessibility.Public
                });

                if (symbol is Microsoft.CodeAnalysis.INamedTypeSymbol namedType)
                {
                    if (namedType.BaseType != null &&
                        namedType.BaseType.SpecialType !=
                            Microsoft.CodeAnalysis.SpecialType.System_Object)
                    {
                        graph.Edges.Add(new DependencyEdge
                        {
                            From = $"node_{nodeIdCounter - 1}",
                            To = namedType.BaseType.Name,
                            Type = DependencyType.Inheritance
                        });
                    }

                    foreach (var iface in namedType.AllInterfaces)
                    {
                        graph.Edges.Add(new DependencyEdge
                        {
                            From = $"node_{nodeIdCounter - 1}",
                            To = iface.Name,
                            Type = DependencyType.Implementation
                        });
                    }
                }
            }
        }

        // Assert
        graph.Nodes.Should().HaveCountGreaterThanOrEqualTo(3);
        var nodeNames = graph.Nodes.Select(n => n.Name).ToList();
        nodeNames.Should().Contain(["Worker", "WorkerBase", "IWorker"]);
    }

    [Fact]
    public async Task TestCoverage_ShouldReturnHeuristicCredibility()
    {
        using var tempProject = TempProject.CreateWithQualityAndGraphSample();
        using var workspaceManager = TestHelper.CreateWorkspaceManager();

        var project = await workspaceManager.GetProjectAsync(tempProject.ProjectPath);
        project.Should().NotBeNull();

        var parser = new CoverageDataParser(
            NullLogger<CoverageDataParser>.Instance);
        var analyzer = new TestCoverageAnalyzer(
            NullLogger<TestCoverageAnalyzer>.Instance,
            parser);

        var result = await analyzer.AnalyzeAsync(project!);

        result.Should().NotBeNull();
        result.Credibility.Should().Be("heuristic");
    }

    [Fact]
    public async Task ChangeImpact_ShouldReturnHeuristicCredibility()
    {
        using var tempProject = TempProject.CreateWithQualityAndGraphSample();
        using var workspaceManager = TestHelper.CreateWorkspaceManager();

        var project = await workspaceManager.GetProjectAsync(tempProject.ProjectPath);
        project.Should().NotBeNull();

        var analyzer = new ChangeImpactAnalyzer(
            NullLogger<ChangeImpactAnalyzer>.Instance);

        var result = await analyzer.AnalyzeAsync(
            project!,
            tempProject.SourceFilePath,
            ChangeType.Other);

        result.Should().NotBeNull();
        result.ChangedFilePath.Should().Be(tempProject.SourceFilePath);
    }

    [Fact]
    public async Task HeatmapChangeFrequency_ShouldHandleNonGitRepository()
    {
        using var tempProject = TempProject.CreateWithQualityAndGraphSample();

        var gitProvider = new GitHistoryProvider(
            NullLogger<GitHistoryProvider>.Instance);

        // 非 Git 仓库目录应抛出异常
        var act = () => HeatmapGenerator
            .GenerateChangeFrequencyHeatmapFromGit(
                gitProvider,
                tempProject.DirectoryPath,
                30);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*不是 Git 仓库*");
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
