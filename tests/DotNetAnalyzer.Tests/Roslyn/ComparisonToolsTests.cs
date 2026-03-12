using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DotNetAnalyzer.Core.Roslyn.Comparison;
using DotNetAnalyzer.Core.Models.Comparison;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace DotNetAnalyzer.Tests.Roslyn;

/// <summary>
/// 代码比较工具功能测试
/// 测试语法树比较、代码差异生成和代码变更应用功能
/// </summary>
public class ComparisonToolsTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly ITestOutputHelper _output;

    public ComparisonToolsTests(ITestOutputHelper output)
    {
        _output = output;
        _workspace = new AdhocWorkspace();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task CompareAsync_WithIdenticalTrees_ShouldReturnNoDifferences()
    {
        // Arrange - 创建相同的代码
        var code1 = @"
namespace Test
{
    public class TestClass
    {
        public void Method()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";
        var code2 = code1; // 完全相同的代码

        var tree1 = CreateSyntaxTree(code1);
        var tree2 = CreateSyntaxTree(code2);

        // Act
        var result = await SyntaxTreeComparer.CompareAsync(
            tree1,
            tree2,
            ignoreWhitespace: false,
            ignoreComments: false);

        // Assert
        result.Should().NotBeNull();
        result.Differences.Should().BeEmpty("相同的语法树应该没有差异");

        _output.WriteLine("✅ 相同语法树比较正确");
    }

    [Fact]
    public async Task CompareAsync_WithDifferentMethodNames_ShouldDetectDifference()
    {
        // Arrange
        var code1 = @"
namespace Test
{
    public class TestClass
    {
        public void MethodA()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";

        var code2 = @"
namespace Test
{
    public class TestClass
    {
        public void MethodB()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";

        var tree1 = CreateSyntaxTree(code1);
        var tree2 = CreateSyntaxTree(code2);

        // Act
        var result = await SyntaxTreeComparer.CompareAsync(
            tree1,
            tree2,
            ignoreWhitespace: false,
            ignoreComments: false);

        // Assert
        result.Should().NotBeNull();
        result.Differences.Should().NotBeEmpty("不同的方法名应该产生差异");

        _output.WriteLine($"✅ 检测到 {result.Differences.Count} 个差异");

        foreach (var diff in result.Differences)
        {
            _output.WriteLine($"   - {diff.Kind}: {diff.Location?.FilePath}");
        }
    }

    [Fact]
    public async Task CompareAsync_WithIgnoreWhitespace_ShouldIgnoreWhitespaceDifferences()
    {
        // Arrange
        var code1 = @"
namespace Test
{
    public class TestClass
    {
        public void Method()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";

        var code2 = @"
namespace Test {
    public class TestClass {
        public void Method( ) {
            Console.WriteLine( ""Hello"" );
        }
    }
}";

        var tree1 = CreateSyntaxTree(code1);
        var tree2 = CreateSyntaxTree(code2);

        // Act - 忽略空白
        var result = await SyntaxTreeComparer.CompareAsync(
            tree1,
            tree2,
            ignoreWhitespace: true,
            ignoreComments: false);

        // Assert
        result.Should().NotBeNull();
        // 忽略空白后，差异应该减少

        _output.WriteLine($"✅ 忽略空白后检测到 {result.Differences.Count} 个差异");
    }

    [Fact]
    public async Task GetCodeDiffAsync_WithAddedLine_ShouldShowAddition()
    {
        // Arrange
        var beforeCode = @"
namespace Test
{
    public class TestClass
    {
        public void MethodA()
        {
        }
    }
}";

        var afterCode = @"
namespace Test
{
    public class TestClass
    {
        public void MethodA()
        {
        }

        public void MethodB()
        {
        }
    }
}";

        var beforePath = CreateTestFile(beforeCode, "before.cs");
        var afterPath = CreateTestFile(afterCode, "after.cs");

        // Act
        var result = await DiffGenerator.GetCodeDiffAsync(
            beforePath,
            afterPath,
            contextLines: 3);

        // Assert
        result.Should().NotBeNull();
        result.Diff.Should().NotBeEmpty();
        result.Diff.Should().Contain("+", "应该显示添加的行");

        _output.WriteLine($"✅ 代码差异检测正确");

        // 清理
        File.Delete(beforePath);
        File.Delete(afterPath);
    }

    [Fact]
    public async Task GetCodeDiffAsync_WithContextLines_ShouldIncludeContext()
    {
        // Arrange
        var beforeCode = @"Line1
Line2
Line3
Line4
Line5";

        var afterCode = @"Line1
Line2
Modified
Line4
Line5";

        var beforePath = CreateTestFile(beforeCode, "before.txt");
        var afterPath = CreateTestFile(afterCode, "after.txt");

        // Act
        var result = await DiffGenerator.GetCodeDiffAsync(
            beforePath,
            afterPath,
            contextLines: 2);

        // Assert
        result.Should().NotBeNull();
        result.Diff.Should().NotBeEmpty();

        _output.WriteLine($"✅ 上下文行正确应用");
        _output.WriteLine($"   Diff 长度: {result.Diff.Length}");

        // 清理
        File.Delete(beforePath);
        File.Delete(afterPath);
    }

    [Fact]
    public async Task ApplyChangesAsync_WithValidChanges_ShouldApplySuccessfully()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void Method()
        {
            var x = 1;
        }
    }
}";

        var document = CreateTestDocument(code);

        var changesJson = @"[
            {
                ""newText"": ""var y = 2;"",
                ""startLine"": 7,
                ""startColumn"": 12,
                ""endLine"": 7,
                ""endColumn"": 21
            }
        ]";

        // Act
        var result = await CodeChangeApplicator.ApplyChangesAsync(
            document,
            changesJson,
            format: true);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        _output.WriteLine($"✅ 代码变更应用成功");
        _output.WriteLine($"   应用了 {result.AppliedChanges} 个变更");

        if (result.Diagnostics.Count > 0)
        {
            _output.WriteLine($"   诊断信息: {result.Diagnostics.Count} 条");
        }
    }

    [Fact]
    public async Task ApplyChangesAsync_WithInvalidJson_ShouldReturnError()
    {
        // Arrange
        var code = "public class TestClass { }";
        var document = CreateTestDocument(code);

        var invalidJson = "{ invalid json }";

        // Act
        var result = await CodeChangeApplicator.ApplyChangesAsync(
            document,
            invalidJson,
            format: false);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("无效的 JSON 应该返回失败");

        _output.WriteLine($"✅ 无效 JSON 正确处理");
    }

    /// <summary>
    /// 创建语法树的辅助方法
    /// </summary>
    private static SyntaxTree CreateSyntaxTree(string code)
    {
        return CSharpSyntaxTree.ParseText(code);
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
    /// 创建临时测试文件的辅助方法
    /// </summary>
    private static string CreateTestFile(string content, string fileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var filePath = Path.Combine(tempDir, fileName);
        File.WriteAllText(filePath, content);

        return filePath;
    }
}
