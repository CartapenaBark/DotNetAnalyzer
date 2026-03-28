using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Roslyn.CallAnalysis;
using DotNetAnalyzer.Core.Models.CallAnalysis;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace DotNetAnalyzer.Tests.Roslyn;

/// <summary>
/// CalleeAnalyzer 跨文档调用解析、接口/虚方法分派、循环检测和深度限制测试
/// </summary>
public class CalleeAnalyzerTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly ITestOutputHelper _output;

    public CalleeAnalyzerTests(ITestOutputHelper output)
    {
        _output = output;
        _workspace = new AdhocWorkspace();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    /// <summary>
    /// 基本跨文档被调用者解析：方法 A 调用方法 B（同一文档），应能正确识别
    /// </summary>
    [Fact]
    public async Task GetCalleeInfoAsync_BasicSameDocument_ShouldResolveCallees()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void MainMethod()
        {
            Helper1();
            Helper2();
        }

        public void Helper1() { }
        public void Helper2() { }
    }
}";
        var document = CreateTestDocument(code);

        var (line, column) = await FindMethodPositionAsync(
            document, "MainMethod");

        // Act
        var result = await CalleeAnalyzer.GetCalleeInfoAsync(
            document, line, column, depth: 2);

        // Assert
        result.Should().NotBeNull();
        result.Callees.Should().HaveCountGreaterThanOrEqualTo(2,
            "MainMethod 调用了 Helper1 和 Helper2");
        result.Truncated.Should().BeFalse();

        var calleeNames = result.Callees
            .Select(c => c.Method.Name).ToList();
        calleeNames.Should().Contain("Helper1");
        calleeNames.Should().Contain("Helper2");

        _output.WriteLine(
            $"Found {result.Callees.Count} callees: " +
            string.Join(", ", calleeNames));
    }

    /// <summary>
    /// 跨文档调用解析：文件 A 的方法调用文件 B 的方法
    /// </summary>
    [Fact]
    public async Task GetCalleeInfoAsync_CrossDocument_ShouldResolveCallees()
    {
        // Arrange — 两个文件的代码
        var codeA = @"
namespace Test
{
    public class ClassA
    {
        public void MethodA()
        {
            var b = new ClassB();
            b.MethodB();
        }
    }
}";
        var codeB = @"
namespace Test
{
    public class ClassB
    {
        public void MethodB()
        {
            System.Console.WriteLine(""Hello"");
        }
    }
}";

        var document = CreateMultiDocumentProject(
            [("FileA.cs", codeA), ("FileB.cs", codeB)],
            "FileA.cs");

        var (line, column) = await FindMethodPositionAsync(document, "MethodA");

        // Act
        var result = await CalleeAnalyzer.GetCalleeInfoAsync(
            document, line, column, depth: 2);

        // Assert
        result.Should().NotBeNull();
        result.Callees.Should().Contain(c =>
            c.Method.Name == "MethodB",
            "MethodA 调用了 ClassB.MethodB（跨文档）");

        _output.WriteLine(
            $"Callees: {string.Join(", ",
                result.Callees.Select(c => c.Method.Name))}");
    }

    /// <summary>
    /// 接口到实现分派：通过接口类型调用方法时，应解析到具体实现
    /// </summary>
    [Fact]
    public async Task GetCalleeInfoAsync_InterfaceToImplementation_ShouldDispatch()
    {
        // Arrange
        var code = @"
namespace Test
{
    public interface IService
    {
        void Execute();
    }

    public class ServiceA : IService
    {
        public void Execute()
        {
            System.Console.WriteLine(""A"");
        }
    }

    public class ServiceB : IService
    {
        public void Execute()
        {
            System.Console.WriteLine(""B"");
        }
    }

    public class Caller
    {
        public void Run(IService service)
        {
            service.Execute();
        }
    }
}";
        var document = CreateTestDocument(code);
        var (line, column) = await FindMethodPositionAsync(document, "Run");

        // Act
        var result = await CalleeAnalyzer.GetCalleeInfoAsync(
            document, line, column, depth: 1);

        // Assert
        result.Should().NotBeNull();

        // 应找到 Execute 调用，并且分派类型为 InterfaceImplementation
        var executeCallees = result.Callees
            .Where(c => c.Method.Name == "Execute").ToList();
        executeCallees.Should().NotBeEmpty(
            "Run 方法通过接口调用了 Execute");

        // 调用树中的 Execute 节点应标记为 InterfaceImplementation
        var executeChild = result.CallTree.Children
            .FirstOrDefault(c => c.Method == "Execute");
        executeChild.Should().NotBeNull();
        executeChild!.DispatchKind.Should().Be(
            DispatchKind.InterfaceImplementation);

        _output.WriteLine(
            $"Interface dispatch callees: " +
            string.Join(", ",
                executeCallees.Select(c => c.Method.ContainingType)));
    }

    /// <summary>
    /// 虚方法到重写分派：调用虚方法时应列出所有可能的运行时目标
    /// </summary>
    [Fact]
    public async Task GetCalleeInfoAsync_VirtualToOverride_ShouldDispatch()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class Base
    {
        public virtual void Process()
        {
            System.Console.WriteLine(""Base"");
        }
    }

    public class Derived : Base
    {
        public override void Process()
        {
            System.Console.WriteLine(""Derived"");
        }
    }

    public class AnotherDerived : Base
    {
        public override void Process()
        {
            System.Console.WriteLine(""AnotherDerived"");
        }
    }

    public class Caller
    {
        public void Run(Base b)
        {
            b.Process();
        }
    }
}";
        var document = CreateTestDocument(code);
        var (line, column) = await FindMethodPositionAsync(document, "Run");

        // Act
        var result = await CalleeAnalyzer.GetCalleeInfoAsync(
            document, line, column, depth: 1);

        // Assert
        result.Should().NotBeNull();

        // 调用树中应包含 Process 子节点，标记为 VirtualOverride
        var processChild = result.CallTree.Children
            .FirstOrDefault(c => c.Method == "Process");
        processChild.Should().NotBeNull();
        processChild!.DispatchKind.Should().Be(
            DispatchKind.VirtualOverride);

        _output.WriteLine(
            $"Virtual dispatch: {processChild.Method} " +
            $"(kind={processChild.DispatchKind})");
    }

    /// <summary>
    /// 循环检测：A 调用 B，B 调用 A，应检测到循环并标记 truncated
    /// </summary>
    [Fact]
    public async Task GetCalleeInfoAsync_CyclicCall_ShouldDetectAndTruncate()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void MethodA()
        {
            MethodB();
        }

        public void MethodB()
        {
            MethodA();
        }
    }
}";
        var document = CreateTestDocument(code);
        var (line, column) = await FindMethodPositionAsync(document, "MethodA");

        // Act — 使用足够大的深度以触发循环检测
        var result = await CalleeAnalyzer.GetCalleeInfoAsync(
            document, line, column, depth: 10);

        // Assert
        result.Should().NotBeNull();
        result.Truncated.Should().BeTrue(
            "存在循环调用 A->B->A，应标记为截断");

        // 调用树中应有截断的节点
        var hasTruncated = HasTruncatedNode(result.CallTree);
        hasTruncated.Should().BeTrue(
            "循环检测应标记被截断的节点");

        _output.WriteLine("Cyclic call correctly detected and truncated");
    }

    /// <summary>
    /// 深度限制截断：当调用链超过最大深度时，应停止并标记 truncated
    /// </summary>
    [Fact]
    public async Task GetCalleeInfoAsync_ExceedsMaxDepth_ShouldTruncate()
    {
        // Arrange — 5 层调用链
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void MethodA() => MethodB();
        public void MethodB() => MethodC();
        public void MethodC() => MethodD();
        public void MethodD() => MethodE();
        public void MethodE() => System.Console.WriteLine(""End"");
    }
}";
        var document = CreateTestDocument(code);
        var (line, column) = await FindMethodPositionAsync(document, "MethodA");

        // Act — 限制深度为 2
        var result = await CalleeAnalyzer.GetCalleeInfoAsync(
            document, line, column, depth: 2);

        // Assert
        result.Should().NotBeNull();
        result.Truncated.Should().BeTrue(
            "调用链 A->B->C->D->E 超过深度 2，应标记为截断");

        // 调用树的最大深度不应超过 maxDepth + 1（根节点算 0）
        var maxTreeDepth = GetMaxDepth(result.CallTree);
        maxTreeDepth.Should().BeLessThanOrEqualTo(2);

        _output.WriteLine(
            $"Max tree depth: {maxTreeDepth} (truncated={result.Truncated})");
    }

    /// <summary>
    /// 深度为 1 时应返回直接被调用者但不递归到更深层。
    /// CalleeAnalyzer 的 depth 参数是 maxDepth：
    /// 根节点在 depth 0，直接被调用者在 depth 1。
    /// depth=1 时，直接被调用者会被标记为 truncated，
    /// 但它们仍会出现在 Callees 列表中。
    /// </summary>
    [Fact]
    public async Task GetCalleeInfoAsync_ZeroDepth_ShouldReturnDirectCalleesOnly()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void MethodA()
        {
            MethodB();
        }

        public void MethodB()
        {
            MethodC();
        }

        public void MethodC() { }
    }
}";
        var document = CreateTestDocument(code);
        var (line, column) = await FindMethodPositionAsync(document, "MethodA");

        // Act — depth=1: 根在 0，直接调用在 1（被截断），不会再递归
        var result = await CalleeAnalyzer.GetCalleeInfoAsync(
            document, line, column, depth: 1);

        // Assert
        result.Should().NotBeNull();
        result.Callees.Should().Contain(c =>
            c.Method.Name == "MethodB",
            "应返回直接被调用者 MethodB");

        // 不应递归到 MethodC
        result.Callees.Should().NotContain(c =>
            c.Method.Name == "MethodC",
            "不应包含间接被调用者 MethodC");

        // 直接被调用者（depth=1）的子节点应为空或被截断
        foreach (var child in result.CallTree.Children)
        {
            child.Children.Should().BeEmpty(
                "直接被调用者不应有 further children");
        }

        _output.WriteLine("Depth=1 correctly returns direct callees only (no recursion)");
    }

    /// <summary>
    /// 未被调用的方法应返回空列表
    /// </summary>
    [Fact]
    public async Task GetCalleeInfoAsync_NoCallees_ShouldReturnEmpty()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void StandaloneMethod()
        {
            System.Console.WriteLine(""standalone"");
        }
    }
}";
        var document = CreateTestDocument(code);
        var (line, column) = await FindMethodPositionAsync(
            document, "StandaloneMethod");

        // Act
        var result = await CalleeAnalyzer.GetCalleeInfoAsync(
            document, line, column, depth: 1);

        // Assert
        result.Should().NotBeNull();
        result.Callees.Should().BeEmpty();
        result.CallTree.Children.Should().BeEmpty();
        result.Truncated.Should().BeFalse();

        _output.WriteLine("Empty callees correctly returned");
    }

    /// <summary>
    /// 直接调用（非虚方法、非接口）应标记为 Direct 分派类型
    /// </summary>
    [Fact]
    public async Task GetCalleeInfoAsync_DirectCall_ShouldMarkDirectDispatch()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void Caller()
        {
            PlainMethod();
        }

        public void PlainMethod()
        {
            System.Console.WriteLine(""plain"");
        }
    }
}";
        var document = CreateTestDocument(code);
        var (line, column) = await FindMethodPositionAsync(document, "Caller");

        // Act
        var result = await CalleeAnalyzer.GetCalleeInfoAsync(
            document, line, column, depth: 1);

        // Assert
        result.Should().NotBeNull();
        var plainChild = result.CallTree.Children
            .FirstOrDefault(c => c.Method == "PlainMethod");
        plainChild.Should().NotBeNull();
        plainChild!.DispatchKind.Should().Be(DispatchKind.Direct);

        _output.WriteLine("Direct dispatch kind correctly assigned");
    }

    #region Helper Methods

    /// <summary>
    /// 创建单文档测试项目
    /// </summary>
    private Document CreateTestDocument(string code)
    {
        return CreateMultiDocumentProject(
            [("Test.cs", code)], "Test.cs");
    }

    /// <summary>
    /// 创建多文档测试项目，返回指定文件对应的 Document
    /// </summary>
    private Document CreateMultiDocumentProject(
        (string FileName, string Code)[] files,
        string targetFileName)
    {
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp);

        _workspace.AddProject(projectInfo);

        DocumentId? targetDocId = null;

        foreach (var (fileName, code) in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            var documentInfo = DocumentInfo.Create(
                documentId,
                fileName,
                filePath: $"/{fileName}",
                sourceCodeKind: SourceCodeKind.Regular,
                loader: TextLoader.From(TextAndVersion.Create(
                    Microsoft.CodeAnalysis.Text.SourceText.From(code),
                    VersionStamp.Create())));

            _workspace.AddDocument(documentInfo);

            if (fileName == targetFileName)
            {
                targetDocId = documentId;
            }
        }

        return _workspace.CurrentSolution.GetDocument(
            targetDocId!)!;
    }

    /// <summary>
    /// 查找指定方法名称的位置（行号、列号）
    /// </summary>
    private static async Task<(int Line, int Column)> FindMethodPositionAsync(
        Document document, string methodName)
    {
        var root = await document.GetSyntaxRootAsync();
        var methodNode = root!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == methodName);

        var lineSpan = methodNode.SyntaxTree.GetLineSpan(
            methodNode.Span);
        return (
            lineSpan.StartLinePosition.Line,
            lineSpan.StartLinePosition.Character);
    }

    /// <summary>
    /// 获取调用树的最大深度
    /// </summary>
    private static int GetMaxDepth(CallTreeNode tree)
    {
        if (tree.Children == null || tree.Children.Count == 0)
        {
            return tree.Depth;
        }

        return tree.Children.Max(GetMaxDepth);
    }

    /// <summary>
    /// 检查调用树中是否有被截断的节点
    /// </summary>
    private static bool HasTruncatedNode(CallTreeNode tree)
    {
        if (tree.Truncated)
        {
            return true;
        }

        return tree.Children.Any(HasTruncatedNode);
    }

    #endregion
}
