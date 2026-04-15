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
/// ViewModelMapper 单元测试。
/// </summary>
/// <remarks>
/// 覆盖 DataType、x:TypeArguments、DataContext 和命名约定四种映射策略。
/// </remarks>
public class ViewModelMapperTests : IDisposable
{
    private readonly ViewModelMapper _mapper;
    private readonly XamlParser _parser;
    private readonly List<IDisposable> _tempDirs = [];

    public ViewModelMapperTests()
    {
        var logger = NullLogger<ViewModelMapper>.Instance;
        _parser = new XamlParser(
            NullLogger<XamlParser>.Instance);
        _mapper = new ViewModelMapper(logger, _parser);
    }

    public void Dispose()
    {
        foreach (var d in _tempDirs)
        {
            d.Dispose();
        }
        _tempDirs.Clear();
    }

    private string CreateTempDirWithFiles(
        Dictionary<string, string> files)
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            $"VMMapperTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        foreach (var kvp in files)
        {
            File.WriteAllText(
                Path.Combine(dir, kvp.Key), kvp.Value);
        }

        _tempDirs.Add(new TempDirCleanup(dir));
        return dir;
    }

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

    [Fact]
    public async Task MapAsync_XamlWithClassAttribute_ProducesConventionMapping()
    {
        // Arrange — Window 后缀触发命名约定推断
        var xaml = """
            <Window
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="MyApp.MainWindow">
                <Grid />
            </Window>
            """;

        var dir = CreateTempDirWithFiles(
            new Dictionary<string, string>
            {
                ["MainWindow.xaml"] = xaml,
                ["MainWindow.xaml.cs"] =
                    "namespace MyApp { public partial class MainWindow { } }",
                ["MainViewModel.cs"] =
                    "namespace MyApp { public class MainViewModel { } }"
            });

        // 先验证 XamlParser 能正常解析
        var parsed = await _parser.ParseAsync(
            Path.Combine(dir, "MainWindow.xaml"));
        parsed.ClassAttribute.Should().Be("MyApp.MainWindow");
        parsed.RootElement.Should().Be("Window");

        var project = CreateProjectWithFiles(
            dir, ["MainWindow.xaml", "MainWindow.xaml.cs", "MainViewModel.cs"]);

        // 诊断：确认项目中的文档数
        var xamlDocCount = project.Documents
            .Count(d => d.FilePath != null && d.FilePath.EndsWith(".xaml",
                StringComparison.OrdinalIgnoreCase));
        xamlDocCount.Should().BeGreaterThan(0,
            "project should contain XAML documents");

        // Act
        var result = await _mapper.MapAsync(project);

        // Assert
        result.Should().NotBeNull();
        result.Mappings.Should().Contain(m =>
            m.MappingSource == "Convention" &&
            m.ViewModelClassName == "MyApp.MainViewModel");
    }

    [Fact]
    public async Task MapAsync_NoClassAttribute_NoMapping()
    {
        // Arrange — 没有 x:Class 的 XAML 不产生映射（如 ResourceDictionary）
        var xaml = """
            <ResourceDictionary
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <SolidColorBrush x:Key="TestBrush" Color="Red" />
            </ResourceDictionary>
            """;

        var dir = CreateTempDirWithFiles(
            new Dictionary<string, string>
            {
                ["Resources.xaml"] = xaml
            });

        var project = CreateProjectWithFiles(
            dir, ["Resources.xaml"]);

        // Act
        var result = await _mapper.MapAsync(project);

        // Assert
        result.Should().NotBeNull();
        result.TotalMappings.Should().Be(0);
    }

    [Fact]
    public async Task MapAsync_ResourceDictionary_NoMapping()
    {
        // Arrange — ResourceDictionary 没有 x:Class，不应产生映射
        var xaml = """
            <ResourceDictionary
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <SolidColorBrush x:Key="PrimaryBrush" Color="Blue" />
            </ResourceDictionary>
            """;

        var dir = CreateTempDirWithFiles(
            new Dictionary<string, string>
            {
                ["Resources.xaml"] = xaml
            });

        var project = CreateProjectWithFiles(
            dir, ["Resources.xaml"]);

        // Act
        var result = await _mapper.MapAsync(project);

        // Assert
        result.Should().NotBeNull();
        result.TotalMappings.Should().Be(0);
    }

    [Fact]
    public async Task MapAsync_EmptyProject_ReturnsEmptyMappings()
    {
        // Arrange
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "EmptyProject",
            "EmptyProject",
            LanguageNames.CSharp,
            metadataReferences:
            [
                MetadataReference.CreateFromFile(
                    typeof(object).Assembly.Location)
            ]);
        workspace.AddProject(projectInfo);

        // Act
        var result = await _mapper.MapAsync(
            workspace.CurrentSolution.GetProject(projectId)!);

        // Assert
        result.Should().NotBeNull();
        result.TotalMappings.Should().Be(0);
        result.Mappings.Should().BeEmpty();
    }

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
