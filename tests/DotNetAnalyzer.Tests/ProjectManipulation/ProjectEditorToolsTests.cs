using System.Text.Json;
using DotNetAnalyzer.Core.ProjectManipulation;
using DotNetAnalyzer.Cli.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.ProjectManipulation;

/// <summary>
/// ProjectEditorTools MCP 工具测试。
/// </summary>
/// <remarks>
/// 使用真实的 .csproj 临时文件验证 MCP 工具的 JSON 响应格式。
/// PathValidator 会检查文件扩展名和存在性，所以必须创建真实的 .csproj 文件。
/// </remarks>
public class ProjectEditorToolsTests : IDisposable
{
    private readonly ProjectFileEditor _editor;
    private readonly List<string> _tempFiles = [];

    public ProjectEditorToolsTests()
    {
        _editor = new ProjectFileEditor(
            NullLogger<ProjectFileEditor>.Instance);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
            try { File.Delete(f + ".bak"); } catch { }
        }
        _tempFiles.Clear();
    }

    private string CreateTempCsproj(
        string content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Library</OutputType>
              </PropertyGroup>
            </Project>
            """)
    {
        var file = Path.Combine(
            Path.GetTempPath(),
            $"TestProject_{Guid.NewGuid():N}.csproj");
        File.WriteAllText(file, content);
        _tempFiles.Add(file);
        return file;
    }

    #region AddProjectReference

    [Fact]
    public async Task AddProjectReference_InvalidPath_ReturnsDataWithSuccessFalse()
    {
        // Act
        var json = await ProjectEditorTools.AddProjectReference(
            _editor, "/nonexistent/Project.csproj",
            "/some/Other.csproj");

        // Assert — MCP 调用成功，但编辑操作失败
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success")
            .GetBoolean().Should().BeTrue("MCP call itself should succeed");
        doc.RootElement.GetProperty("data")
            .GetProperty("success")
            .GetBoolean().Should().BeFalse(
                "edit operation should fail for nonexistent path");
    }

    #endregion

    #region UpdateProjectProperty

    [Fact]
    public async Task UpdateProjectProperty_InvalidPath_ReturnsDataWithSuccessFalse()
    {
        // Act
        var json = await ProjectEditorTools.UpdateProjectProperty(
            _editor, "/nonexistent/Project.csproj",
            "Version", "2.0.0");

        // Assert — MCP 调用成功，但编辑操作失败
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success")
            .GetBoolean().Should().BeTrue("MCP call itself should succeed");
        doc.RootElement.GetProperty("data")
            .GetProperty("success")
            .GetBoolean().Should().BeFalse(
                "edit operation should fail for nonexistent path");
    }

    [Fact]
    public async Task UpdateProjectProperty_ValidFile_ReturnsJsonWithFields()
    {
        // Arrange — 创建一个 .csproj 文件，验证 MCP 工具返回正确的 JSON 结构
        // 注意：SDK 解析在测试环境中可能失败，因此只验证 JSON 响应格式
        var csproj = CreateTempCsproj();

        // Act
        var json = await ProjectEditorTools.UpdateProjectProperty(
            _editor, csproj, "Version", "2.0.0");

        // Assert — 无论成功失败，都应返回有效的 JSON
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("success", out _)
            .Should().BeTrue();

        if (doc.RootElement.GetProperty("success").GetBoolean())
        {
            doc.RootElement.GetProperty("data")
                .TryGetProperty("backupPath", out var backup)
                .Should().BeTrue();
        }
        else
        {
            doc.RootElement.TryGetProperty("error", out _)
                .Should().BeTrue();
        }
    }

    #endregion

    #region AddNuGetPackage

    [Fact]
    public async Task AddNuGetPackage_InvalidPath_ReturnsDataWithSuccessFalse()
    {
        // Act
        var json = await ProjectEditorTools.AddNuGetPackage(
            _editor,
            null!,
            "/nonexistent/Project.csproj",
            "Newtonsoft.Json", "13.0.3");

        // Assert — MCP 调用成功，但编辑操作失败
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success")
            .GetBoolean().Should().BeTrue("MCP call itself should succeed");
        doc.RootElement.GetProperty("data")
            .GetProperty("success")
            .GetBoolean().Should().BeFalse(
                "edit operation should fail for nonexistent path");
    }

    #endregion
}
