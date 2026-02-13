using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Roslyn;
using Xunit;
using FluentAssertions;

namespace DotNetAnalyzer.Tests.Roslyn;

public class SyntaxTreeAnalyzerTests
{
    [Fact]
    public void AnalyzeTree_ShouldReturnCorrectInfo()
    {
        // Arrange
        var code = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            var x = 42;
        }
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        // Act
        var result = SyntaxTreeAnalyzer.AnalyzeTree(syntaxTree);

        // Assert
        result.Should().NotBeNull();
        result.FilePath.Should().BeEmpty();
        result.HasCompilationUnit.Should().BeTrue();
        result.UsingsCount.Should().Be(1);
        result.NamespacesCount.Should().Be(1);
        result.TypeDeclarationsCount.Should().Be(1);
        result.MethodDeclarationsCount.Should().Be(1);
    }

    [Fact]
    public void ExtractHierarchy_ShouldReturnCorrectStructure()
    {
        // Arrange
        var code = @"
namespace TestNamespace
{
    public class TestClass
    {
        public void Method1() {}
        public void Method2() {}
    }

    public class AnotherClass
    {
        public void Method3() {}
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();

        // Act
        var hierarchy = SyntaxTreeAnalyzer.ExtractHierarchy(root);

        // Assert
        hierarchy.Namespaces.Should().HaveCount(1);
        var ns = hierarchy.Namespaces[0];
        ns.Name.Should().Be("TestNamespace");
        ns.Types.Should().HaveCount(2);

        var class1 = ns.Types[0];
        class1.Name.Should().Be("TestClass");
        class1.Members.Should().HaveCount(2);

        var class2 = ns.Types[1];
        class2.Name.Should().Be("AnotherClass");
        class2.Members.Should().HaveCount(1);
    }

    [Fact]
    public void FindNodeAtPosition_ShouldFindCorrectNode()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class MyClass
    {
        public void MyMethod() {}
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        // Act - 查找第4行的节点
        var node = SyntaxTreeAnalyzer.FindNodeAtPosition(syntaxTree, 4, 10);

        // Assert
        node.Should().NotBeNull();
        node.Kind().ToString().Should().Contain("Method");
    }

    [Fact]
    public void FindNodeAtPosition_WithInvalidLine_ShouldHandleGracefully()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class MyClass
    {
        public void MyMethod() {}
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        // Act & Assert - 行号超出范围应该抛出异常
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SyntaxTreeAnalyzer.FindNodeAtPosition(syntaxTree, 100, 10));
    }

    [Fact]
    public void ExtractHierarchy_WithMultipleNamespaces_ShouldReturnAllNamespaces()
    {
        // Arrange
        var code = @"
namespace NS1
{
    public class Class1 {}
}

namespace NS2
{
    public class Class2 {}
}
";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();

        // Act
        var hierarchy = SyntaxTreeAnalyzer.ExtractHierarchy(root);

        // Assert
        hierarchy.Namespaces.Should().HaveCount(2);
        hierarchy.Namespaces[0].Name.Should().Be("NS1");
        hierarchy.Namespaces[1].Name.Should().Be("NS2");
        hierarchy.Namespaces[0].Types.Should().HaveCount(1);
        hierarchy.Namespaces[1].Types.Should().HaveCount(1);
    }

    [Fact]
    public void ExtractHierarchy_WithDifferentMemberTypes_ShouldExtractAllMembers()
    {
        // Arrange
        var code = @"
namespace Test
{
    public class MyClass
    {
        private int _field;
        public string Property { get; set; }
        public MyClass() {}
        public void Method() {}
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();

        // Act
        var hierarchy = SyntaxTreeAnalyzer.ExtractHierarchy(root);
        var classInfo = hierarchy.Namespaces[0].Types[0];

        // Assert
        classInfo.Members.Should().HaveCount(4);
        classInfo.Members[0].Kind.Should().Contain("Field");
        classInfo.Members[0].Name.Should().Be("_field");
        classInfo.Members[1].Kind.Should().Contain("Property");
        classInfo.Members[1].Name.Should().Be("Property");
        classInfo.Members[2].Kind.Should().Contain("Constructor");
        classInfo.Members[2].Name.Should().Be("MyClass");
        classInfo.Members[3].Kind.Should().Contain("Method");
        classInfo.Members[3].Name.Should().Be("Method");
    }

    [Fact]
    public void AnalyzeTree_WithEmptyCode_ShouldReturnZeroCounts()
    {
        // Arrange
        var code = @"";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        // Act
        var result = SyntaxTreeAnalyzer.AnalyzeTree(syntaxTree);

        // Assert
        result.Should().NotBeNull();
        result.UsingsCount.Should().Be(0);
        result.NamespacesCount.Should().Be(0);
        result.TypeDeclarationsCount.Should().Be(0);
        result.MethodDeclarationsCount.Should().Be(0);
    }

    [Fact]
    public void AnalyzeTree_ShouldThrowOnNullTree()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SyntaxTreeAnalyzer.AnalyzeTree(null!));
    }
}
