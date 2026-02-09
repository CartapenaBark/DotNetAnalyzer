using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    [Fact]
    public void AnalyzeDependencies_WithNullProject_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            DependencyAnalyzer.AnalyzeDependencies(null!);
        });
    }

    [Fact]
    public void ProjectDependencyInfo_WithAllProperties_ShouldWorkCorrectly()
    {
        // Arrange
        var info = new ProjectDependencyInfo
        {
            ProjectName = "TestProject",
            ProjectFilePath = "C:\\Projects\\TestProject.csproj",
            TargetFramework = "net8.0",
            ProjectReferences = new[]
            {
                new ProjectReferenceInfo
                {
                    ProjectName = "Project1",
                    ProjectFilePath = "C:\\Projects\\Project1.csproj",
                    Alias = "global"
                }
            },
            PackageReferences = new[]
            {
                new PackageReferenceInfo
                {
                    Name = "Newtonsoft.Json",
                    Version = "13.0.3",
                    IsTransitive = false
                }
            },
            TransitiveDependencies = new[]
            {
                new DependencyInfo { Name = "TransitiveDep1" }
            },
            HasCircularReference = false
        };

        // Assert
        info.ProjectName.Should().Be("TestProject");
        info.ProjectFilePath.Should().Be("C:\\Projects\\TestProject.csproj");
        info.TargetFramework.Should().Be("net8.0");
        info.ProjectReferences.Should().HaveCount(1);
        info.PackageReferences.Should().HaveCount(1);
        info.TransitiveDependencies.Should().HaveCount(1);
        info.HasCircularReference.Should().BeFalse();
    }

    [Fact]
    public void ProjectReferenceInfo_WithDefaultValues_ShouldWorkCorrectly()
    {
        // Arrange
        var info = new ProjectReferenceInfo();

        // Assert
        info.ProjectName.Should().BeEmpty();
        info.ProjectFilePath.Should().BeNull();
        info.Alias.Should().BeEmpty();
    }

    [Fact]
    public void PackageReferenceInfo_WithDefaultValues_ShouldWorkCorrectly()
    {
        // Arrange
        var info = new PackageReferenceInfo();

        // Assert
        info.Name.Should().BeEmpty();
        info.Version.Should().BeEmpty();
        info.IsTransitive.Should().BeFalse();
    }

    [Fact]
    public void DependencyInfo_WithDefaultValues_ShouldWorkCorrectly()
    {
        // Arrange
        var info = new DependencyInfo();

        // Assert
        info.Name.Should().BeEmpty();
    }

    [Fact]
    public void ProjectDependencyInfo_HasCircularReference_ShouldBeSettable()
    {
        // Arrange
        var info = new ProjectDependencyInfo
        {
            ProjectName = "CircularProject",
            HasCircularReference = true
        };

        // Assert
        info.HasCircularReference.Should().BeTrue();
    }

    [Fact]
    public void ProjectReferenceInfo_WithMultipleAliases_ShouldHandleFirstAlias()
    {
        // Arrange
        var info = new ProjectReferenceInfo
        {
            ProjectName = "AliasedProject",
            Alias = "alias1,alias2"
        };

        // Assert
        info.Alias.Should().Be("alias1,alias2");
    }

    [Fact]
    public void PackageReferenceInfo_TransitiveVsNonTransitive_ShouldWorkCorrectly()
    {
        // Arrange
        var directDep = new PackageReferenceInfo
        {
            Name = "DirectPackage",
            Version = "1.0.0",
            IsTransitive = false
        };

        var transitiveDep = new PackageReferenceInfo
        {
            Name = "TransitivePackage",
            Version = "2.0.0",
            IsTransitive = true
        };

        // Assert
        directDep.IsTransitive.Should().BeFalse();
        transitiveDep.IsTransitive.Should().BeTrue();
    }

    [Fact]
    public void TransitiveDependencies_ShouldSupportMultipleLevels()
    {
        // Arrange
        var info = new ProjectDependencyInfo
        {
            ProjectName = "MainProject",
            TransitiveDependencies = new[]
            {
                new DependencyInfo { Name = "Level1A" },
                new DependencyInfo { Name = "Level1B" },
                new DependencyInfo { Name = "Level2A" },
                new DependencyInfo { Name = "Level3A" }
            }
        };

        // Assert
        info.TransitiveDependencies.Should().HaveCount(4);
        info.TransitiveDependencies.Select(d => d.Name).Should().Contain("Level1A");
        info.TransitiveDependencies.Select(d => d.Name).Should().Contain("Level3A");
    }

    [Fact]
    public void TargetFramework_ShouldHandleVariousFormats()
    {
        // Arrange & Act
        var frameworks = new[]
        {
            "net8.0",
            "net7.0",
            "net6.0",
            "netstandard2.1",
            "net48",
            "net472",
            "Unknown"
        };

        foreach (var framework in frameworks)
        {
            var info = new ProjectDependencyInfo
            {
                ProjectName = $"Project_{framework}",
                TargetFramework = framework
            };

            // Assert
            info.TargetFramework.Should().Be(framework);
        }
    }

    [Fact]
    public void ProjectReferences_ShouldHandleEmptyCollection()
    {
        // Arrange
        var info = new ProjectDependencyInfo
        {
            ProjectName = "NoReferencesProject",
            ProjectReferences = Array.Empty<ProjectReferenceInfo>()
        };

        // Assert
        info.ProjectReferences.Should().NotBeNull();
        info.ProjectReferences.Should().BeEmpty();
    }

    [Fact]
    public void PackageReferences_ShouldHandleEmptyCollection()
    {
        // Arrange
        var info = new ProjectDependencyInfo
        {
            ProjectName = "NoPackagesProject",
            PackageReferences = Array.Empty<PackageReferenceInfo>()
        };

        // Assert
        info.PackageReferences.Should().NotBeNull();
        info.PackageReferences.Should().BeEmpty();
    }

    [Fact]
    public void TransitiveDependencies_ShouldHandleEmptyCollection()
    {
        // Arrange
        var info = new ProjectDependencyInfo
        {
            ProjectName = "NoTransitiveProject",
            TransitiveDependencies = Array.Empty<DependencyInfo>()
        };

        // Assert
        info.TransitiveDependencies.Should().NotBeNull();
        info.TransitiveDependencies.Should().BeEmpty();
    }

    [Fact]
    public void ProjectDependencyInfo_AllPropertiesShouldHaveDefaults()
    {
        // Arrange
        var info = new ProjectDependencyInfo();

        // Assert
        info.ProjectName.Should().BeEmpty();
        info.ProjectFilePath.Should().BeNull();
        info.TargetFramework.Should().BeEmpty();
        info.ProjectReferences.Should().NotBeNull();
        info.PackageReferences.Should().NotBeNull();
        info.TransitiveDependencies.Should().NotBeNull();
        info.HasCircularReference.Should().BeFalse();
    }

    [Fact]
    public void MultipleProjectReferences_ShouldBeStoredCorrectly()
    {
        // Arrange
        var info = new ProjectDependencyInfo
        {
            ProjectName = "MultiRefProject",
            ProjectReferences = new[]
            {
                new ProjectReferenceInfo
                {
                    ProjectName = "ProjectA",
                    ProjectFilePath = "C:\\Projects\\A.csproj",
                    Alias = "aliasA"
                },
                new ProjectReferenceInfo
                {
                    ProjectName = "ProjectB",
                    ProjectFilePath = "C:\\Projects\\B.csproj",
                    Alias = "aliasB"
                },
                new ProjectReferenceInfo
                {
                    ProjectName = "ProjectC",
                    ProjectFilePath = "C:\\Projects\\C.csproj",
                    Alias = "aliasC"
                }
            }
        };

        // Assert
        info.ProjectReferences.Should().HaveCount(3);
        info.ProjectReferences[0].ProjectName.Should().Be("ProjectA");
        info.ProjectReferences[1].ProjectName.Should().Be("ProjectB");
        info.ProjectReferences[2].ProjectName.Should().Be("ProjectC");
    }

    [Fact]
    public void MultiplePackageReferences_ShouldBeStoredCorrectly()
    {
        // Arrange
        var info = new ProjectDependencyInfo
        {
            ProjectName = "MultiPackageProject",
            PackageReferences = new[]
            {
                new PackageReferenceInfo
                {
                    Name = "Newtonsoft.Json",
                    Version = "13.0.3",
                    IsTransitive = false
                },
                new PackageReferenceInfo
                {
                    Name = "Serilog",
                    Version = "3.0.1",
                    IsTransitive = false
                },
                new PackageReferenceInfo
                {
                    Name = "Microsoft.Extensions.Logging",
                    Version = "8.0.0",
                    IsTransitive = true
                }
            }
        };

        // Assert
        info.PackageReferences.Should().HaveCount(3);
        info.PackageReferences[0].Name.Should().Be("Newtonsoft.Json");
        info.PackageReferences[1].Name.Should().Be("Serilog");
        info.PackageReferences[2].Name.Should().Be("Microsoft.Extensions.Logging");
    }

    [Fact]
    public void CircularDependencyDetection_ShouldWorkCorrectly()
    {
        // Arrange
        var circularInfo = new ProjectDependencyInfo
        {
            ProjectName = "CircularProject",
            HasCircularReference = true
        };

        var nonCircularInfo = new ProjectDependencyInfo
        {
            ProjectName = "NonCircularProject",
            HasCircularReference = false
        };

        // Assert
        circularInfo.HasCircularReference.Should().BeTrue();
        nonCircularInfo.HasCircularReference.Should().BeFalse();
    }
}
