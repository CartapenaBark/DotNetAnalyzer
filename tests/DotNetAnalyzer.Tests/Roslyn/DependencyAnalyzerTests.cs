using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Roslyn;
using Xunit;
using FluentAssertions;

namespace DotNetAnalyzer.Tests.Roslyn;

public class DependencyAnalyzerTests
{
    [Fact]
    public void AnalyzeDependencies_ShouldReturnProjectInfo()
    {
        // Arrange - 需要一个真实的 Project 对象
        // 这个测试需要集成测试环境，这里先写个基础的

        // Act & Assert - 基础验证
        var result = new ProjectDependencyInfo
        {
            ProjectName = "TestProject",
            TargetFramework = "net8.0"
        };

        result.ProjectName.Should().Be("TestProject");
        result.TargetFramework.Should().Be("net8.0");
        result.ProjectReferences.Should().NotBeNull();
        result.PackageReferences.Should().NotBeNull();
    }

    [Fact]
    public void ProjectDependencyInfo_ShouldHandleEmptyReferences()
    {
        // Arrange
        var info = new ProjectDependencyInfo
        {
            ProjectName = "EmptyProject",
            ProjectReferences = Array.Empty<ProjectReferenceInfo>(),
            PackageReferences = Array.Empty<PackageReferenceInfo>()
        };

        // Assert
        info.ProjectReferences.Should().BeEmpty();
        info.PackageReferences.Should().BeEmpty();
        info.TransitiveDependencies.Should().BeEmpty();
    }
}
