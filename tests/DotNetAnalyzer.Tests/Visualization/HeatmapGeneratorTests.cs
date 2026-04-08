using DotNetAnalyzer.Core.Analysis;
using DotNetAnalyzer.Core.Visualization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.Visualization;

public class HeatmapGeneratorTests
{
    private readonly GitHistoryProvider _gitProvider;

    public HeatmapGeneratorTests()
    {
        _gitProvider = new GitHistoryProvider(
            NullLogger<GitHistoryProvider>.Instance);
    }

    [Fact]
    public async Task
        GenerateChangeFrequencyHeatmapFromGit_ShouldReturnPopulatedData()
    {
        var repoRoot = GetRepoRoot();
        var data = await HeatmapGenerator
            .GenerateChangeFrequencyHeatmapFromGit(
                _gitProvider, repoRoot, 30);

        data.Should().NotBeNull();
        data.Title.Should().Contain("Change frequency heatmap");
        data.Type.Should().Be(HeatmapType.ChangeFrequency);
        data.Cells.Should().NotBeEmpty();
        data.MinValue.Should().BeGreaterThan(0);
        data.MaxValue.Should().BeGreaterThanOrEqualTo(data.MinValue);
    }

    [Fact]
    public async Task
        GenerateChangeFrequencyHeatmapFromGit_CellsShouldHaveMetadata()
    {
        var repoRoot = GetRepoRoot();
        var data = await HeatmapGenerator
            .GenerateChangeFrequencyHeatmapFromGit(
                _gitProvider, repoRoot, 30);

        foreach (var cell in data.Cells)
        {
            cell.Label.Should().NotBeNullOrEmpty();
            cell.Value.Should().BeGreaterThan(0);
            cell.Color.Should().NotBeNullOrEmpty();
            cell.Tooltip.Should().Contain("changes");
            cell.Metadata.Should().ContainKey("filePath");
            cell.Metadata.Should().ContainKey("changeCount");
            cell.Metadata["changeCount"].Should().Be(cell.Value);
        }
    }

    [Fact]
    public async Task
        GenerateChangeFrequencyHeatmapFromGit_MermaidChart_ShouldBeWellFormed()
    {
        var repoRoot = GetRepoRoot();
        var data = await HeatmapGenerator
            .GenerateChangeFrequencyHeatmapFromGit(
                _gitProvider, repoRoot, 30);
        var mermaid = HeatmapGenerator.GenerateMermaidChart(data);

        mermaid.Should().StartWith("```mermaid");
        mermaid.Should().Contain("xychart-beta");
        mermaid.Should().Contain("x-axis");
        mermaid.Should().Contain("y-axis");
        mermaid.Should().Contain("bar");
        mermaid.Should().Contain("```");
    }

    [Fact]
    public async Task
        GenerateChangeFrequencyHeatmapFromGit_JsonOutput_ShouldBeValid()
    {
        var repoRoot = GetRepoRoot();
        var data = await HeatmapGenerator
            .GenerateChangeFrequencyHeatmapFromGit(
                _gitProvider, repoRoot, 30);
        var json = HeatmapGenerator.GenerateJsonData(data);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"title\"");
        json.Should().Contain("\"ChangeFrequency\"");
        json.Should().Contain("\"cells\"");
        json.Should().Contain("\"minValue\"");
        json.Should().Contain("\"maxValue\"");
    }

    [Fact]
    public async Task
        GenerateChangeFrequencyHeatmapFromGit_ColorsShouldFollowHeatScale()
    {
        var repoRoot = GetRepoRoot();
        var data = await HeatmapGenerator
            .GenerateChangeFrequencyHeatmapFromGit(
                _gitProvider, repoRoot, 90);

        var validColors = new[]
        {
            "#90EE90", "#FFD700", "#FFA500", "#FF6347"
        };

        foreach (var cell in data.Cells)
        {
            cell.Color.Should().BeOneOf(validColors);
        }
    }

    [Fact]
    public async Task
        GenerateChangeFrequencyHeatmapFromGit_90Days_ShouldHaveMoreRecordsThan1Day()
    {
        var repoRoot = GetRepoRoot();
        var data90 = await HeatmapGenerator
            .GenerateChangeFrequencyHeatmapFromGit(
                _gitProvider, repoRoot, 90);
        var data1 = await HeatmapGenerator
            .GenerateChangeFrequencyHeatmapFromGit(
                _gitProvider, repoRoot, 1);

        data90.Cells.Count.Should().BeGreaterThanOrEqualTo(data1.Cells.Count);
    }

    [Fact]
    public async Task
        GenerateChangeFrequencyHeatmapFromGit_WithNullProvider_ShouldThrow()
    {
        var act = () => HeatmapGenerator
            .GenerateChangeFrequencyHeatmapFromGit(
                null!, "/some/path", 30);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void GenerateComplexityHeatmap_WithEmptyData_ShouldReturnEmptyCells()
    {
        // 空集合的边界情况
        var emptyCollection = new DotNetAnalyzer.Core.Models.CodeQuality.CodeSmellCollection();
        var data = HeatmapGenerator.GenerateComplexityHeatmap(emptyCollection);

        data.Should().NotBeNull();
        data.Cells.Should().BeEmpty();
        data.MinValue.Should().Be(0);
        data.MaxValue.Should().Be(100);
    }

    private static string GetRepoRoot()
    {
        var directory = Directory.GetCurrentDirectory();
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory, ".git")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

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

        throw new InvalidOperationException("无法找到 Git 仓库根目录。");
    }
}
