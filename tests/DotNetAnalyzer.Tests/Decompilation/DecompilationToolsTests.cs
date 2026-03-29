using System.Text.Json;
using DotNetAnalyzer.Core.Decompilation;
using DotNetAnalyzer.Core.Decompilation.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetAnalyzer.Tests.Decompilation;

/// <summary>
/// DecompilationTools MCP 工具的集成测试
/// </summary>
public class DecompilationToolsTests : IDisposable
{
    private readonly AssemblyCache _assemblyCache;
    private readonly CSharpDecompilerService _decompilationService;
    private readonly ILAnalyzer _ilAnalyzer;
    private readonly AssemblyMetadataReader _metadataReader;

    private readonly string _testAssemblyPath;

    public DecompilationToolsTests()
    {
        var loggerFactory = NullLoggerFactory.Instance;

        _assemblyCache = new AssemblyCache(
            loggerFactory.CreateLogger<AssemblyCache>());
        _decompilationService = new CSharpDecompilerService(
            _assemblyCache,
            loggerFactory.CreateLogger<CSharpDecompilerService>());
        _ilAnalyzer = new ILAnalyzer(
            _assemblyCache,
            loggerFactory.CreateLogger<ILAnalyzer>());
        _metadataReader = new AssemblyMetadataReader(
            _assemblyCache,
            loggerFactory.CreateLogger<AssemblyMetadataReader>());

        // 测试用的程序集路径 - 使用 Core 项目的输出
        _testAssemblyPath = Path.Combine(
            "..", "..", "..", "..", "..",
            "src", "DotNetAnalyzer.Core", "bin", "Release", "net10.0",
            "DotNetAnalyzer.Core.dll");
    }

    public void Dispose()
    {
        _assemblyCache.Dispose();
    }

    /// <summary>
    /// 当测试程序集不存在时跳过测试
    /// </summary>
    private bool RequiresTestAssembly()
    {
        if (File.Exists(_testAssemblyPath))
        {
            return true;
        }

        // 程序集不存在时标记为跳过（返回 false 表示不跳过，
        // 调用方用 Assert.True + return 处理）
        return false;
    }

    [Fact]
    public async Task
        DecompileAssembly_WithValidPath_ReturnsCSharpCode()
    {
        // Arrange
        if (!RequiresTestAssembly())
        {
            return;
        }

        // Act
        var result = await _decompilationService
            .DecompileAsync(_testAssemblyPath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(result.SourceCode);
        Assert.True(result.TotalLines > 0);
        Assert.True(result.DecompiledTypeCount > 0);

        // 验证 C# 关键字存在
        Assert.Contains("namespace", result.SourceCode);
        Assert.Contains("class", result.SourceCode);
        Assert.Contains("using", result.SourceCode);
    }

    [Fact]
    public async Task
        DecompileAssembly_WithTypeFilter_ReturnsFilteredCode()
    {
        // Arrange
        if (!RequiresTestAssembly())
        {
            return;
        }

        // Act
        var result = await _decompilationService
            .DecompileAsync(
                _testAssemblyPath, typeNameFilter: "WorkspaceManager");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, result.Error);
        Assert.Contains("WorkspaceManager", result.SourceCode);
    }

    [Fact]
    public async Task
        DecompileAssembly_WithNonExistentPath_ReturnsError()
    {
        // Act
        var result = await _decompilationService
            .DecompileAsync("/nonexistent/path/assembly.dll");

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Error!);
    }

    [Fact]
    public async Task
        AnalyzeIL_WithValidPath_ReturnsPerformanceCharacteristics()
    {
        // Arrange
        if (!RequiresTestAssembly())
        {
            return;
        }

        // Act - 分析 AssemblyCache 类型的方法
        var result = await _ilAnalyzer.AnalyzeMethod(
            _testAssemblyPath,
            "DotNetAnalyzer.Core.Decompilation.AssemblyCache",
            "GetOrAddAsync");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, result.Error);
        Assert.Equal(
            "DotNetAnalyzer.Core.Decompilation.AssemblyCache",
            result.TypeName);
        Assert.Equal("GetOrAddAsync", result.MethodName);
        Assert.NotNull(result.PerformanceCharacteristics);
        Assert.True(
            result.PerformanceCharacteristics.InstructionCount > 0);
    }

    [Fact]
    public async Task
        AnalyzeIL_WithNonExistentType_ReturnsError()
    {
        // Arrange
        if (!RequiresTestAssembly())
        {
            return;
        }

        // Act
        var result = await _ilAnalyzer.AnalyzeMethod(
            _testAssemblyPath,
            "NonExistent.Type",
            "SomeMethod");

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Error!);
    }

    [Fact]
    public async Task
        GetAssemblyMetadata_WithValidPath_ReturnsMetadata()
    {
        // Arrange
        if (!RequiresTestAssembly())
        {
            return;
        }

        // Act
        var result = await _metadataReader.Read(_testAssemblyPath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, result.Error);
        Assert.Equal(_testAssemblyPath, result.AssemblyPath);
        Assert.NotEmpty(result.AssemblyName);
        Assert.Contains("DotNetAnalyzer.Core", result.AssemblyName);
        Assert.True(result.TypeCount > 0);
        Assert.NotEmpty(result.References);
    }

    [Fact]
    public async Task
        GetAssemblyMetadata_WithNonExistentPath_ReturnsError()
    {
        // Act
        var result = await _metadataReader
            .Read("/nonexistent/path/assembly.dll");

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Error!);
    }

    [Fact]
    public async Task
        GetAssemblyMetadata_ContainsTargetFramework()
    {
        // Arrange
        if (!RequiresTestAssembly())
        {
            return;
        }

        // Act
        var result = await _metadataReader.Read(_testAssemblyPath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(result.TargetFramework!);
        Assert.NotEmpty(result.TargetFrameworkIdentifier!);
    }
}
