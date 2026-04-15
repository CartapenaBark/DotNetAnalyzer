using DotNetAnalyzer.Core.Xaml;
using DotNetAnalyzer.Core.Xaml.Models;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetAnalyzer.Tests.Xaml;

/// <summary>
/// ViewModelMapper 精确化测试。
/// </summary>
/// <remarks>
/// 覆盖 SyntaxWalker 精确分析、Convention 存在性验证、
/// FindTypeBySimpleName 多级命名空间搜索、clr-namespace URI 解析。
/// </remarks>
public class ViewModelMapperPrecisionTests : IDisposable
{
    private readonly XamlParser _parser = new(
        new LoggerFactory().CreateLogger<XamlParser>());
    private readonly ViewModelMapper _mapper;

    public ViewModelMapperPrecisionTests()
    {
        _mapper = new ViewModelMapper(
            new LoggerFactory().CreateLogger<ViewModelMapper>(),
            _parser);
    }

    public void Dispose()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DotNetAnalyzerTests");
        if (Directory.Exists(tempDir))
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // 忽略清理失败
            }
        }
    }

    private static string CreateTempDirWithFiles(
        Dictionary<string, string> files)
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "DotNetAnalyzerTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        foreach (var file in files)
        {
            File.WriteAllText(
                Path.Combine(dir, file.Key), file.Value);
        }

        return dir;
    }

    [Fact]
    public async Task
        TryMapByConvention_TypeDoesNotExist_NoMapping()
    {
        var xaml = """
            <UserControl
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="MyApp.FooView">
            </UserControl>
            """;

        var dir = CreateTempDirWithFiles(
            new Dictionary<string, string>
            {
                ["FooView.xaml"] = xaml,
                ["FooView.xaml.cs"] =
                    "namespace MyApp { public partial class FooView { } }",
            });

        var project = CreateProjectWithFiles(
            dir, ["FooView.xaml", "FooView.xaml.cs"]);
        var result = await _mapper.MapAsync(project);

        result.Should().NotBeNull();
        result.Mappings.Should().BeEmpty();
    }

    [Fact]
    public async Task
        FindTypeBySimpleName_MultiLevelNamespace_FindsType()
    {
        // Convention 策略从 MyApp.Views.OrderView 推断
        // MyApp.Views.OrderViewModel，验证类型存在时产出映射
        var xaml = """
            <UserControl
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="MyApp.Views.OrderView">
            </UserControl>
            """;

        var dir = CreateTempDirWithFiles(
            new Dictionary<string, string>
            {
                ["OrderView.xaml"] = xaml,
                ["OrderView.xaml.cs"] =
                    "namespace MyApp.Views { " +
                    "public partial class OrderView { } }",
                ["OrderViewModel.cs"] =
                    "namespace MyApp.Views { " +
                    "public class OrderViewModel { } }"
            });

        var project = CreateProjectWithFiles(
            dir,
            ["OrderView.xaml", "OrderView.xaml.cs", "OrderViewModel.cs"]);
        var result = await _mapper.MapAsync(project);

        result.Should().NotBeNull();
        result.Mappings.Should().Contain(m =>
            m.MappingSource == "Convention" &&
            m.ViewModelClassName == "MyApp.Views.OrderViewModel");
    }

    [Fact]
    public async Task
        TryMapFromCodeBehind_NoCodeBehindFile_FallsThrough()
    {
        // 没有 code-behind 文件时，策略 3 跳过，
        // 回退到 Convention 策略成功匹配
        var xaml = """
            <UserControl
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="MyApp.DetailView">
            </UserControl>
            """;

        var dir = CreateTempDirWithFiles(
            new Dictionary<string, string>
            {
                ["DetailViewModel.cs"] =
                    "namespace MyApp { " +
                    "public class DetailViewModel { } }",
                ["DetailView.xaml"] = xaml,
            });

        var project = CreateProjectWithFiles(
            dir, ["DetailView.xaml", "DetailViewModel.cs"]);
        var result = await _mapper.MapAsync(project);

        result.Should().NotBeNull();
        result.Mappings.Should().Contain(m =>
            m.MappingSource == "Convention" &&
            m.ViewModelClassName == "MyApp.DetailViewModel");
    }

    private static Project CreateProjectWithFiles(
        string dir, string[] fileNames)
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
            var fullPath = Path.Combine(dir, fileName);
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
}
