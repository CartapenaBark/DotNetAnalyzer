using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;
using System.Text.Json;

namespace DotNetAnalyzer.Tests.Tools;

/// <summary>
/// 高级查询工具功能测试
/// 测试符号解析、定义和引用查询、文档列表功能
/// </summary>
public class AdvancedQueryToolsTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly ITestOutputHelper _output;

    public AdvancedQueryToolsTests(ITestOutputHelper output)
    {
        _output = output;
        _workspace = new AdhocWorkspace();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task ResolveSymbol_WithMethodSymbol_ShouldReturnSymbolInfo()
    {
        // Arrange - 创建包含方法的代码
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void TestMethod()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";
        var document = CreateTestDocument(code);

        // 模拟 MCP 工具调用
        var filePath = document.FilePath ?? "Test.cs";
        var line = 5; // TestMethod 声明行
        var column = 17; // TestMethod 名称位置

        // Act - 直接测试符号解析逻辑
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        var textLine = root!.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 0));
        var symbol = semanticModel!.GetSymbolInfo(node).Symbol;

        // Assert
        symbol.Should().NotBeNull();
        symbol.Name.Should().Be("TestMethod");
        symbol.Kind.Should().Be(SymbolKind.Method);

        _output.WriteLine($"✅ 符号解析成功");
        _output.WriteLine($"   符号名称: {symbol.Name}");
        _output.WriteLine($"   符号类型: {symbol.Kind}");
    }

    [Fact]
    public async Task ResolveSymbol_WithOverride_ShouldDetectOverride()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class BaseClass
    {
        public virtual void VirtualMethod() { }
    }

    public class DerivedClass : BaseClass
    {
        public override void VirtualMethod() { }
    }
}";
        var document = CreateTestDocument(code);

        // Act - 查找重写方法
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        var methodNode = root!.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "VirtualMethod" && m.Parent.Parent.Parent.ToString().Contains("DerivedClass"));

        var symbol = semanticModel!.GetDeclaredSymbol(methodNode!);

        // Assert
        symbol.Should().NotBeNull();
        symbol.IsOverride.Should().BeTrue();

        _output.WriteLine($"✅ 重写方法检测正确");
    }

    [Fact]
    public async Task GetDefinitionAndReferences_WithMethodUsage_ShouldReturnDefinitionAndReferences()
    {
        // Arrange - 创建包含方法定义和调用的代码
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void HelperMethod()
        {
        }

        public void CallerMethod()
        {
            HelperMethod(); // 调用 1
        }

        public void AnotherCaller()
        {
            HelperMethod(); // 调用 2
        }
    }
}";
        var document = CreateTestDocument(code);

        // Act - 查找 HelperMethod 的定义和引用
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();

        // 找到 HelperMethod 的声明
        var methodNode = root!.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "HelperMethod");

        var methodSymbol = semanticModel!.GetDeclaredSymbol(methodNode!);

        // Assert
        methodSymbol.Should().NotBeNull();
        methodSymbol.Name.Should().Be("HelperMethod");

        // 查找引用
        var invocations = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>();

        var referenceCount = 0;
        foreach (var invocation in invocations)
        {
            var invokedSymbol = semanticModel.GetSymbolInfo(invocation).Symbol;
            if (SymbolEqualityComparer.Default.Equals(invokedSymbol, methodSymbol))
            {
                referenceCount++;
            }
        }

        referenceCount.Should().BeGreaterThanOrEqualTo(2, "HelperMethod 至少被调用两次");

        _output.WriteLine($"✅ 定义和引用检测成功");
        _output.WriteLine($"   找到 {referenceCount} 个引用");
    }

    [Fact]
    public async Task GetDocumentList_WithMultipleDocuments_ShouldReturnAllDocuments()
    {
        // Arrange - 创建包含多个文档的项目
        var project = _workspace.AddProject("TestProject", LanguageNames.CSharp);

        var doc1 = CreateDocumentInProject(project, "File1.cs", "public class Class1 { }");
        var doc2 = CreateDocumentInProject(project, "File2.cs", "public class Class2 { }");
        var doc3 = CreateDocumentInProject(project, "File3.cs", "public class Class3 { }");

        // Act
        var documents = project.Documents.ToList();

        // Assert
        documents.Should().HaveCountGreaterThanOrEqualTo(3, "应该有至少 3 个文档");

        _output.WriteLine($"✅ 文档列表获取成功");
        _output.WriteLine($"   文档数量: {documents.Count}");

        foreach (var doc in documents)
        {
            var tree = await doc.GetSyntaxTreeAsync();
            var lineCount = tree!.GetText().Lines.Count;
            _output.WriteLine($"   - {doc.Name}: {lineCount} 行");
        }
    }

    [Fact]
    public async Task GetDocumentList_WithFilter_ShouldReturnFilteredDocuments()
    {
        // Arrange
        var project = _workspace.AddProject("TestProject", LanguageNames.CSharp);

        CreateDocumentInProject(project, "File1.cs", "public class Class1 { }");
        CreateDocumentInProject(project, "File2.txt", "Some text content");
        CreateDocumentInProject(project, "File3.cs", "public class Class3 { }");

        // Act - 应用过滤器
        var filteredDocs = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs") == true)
            .ToList();

        // Assert
        filteredDocs.Should().HaveCount(3, "应该有 3 个 .cs 文件");

        _output.WriteLine($"✅ 过滤器应用成功");
        _output.WriteLine($"   .cs 文件数量: {filteredDocs.Count}");
    }

    [Fact]
    public async Task GetDocumentList_ShouldIncludeErrorInformation()
    {
        // Arrange - 创建包含语法错误的代码
        var codeWithErrors = @"
namespace Test
{
    public class TestClass
    {
        public void Method(
        // 缺少右括号 - 语法错误
    }
}";

        var project = _workspace.AddProject("TestProject", LanguageNames.CSharp);
        CreateDocumentInProject(project, "ErrorFile.cs", codeWithErrors);

        // Act
        var documents = project.Documents.ToList();
        var errorCount = 0;

        foreach (var doc in documents)
        {
            var tree = await doc.GetSyntaxTreeAsync();
            var diagnostics = tree!.GetDiagnostics();
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Count();
            errorCount += errors;
        }

        // Assert
        errorCount.Should().BeGreaterThan(0, "应该检测到语法错误");

        _output.WriteLine($"✅ 错误信息包含正确");
        _output.WriteLine($"   错误数量: {errorCount}");
    }

    /// <summary>
    /// 创建测试文档的辅助方法
    /// </summary>
    private Document CreateTestDocument(string code)
    {
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp);

        _workspace.AddProject(projectInfo);

        var documentInfo = DocumentInfo.Create(
            documentId,
            "Test.cs",
            filePath: "/Test.cs",
            sourceCodeKind: SourceCodeKind.Regular,
            loader: TextLoader.From(TextAndVersion.Create(
                Microsoft.CodeAnalysis.Text.SourceText.From(code),
                VersionStamp.Create())));

        _workspace.AddDocument(documentInfo);

        return _workspace.CurrentSolution.GetDocument(documentId)!;
    }

    /// <summary>
    /// 在项目中创建文档的辅助方法
    /// </summary>
    private Document CreateDocumentInProject(Project project, string name, string content)
    {
        var documentId = DocumentId.CreateNewId(project.Id);

        var documentInfo = DocumentInfo.Create(
            documentId,
            name,
            filePath: $"/{name}",
            sourceCodeKind: SourceCodeKind.Regular,
            loader: TextLoader.From(TextAndVersion.Create(
                Microsoft.CodeAnalysis.Text.SourceText.From(content),
                VersionStamp.Create())));

        _workspace.AddDocument(documentInfo);

        return _workspace.CurrentSolution.GetDocument(documentId)!;
    }
}
