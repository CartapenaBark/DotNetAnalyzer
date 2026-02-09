using DotNetAnalyzer.Core.Security;
using Xunit;
using FluentAssertions;
using System.Runtime.InteropServices;

namespace DotNetAnalyzer.Tests.Security;

/// <summary>
/// PathValidator 类的单元测试
/// </summary>
public class PathValidatorTests
{
    private readonly string _testDataDir;

    public PathValidatorTests()
    {
        // 获取测试数据目录路径
        var currentDir = Directory.GetCurrentDirectory();
        _testDataDir = Path.Combine(currentDir, "TestData");

        // 确保测试目录存在
        if (!Directory.Exists(_testDataDir))
        {
            Directory.CreateDirectory(_testDataDir);
        }
    }

    #region ValidateAndNormalize Tests

    [Fact]
    public void ValidateAndNormalize_WithNullPath_ShouldThrowException()
    {
        // Act
        var act = () => PathValidator.ValidateAndNormalize(null!);

        // Assert
        act.Should().Throw<PathValidationException>()
            .WithMessage("*路径不能为空*");
    }

    [Fact]
    public void ValidateAndNormalize_WithEmptyPath_ShouldThrowException()
    {
        // Act
        var act = () => PathValidator.ValidateAndNormalize("   ");

        // Assert
        act.Should().Throw<PathValidationException>()
            .WithMessage("*路径不能为空*");
    }

    [Fact]
    public void ValidateAndNormalize_WithRelativePath_ShouldReturnAbsolutePath()
    {
        // Arrange
        var relativePath = "test.csproj";
        var expectedDirectory = Directory.GetCurrentDirectory();

        // Act
        var result = PathValidator.ValidateAndNormalize(relativePath, checkExists: false);

        // Assert
        result.Should().NotBeNullOrEmpty();
        Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void ValidateAndNormalize_WithAbsolutePath_ShouldReturnNormalizedPath()
    {
        // Arrange
        var testFile = Path.Combine(_testDataDir, "test.csproj");
        File.WriteAllText(testFile, "// Test file");

        // Act
        var result = PathValidator.ValidateAndNormalize(testFile);

        // Assert
        result.Should().Be(Path.GetFullPath(testFile));
    }

    [Fact]
    public void ValidateAndNormalize_WithPathTraversal_ShouldThrowException_WhenBasePathProvided()
    {
        // Arrange
        var basePath = _testDataDir;
        var traversalPath = Path.Combine(_testDataDir, "..", "Windows");

        // Act
        var act = () => PathValidator.ValidateAndNormalize(traversalPath, basePath);

        // Assert
        act.Should().Throw<PathValidationException>()
            .WithMessage("*路径超出基础目录范围*");
    }

    [Fact]
    public void ValidateAndNormalize_WithNonExistentPath_WhenCheckExistsIsTrue_ShouldThrowException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDataDir, "nonexistent.csproj");

        // Act
        var act = () => PathValidator.ValidateAndNormalize(nonExistentPath, checkExists: true);

        // Assert
        act.Should().Throw<PathValidationException>()
            .WithMessage("*路径不存在*");
    }

    [Fact]
    public void ValidateAndNormalize_WithNonExistentPath_WhenCheckExistsIsFalse_ShouldReturnNormalizedPath()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDataDir, "nonexistent.csproj");

        // Act
        var result = PathValidator.ValidateAndNormalize(nonExistentPath, checkExists: false);

        // Assert
        result.Should().NotBeNullOrEmpty();
        Path.IsPathRooted(result).Should().BeTrue();
    }

    [Fact]
    public void ValidateAndNormalize_WithDotDotInPath_ShouldNormalize()
    {
        // Arrange
        var subDir = Path.Combine(_testDataDir, "subdir");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "test.csproj");
        File.WriteAllText(testFile, "// Test");

        var pathWithDotDot = Path.Combine(subDir, "..", "subdir", "test.csproj");

        // Act
        var result = PathValidator.ValidateAndNormalize(pathWithDotDot);

        // Assert
        result.Should().Be(Path.GetFullPath(testFile));
    }

    #endregion

    #region ValidateProjectPath Tests

    [Fact]
    public void ValidateProjectPath_WithValidCsproj_ShouldReturnNormalizedPath()
    {
        // Arrange
        var testFile = Path.Combine(_testDataDir, "TestProject.csproj");
        File.WriteAllText(testFile, "<Project />");

        // Act
        var result = PathValidator.ValidateProjectPath(testFile);

        // Assert
        result.Should().Be(Path.GetFullPath(testFile));
        result.Should().EndWith(".csproj");
    }

    [Fact]
    public void ValidateProjectPath_WithInvalidExtension_ShouldThrowException()
    {
        // Arrange
        var testFile = Path.Combine(_testDataDir, "test.txt");
        File.WriteAllText(testFile, "Not a project file");

        // Act
        var act = () => PathValidator.ValidateProjectPath(testFile);

        // Assert
        act.Should().Throw<PathValidationException>()
            .WithMessage("*文件不是有效的 C# 项目文件*");
    }

    [Fact]
    public void ValidateProjectPath_WithNonExistentFile_ShouldThrowException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDataDir, "nonexistent.csproj");

        // Act
        var act = () => PathValidator.ValidateProjectPath(nonExistentFile);

        // Assert
        act.Should().Throw<PathValidationException>();
    }

    [Fact]
    public void ValidateProjectPath_WithCheckExistsFalse_ShouldNotThrowForNonExistentFile()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDataDir, "nonexistent.csproj");

        // Act
        var result = PathValidator.ValidateProjectPath(nonExistentFile, checkExists: false);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().EndWith(".csproj");
    }

    #endregion

    #region ValidateSolutionPath Tests

    [Fact]
    public void ValidateSolutionPath_WithValidSln_ShouldReturnNormalizedPath()
    {
        // Arrange
        var testFile = Path.Combine(_testDataDir, "TestSolution.sln");
        File.WriteAllText(testFile, "Visual Studio Solution");

        // Act
        var result = PathValidator.ValidateSolutionPath(testFile);

        // Assert
        result.Should().Be(Path.GetFullPath(testFile));
    }

    [Fact]
    public void ValidateSolutionPath_WithValidSlnx_ShouldReturnNormalizedPath()
    {
        // Arrange
        var testFile = Path.Combine(_testDataDir, "TestSolution.slnx");
        File.WriteAllText(testFile, "<Solution />");

        // Act
        var result = PathValidator.ValidateSolutionPath(testFile);

        // Assert
        result.Should().Be(Path.GetFullPath(testFile));
    }

    [Fact]
    public void ValidateSolutionPath_WithInvalidExtension_ShouldThrowException()
    {
        // Arrange
        var testFile = Path.Combine(_testDataDir, "test.txt");
        File.WriteAllText(testFile, "Not a solution file");

        // Act
        var act = () => PathValidator.ValidateSolutionPath(testFile);

        // Assert
        act.Should().Throw<PathValidationException>()
            .WithMessage("*文件不是有效的解决方案文件*");
    }

    [Theory]
    [InlineData(".sln")]
    [InlineData(".slnx")]
    public void ValidateSolutionPath_WithValidExtensions_ShouldSucceed(string extension)
    {
        // Arrange
        var testFile = Path.Combine(_testDataDir, $"test{extension}");
        File.WriteAllText(testFile, "Test content");

        // Act
        var result = PathValidator.ValidateSolutionPath(testFile);

        // Assert
        result.Should().EndWith(extension);
    }

    #endregion

    #region ValidateSourceFilePath Tests

    [Fact]
    public void ValidateSourceFilePath_WithValidCsFile_ShouldReturnNormalizedPath()
    {
        // Arrange
        var testFile = Path.Combine(_testDataDir, "TestClass.cs");
        File.WriteAllText(testFile, "public class TestClass {}");

        // Act
        var result = PathValidator.ValidateSourceFilePath(testFile);

        // Assert
        result.Should().Be(Path.GetFullPath(testFile));
    }

    [Fact]
    public void ValidateSourceFilePath_WithValidVbFile_ShouldReturnNormalizedPath()
    {
        // Arrange
        var testFile = Path.Combine(_testDataDir, "TestClass.vb");
        File.WriteAllText(testFile, "Public Class TestClass");

        // Act
        var result = PathValidator.ValidateSourceFilePath(testFile);

        // Assert
        result.Should().Be(Path.GetFullPath(testFile));
    }

    [Fact]
    public void ValidateSourceFilePath_WithValidFsFile_ShouldReturnNormalizedPath()
    {
        // Arrange
        var testFile = Path.Combine(_testDataDir, "TestClass.fs");
        File.WriteAllText(testFile, "module TestModule");

        // Act
        var result = PathValidator.ValidateSourceFilePath(testFile);

        // Assert
        result.Should().Be(Path.GetFullPath(testFile));
    }

    [Fact]
    public void ValidateSourceFilePath_WithInvalidExtension_ShouldThrowException()
    {
        // Arrange
        var testFile = Path.Combine(_testDataDir, "test.txt");
        File.WriteAllText(testFile, "Not a source file");

        // Act
        var act = () => PathValidator.ValidateSourceFilePath(testFile);

        // Assert
        act.Should().Throw<PathValidationException>()
            .WithMessage("*文件不是有效的源代码文件*");
    }

    #endregion

    #region ContainsPathTraversalPatterns Tests

    [Fact]
    public void ContainsPathTraversalPatterns_WithDoubleDot_ShouldReturnTrue()
    {
        // Arrange
        var path = Path.Combine("dir", "..", "file.txt");

        // Act
        var result = PathValidator.ContainsPathTraversalPatterns(path);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ContainsPathTraversalPatterns_WithNormalPath_ShouldReturnFalse()
    {
        // Arrange
        var path = Path.Combine("dir", "subdir", "file.txt");

        // Act
        var result = PathValidator.ContainsPathTraversalPatterns(path);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ContainsPathTraversalPatterns_WithNullPath_ShouldReturnFalse()
    {
        // Act
        var result = PathValidator.ContainsPathTraversalPatterns(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ContainsPathTraversalPatterns_WithEmptyPath_ShouldReturnFalse()
    {
        // Act
        var result = PathValidator.ContainsPathTraversalPatterns("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ContainsPathTraversalPatterns_WithMultipleDoubleDots_ShouldReturnTrue()
    {
        // Arrange
        var path = Path.Combine("dir", "..", "..", "file.txt");

        // Act
        var result = PathValidator.ContainsPathTraversalPatterns(path);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void PathValidator_WithComplexPath_ShouldHandleCorrectly()
    {
        // Arrange
        var subDir = Path.Combine(_testDataDir, "level1", "level2");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "test.csproj");
        File.WriteAllText(testFile, "<Project />");

        // Act
        var result = PathValidator.ValidateProjectPath(testFile);

        // Assert
        result.Should().Be(Path.GetFullPath(testFile));
        File.Exists(result).Should().BeTrue();
    }

    [Fact]
    public void PathValidator_WithBasePath_ShouldEnforceBoundary()
    {
        // Arrange
        var basePath = Path.Combine(_testDataDir, "allowed");
        Directory.CreateDirectory(basePath);

        var testFile = Path.Combine(basePath, "test.csproj");
        File.WriteAllText(testFile, "<Project />");

        // Act - 应该成功
        var result = PathValidator.ValidateProjectPath(testFile, basePath);

        // Assert
        result.Should().Be(Path.GetFullPath(testFile));
    }

    #endregion
}
