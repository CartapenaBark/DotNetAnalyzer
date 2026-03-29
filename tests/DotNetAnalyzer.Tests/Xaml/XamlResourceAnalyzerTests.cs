using DotNetAnalyzer.Core.Xaml;
using DotNetAnalyzer.Core.Xaml.Models;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.Xaml;

/// <summary>
/// XamlResourceAnalyzer 单元测试。
/// </summary>
/// <remarks>
/// 覆盖资源定义收集、MergedDictionaries 和缺失资源检测。
/// 测试通过创建临时 XAML 文件和 AdhocWorkspace 项目来验证分析器行为。
/// </remarks>
public class XamlResourceAnalyzerTests : IDisposable
{
    private readonly XamlResourceAnalyzer _analyzer;
    private readonly List<IDisposable> _tempDirs = [];

    public XamlResourceAnalyzerTests()
    {
        var logger = NullLogger<XamlResourceAnalyzer>.Instance;
        var parser = new XamlParser(
            NullLoggerFactory.Instance.CreateLogger<XamlParser>());
        _analyzer = new XamlResourceAnalyzer(logger, parser);
    }

    public void Dispose()
    {
        foreach (var d in _tempDirs)
        {
            d.Dispose();
        }
        _tempDirs.Clear();
    }

    #region 辅助方法

    /// <summary>
    /// 创建临时目录并写入 XAML 文件，返回目录路径。
    /// </summary>
    private string CreateTempDirWithXaml(
        Dictionary<string, string> files)
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            $"XamlResTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        foreach (var kvp in files)
        {
            File.WriteAllText(
                Path.Combine(dir, kvp.Key), kvp.Value);
        }

        _tempDirs.Add(new TempDirCleanup(dir));
        return dir;
    }

    /// <summary>
    /// 创建一个 AdhocWorkspace 项目，将指定目录下的文件作为文档添加进去。
    /// </summary>
    private static Project CreateProjectWithFiles(
        string baseDir,
        IEnumerable<string> fileNames)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
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

        var docIdCounter = 0;
        foreach (var fileName in fileNames)
        {
            var fullPath = Path.Combine(baseDir, fileName);
            var content = File.ReadAllText(fullPath);
            var documentId = DocumentId.CreateNewId(
                projectId, $"doc{docIdCounter++}");

            var documentInfo = DocumentInfo.Create(
                documentId,
                fileName,
                filePath: fullPath,
                loader: TextLoader.From(TextAndVersion.Create(
                    SourceText.From(content),
                    versionStamp)));

            workspace.AddDocument(documentInfo);
        }

        return workspace.CurrentSolution.GetProject(projectId)!;
    }

    #endregion

    [Fact]
    public async Task AnalyzeAsync_SingleResourceDictionary_ReturnsResources()
    {
        // Arrange — 单个 ResourceDictionary 中定义两个资源
        var xaml = """
            <ResourceDictionary
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <SolidColorBrush x:Key="PrimaryBrush" Color="Blue" />
                <SolidColorBrush x:Key="SecondaryBrush" Color="Red" />
            </ResourceDictionary>
            """;

        var dir = CreateTempDirWithXaml(
            new Dictionary<string, string>
            {
                ["App.xaml"] = xaml
            });

        var project = CreateProjectWithFiles(
            dir, ["App.xaml"]);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.Should().NotBeNull();
        result.TotalDefinedResources.Should().Be(2);
        result.DefinedResources.Should().Contain(r =>
            r.Key == "PrimaryBrush" &&
            r.ResourceType == "SolidColorBrush");
        result.DefinedResources.Should().Contain(r =>
            r.Key == "SecondaryBrush" &&
            r.ResourceType == "SolidColorBrush");
    }

    [Fact]
    public async Task
        AnalyzeAsync_MergedResourceDictionaries_ReturnsMerged()
    {
        // Arrange — 一个主 XAML 引用资源，另一个定义资源
        var resourcesXaml = """
            <ResourceDictionary
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <SolidColorBrush x:Key="ThemeBrush" Color="Green" />
            </ResourceDictionary>
            """;

        var windowXaml = """
            <Window
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <ResourceDictionary>
                        <ResourceDictionary.MergedDictionaries>
                            <ResourceDictionary Source="Resources.xaml" />
                        </ResourceDictionary.MergedDictionaries>
                    </ResourceDictionary>
                </Window.Resources>
                <Grid Background="{StaticResource ThemeBrush}" />
            </Window>
            """;

        var dir = CreateTempDirWithXaml(
            new Dictionary<string, string>
            {
                ["Resources.xaml"] = resourcesXaml,
                ["MainWindow.xaml"] = windowXaml
            });

        var project = CreateProjectWithFiles(
            dir, ["Resources.xaml", "MainWindow.xaml"]);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.Should().NotBeNull();
        result.TotalDefinedResources.Should().Be(1);
        result.DefinedResources[0].Key.Should().Be("ThemeBrush");

        // StaticResource ThemeBrush 引用应该被解析为已定义
        var themeRef = result.References.Should()
            .Contain(r => r.Key == "ThemeBrush").Subject;
        themeRef.IsLocallyDefined.Should().BeTrue();
    }

    [Fact]
    public async Task
        AnalyzeAsync_MissingResource_DetectsBrokenReference()
    {
        // Arrange — 引用了一个不存在的资源
        var xaml = """
            <Window
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Grid Background="{StaticResource NonExistentBrush}">
                    <TextBlock
                        Foreground="{DynamicResource AnotherMissing}" />
                </Grid>
            </Window>
            """;

        var dir = CreateTempDirWithXaml(
            new Dictionary<string, string>
            {
                ["BrokenWindow.xaml"] = xaml
            });

        var project = CreateProjectWithFiles(
            dir, ["BrokenWindow.xaml"]);

        // Act
        var result = await _analyzer.AnalyzeAsync(project);

        // Assert
        result.Should().NotBeNull();
        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i =>
            i.IssueType == "MissingResource" &&
            i.Key == "NonExistentBrush" &&
            i.Severity == "Error");
        result.Issues.Should().Contain(i =>
            i.IssueType == "MissingResource" &&
            i.Key == "AnotherMissing" &&
            i.Severity == "Error");
    }

    /// <summary>
    /// 临时目录清理辅助类。
    /// </summary>
    private sealed class TempDirCleanup : IDisposable
    {
        private readonly string _directory;

        public TempDirCleanup(string directory)
        {
            _directory = directory;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }
            }
            catch
            {
                // 忽略清理失败，避免影响测试结果
            }
        }
    }
}
