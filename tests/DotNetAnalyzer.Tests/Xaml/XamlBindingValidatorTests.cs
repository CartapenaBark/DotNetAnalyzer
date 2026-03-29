using DotNetAnalyzer.Core.Xaml;
using DotNetAnalyzer.Core.Xaml.Models;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.Xaml;

/// <summary>
/// XamlBindingValidator 单元测试。
/// </summary>
/// <remarks>
/// 覆盖 Binding 验证的基本场景：有效绑定、空绑定集和多个绑定计数。
/// </remarks>
public class XamlBindingValidatorTests
{
    private readonly XamlBindingValidator _validator;

    public XamlBindingValidatorTests()
    {
        _validator = new XamlBindingValidator(
            NullLogger<XamlBindingValidator>.Instance);
    }

    #region 辅助方法

    /// <summary>
    /// 创建带有单个文档的测试项目（用于获取 Compilation）。
    /// </summary>
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

        var project = workspace.CurrentSolution.GetProject(projectId)!;
        return project;
    }

    /// <summary>
    /// 构造包含指定绑定列表的 XamlDocumentInfo。
    /// </summary>
    private static XamlDocumentInfo CreateXamlInfo(
        string filePath,
        IReadOnlyList<XamlBindingInfo> bindings,
        IReadOnlyList<XamlElementInfo>? elements = null)
    {
        return new XamlDocumentInfo
        {
            FilePath = filePath,
            RootElement = "Window",
            ClassAttribute = null,
            Namespaces = [],
            Elements = elements ?? [],
            Bindings = bindings,
            ResourceReferences = []
        };
    }

    private static XamlBindingInfo CreateBinding(
        string bindingType = "Binding",
        string? path = null,
        string? elementName = null,
        string hostElement = "TextBlock",
        string rawExpression = "")
    {
        return new XamlBindingInfo
        {
            BindingType = bindingType,
            Path = path,
            ElementName = elementName,
            Converter = null,
            Mode = null,
            RawExpression = rawExpression,
            HostElementName = hostElement,
            Line = 1,
            Column = 1,
            AttachedProperty = "Text"
        };
    }

    #endregion

    [Fact]
    public async Task ValidateBindingsAsync_SimpleBinding_ReturnsResult()
    {
        // Arrange
        var xamlInfo = CreateXamlInfo(
            "/TestWindow.xaml",
            [
                CreateBinding(
                    "Binding",
                    path: "Title",
                    rawExpression: "{Binding Path=Title}")
            ]);

        var project = await CreateProjectAsync(
            "public class TestViewModel { public string Title { get; set; } }");

        // Act
        var result = await _validator.ValidateAsync(
            xamlInfo, project);

        // Assert
        result.Should().NotBeNull();
        result.TotalBindings.Should().Be(1);
    }

    [Fact]
    public async Task ValidateBindingsAsync_EmptyBindings_NoIssues()
    {
        // Arrange
        var xamlInfo = CreateXamlInfo(
            "/EmptyWindow.xaml",
            []);

        var project = await CreateProjectAsync("public class Empty { }");

        // Act
        var result = await _validator.ValidateAsync(
            xamlInfo, project);

        // Assert
        result.Should().NotBeNull();
        result.TotalBindings.Should().Be(0);
        result.ValidBindings.Should().BeEmpty();
        result.InvalidBindings.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateBindingsAsync_MultipleBindings_ReturnsCorrectCount()
    {
        // Arrange — 三种场景：有 Path 无 DataType、x:Bind、ElementName
        var xamlInfo = CreateXamlInfo(
            "/MultiWindow.xaml",
            [
                CreateBinding(
                    "Binding",
                    path: "Name",
                    rawExpression: "{Binding Path=Name}"),
                CreateBinding(
                    "x:Bind",
                    rawExpression: "{x:Bind UserName}"),
                CreateBinding(
                    "Binding",
                    elementName: "OtherElement",
                    rawExpression: "{Binding ElementName=OtherElement}")
            ]);

        var project = await CreateProjectAsync("public class VM { }");

        // Act
        var result = await _validator.ValidateAsync(
            xamlInfo, project);

        // Assert
        result.Should().NotBeNull();
        result.TotalBindings.Should().Be(3);

        // 无法推断 ViewModel 类型时，Binding 和无 Path 的绑定均标记为有效（跳过验证）
        result.ValidBindings.Should().HaveCount(3);
        result.InvalidBindings.Should().BeEmpty();
    }
}
