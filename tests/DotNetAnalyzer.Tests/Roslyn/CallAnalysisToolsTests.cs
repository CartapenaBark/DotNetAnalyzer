using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DotNetAnalyzer.Core.Roslyn.CallAnalysis;
using DotNetAnalyzer.Core.Models.CallAnalysis;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace DotNetAnalyzer.Tests.Roslyn;

/// <summary>
/// 调用分析工具功能测试
/// 测试调用者分析、被调用者分析和调用图生成功能
/// </summary>
public class CallAnalysisToolsTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly ITestOutputHelper _output;

    public CallAnalysisToolsTests(ITestOutputHelper output)
    {
        _output = output;
        _workspace = new AdhocWorkspace();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task GetCallerInfoAsync_WithMethodThatHasCallers_ShouldReturnCallers()
    {
        // Arrange - 创建一个有调用关系的代码
        var callerCode = @"
namespace Test
{
    public class Caller
    {
        public void MethodA()
        {
            MethodB();
        }

        public void MethodB()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";
        var document = CreateTestDocument(callerCode);

        // Act - 获取 MethodB 的调用者信息
        var result = await CallerAnalyzer.GetCallerInfoAsync(
            document,
            line: 8, // MethodB 声明行
            column: 17, // MethodB 名称位置
            includeIndirect: false);

        // Assert
        result.Should().NotBeNull();
        result.CallCount.Should().BeGreaterThan(0, "MethodB 被 MethodA 调用");
        result.Callers.Should().Contain(c => c.CallerSymbol.Name == "MethodA");

        _output.WriteLine($"✅ 找到 {result.CallCount} 个调用者");
        foreach (var caller in result.Callers)
        {
            _output.WriteLine($"   - {caller.CallerSymbol.Name} at {caller.Location.FilePath}:{caller.Location.Line}");
        }
    }

    [Fact]
    public async Task GetCallerInfoAsync_WithUnusedMethod_ShouldReturnEmpty()
    {
        // Arrange - 创建一个未被调用的方法
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void UnusedMethod()
        {
            Console.WriteLine(""Unused"");
        }
    }
}";
        var document = CreateTestDocument(code);

        // Act
        var result = await CallerAnalyzer.GetCallerInfoAsync(
            document,
            line: 5,
            column: 21,
            includeIndirect: false);

        // Assert
        result.Should().NotBeNull();
        result.CallCount.Should().Be(0, "UnusedMethod 没有被调用");
        result.Callers.Should().BeEmpty();

        _output.WriteLine("✅ 未使用方法正确返回空结果");
    }

    [Fact]
    public async Task GetCalleeInfoAsync_WithMethodThatCallsOthers_ShouldReturnCallees()
    {
        // Arrange - 创建一个调用其他方法的代码
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

        // Act - 获取 MainMethod 的被调用者信息
        var result = await CalleeAnalyzer.GetCalleeInfoAsync(
            document,
            line: 5,
            column: 20,
            depth: 0);

        // Assert
        result.Should().NotBeNull();
        result.Callees.Should().HaveCountGreaterThanOrEqualTo(2, "MainMethod 调用了 Helper1 和 Helper2");

        _output.WriteLine($"✅ 找到 {result.Callees.Count} 个被调用者");
        foreach (var callee in result.Callees)
        {
            _output.WriteLine($"   - {callee.Method.Name}");
        }
    }

    [Fact]
    public async Task GetCalleeInfoAsync_WithRecursiveDepth_ShouldBuildCallTree()
    {
        // Arrange - 创建递归调用结构
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

        public void MethodC()
        {
            Console.WriteLine(""End"");
        }
    }
}";
        var document = CreateTestDocument(code);

        // Act - 使用深度 1 获取调用信息
        var result = await CalleeAnalyzer.GetCalleeInfoAsync(
            document,
            line: 5,
            column: 20,
            depth: 1);

        // Assert
        result.Should().NotBeNull();
        result.CallTree.Should().NotBeNull();
        result.CallTree.Method.Should().Be("MethodA");

        _output.WriteLine($"✅ 调用树深度: {GetMaxDepth(result.CallTree)}");
    }

    [Fact]
    public async Task GetCallGraphAsync_WithSimpleCallChain_ShouldGenerateGraph()
    {
        // Arrange - 创建简单的调用链
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

        public void MethodC()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";
        var document = CreateTestDocument(code);

        // Act
        var result = await CallGraphBuilder.GetCallGraphAsync(
            document,
            line: 5,
            column: 20,
            maxDepth: 10,
            format: "dot");

        // Assert
        result.Should().NotBeNull();
        result.Graph.Should().NotBeNull();
        result.Graph.Nodes.Should().HaveCountGreaterThanOrEqualTo(3, "应该包含 MethodA, MethodB, MethodC");
        result.Graph.Edges.Should().HaveCountGreaterThanOrEqualTo(2, "应该有 A->B 和 B->C 的边");
        result.Visualization.Format.Should().Be("dot");
        result.Visualization.Content.Should().NotBeEmpty();

        _output.WriteLine($"✅ 调用图生成成功");
        _output.WriteLine($"   节点数: {result.Graph.Nodes.Count}");
        _output.WriteLine($"   边数: {result.Graph.Edges.Count}");

        // 输出每个节点的指标
        foreach (var node in result.Graph.Nodes)
        {
            _output.WriteLine($"   {node.Name}: FanIn={node.Metrics.FanIn}, FanOut={node.Metrics.FanOut}");
        }
    }

    [Fact]
    public async Task GetCallGraphAsync_WithMaxDepthLimit_ShouldRespectLimit()
    {
        // Arrange - 创建深层调用链
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void MethodA() => MethodB();
        public void MethodB() => MethodC();
        public void MethodC() => MethodD();
        public void MethodD() => MethodE();
        public void MethodE() => Console.WriteLine(""End"");
    }
}";
        var document = CreateTestDocument(code);

        // Act - 限制最大深度为 2
        var result = await CallGraphBuilder.GetCallGraphAsync(
            document,
            line: 5,
            column: 20,
            maxDepth: 2,
            format: "dot");

        // Assert
        result.Should().NotBeNull();
        result.Graph.Nodes.Count.Should().BeLessThanOrEqualTo(3, "深度为 2 时最多应该有 3 个节点 (A->B->C)");

        _output.WriteLine($"✅ 深度限制正确应用: {result.Graph.Nodes.Count} 个节点");
    }

    [Fact]
    public async Task GetCallGraphAsync_WithDifferentFormats_ShouldGenerateCorrectFormat()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void MethodA() => Console.WriteLine(""Hello"");
    }
}";
        var document = CreateTestDocument(code);

        // Act & Assert - 测试 DOT 格式
        var dotResult = await CallGraphBuilder.GetCallGraphAsync(
            document, 5, 20, 10, "dot");
        dotResult.Visualization.Format.Should().Be("dot");
        dotResult.Visualization.Content.Should().Contain("digraph");

        _output.WriteLine("✅ DOT 格式生成正确");

        // 测试 JSON 格式
        var jsonResult = await CallGraphBuilder.GetCallGraphAsync(
            document, 5, 20, 10, "json");
        jsonResult.Visualization.Format.Should().Be("json");

        _output.WriteLine("✅ JSON 格式生成正确");
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
    /// 获取调用树的最大深度
    /// </summary>
    private int GetMaxDepth(CallTreeNode tree)
    {
        if (tree.Children == null || tree.Children.Count == 0)
        {
            return tree.Depth;
        }

        return tree.Children.Max(GetMaxDepth);
    }
}
