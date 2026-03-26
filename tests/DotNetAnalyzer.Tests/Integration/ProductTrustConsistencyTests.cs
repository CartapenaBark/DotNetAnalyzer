using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using DotNetAnalyzer.Tests.Helpers;
using Xunit;

namespace DotNetAnalyzer.Tests.Integration;

public partial class ProductTrustConsistencyTests
{
    private readonly string _repoRoot = TestHelper.GetRepositoryRoot();

    [Fact]
    public void ProductMetadata_ShouldStayAlignedAcrossDocsAndRuntimeEntryPoints()
    {
        var metadataPath = Path.Combine(_repoRoot, "eng", "product-metadata.json");
        var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath)).RootElement;
        var version = metadata.GetProperty("currentVersion").GetString()!;
        var packageId = metadata.GetProperty("packageId").GetString()!;
        var toolCommandName = metadata.GetProperty("toolCommandName").GetString()!;
        var repositoryUrl = metadata.GetProperty("repositoryUrl").GetString()!;
        var packageUrl = metadata.GetProperty("packageUrl").GetString()!;
        var toolCount = metadata.GetProperty("toolCount").GetInt32();

        var readme = File.ReadAllText(Path.Combine(_repoRoot, "README.md"));
        readme.Should().Contain($"当前版本 (v{version})");
        readme.Should().Contain($"**{toolCount} 个 MCP 工具**");
        readme.Should().Contain(packageUrl);

        var apiGuide = File.ReadAllText(Path.Combine(_repoRoot, "docs", "api-guide.md"));
        apiGuide.Should().Contain($"DotNetAnalyzer v{version}");
        apiGuide.Should().Contain($"**{toolCount} 个 MCP 工具**");

        var workflowDoc = File.ReadAllText(Path.Combine(_repoRoot, "docs", "development-workflow.md"));
        workflowDoc.Should().Contain("eng/product-metadata.json");

        var programSource = File.ReadAllText(Path.Combine(_repoRoot, "src", "DotNetAnalyzer.Cli", "Program.cs"));
        programSource.Should().Contain(repositoryUrl);
        programSource.Should().NotContain("yourusername");

        var cliProject = XDocument.Load(Path.Combine(_repoRoot, "src", "DotNetAnalyzer.Cli", "DotNetAnalyzer.Cli.csproj"));
        cliProject.Root.Should().NotBeNull();
        GetProperty(cliProject, "PackageId").Should().Be(packageId);
        GetProperty(cliProject, "ToolCommandName").Should().Be(toolCommandName);
        GetProperty(cliProject, "Version").Should().Be(version);
        GetProperty(cliProject, "RepositoryUrl").Should().Be(repositoryUrl);
        GetProperty(cliProject, "PackageProjectUrl").Should().Be(repositoryUrl);
        GetProperty(cliProject, "Description").Should().Contain($"{toolCount} tools");
    }

    [Fact]
    public void MaintainedDocuments_ShouldNotContainPlaceholderValuesOrBrokenRelativeLinks()
    {
        var metadataPath = Path.Combine(_repoRoot, "eng", "product-metadata.json");
        var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath)).RootElement;
        var docs = metadata.GetProperty("maintainedDocuments")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();

        foreach (var relativePath in docs)
        {
            var documentPath = Path.Combine(_repoRoot, relativePath);
            var content = File.ReadAllText(documentPath);

            content.Should().NotContain("yourusername");
            content.Should().NotContain("claude-cn.org");

            foreach (Match match in MarkdownLinkRegex().Matches(content))
            {
                var target = match.Groups["target"].Value;
                if (string.IsNullOrWhiteSpace(target) ||
                    target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var resolvedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(documentPath)!, target));
                File.Exists(resolvedPath).Should().BeTrue($"链接 {target} 应指向存在的文件");
            }
        }
    }

    private static string? GetProperty(XDocument document, string propertyName)
    {
        return document.Descendants().FirstOrDefault(element => element.Name.LocalName == propertyName)?.Value;
    }

    [GeneratedRegex(@"\[[^\]]+\]\((?<target>[^)#]+)(#[^)]+)?\)")]
    private static partial Regex MarkdownLinkRegex();

}
