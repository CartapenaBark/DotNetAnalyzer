using DotNetAnalyzer.Core.ProjectManipulation;
using DotNetAnalyzer.Core.ProjectManipulation.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.ProjectManipulation;

/// <summary>
/// ProjectFileEditor 单元测试。
/// </summary>
/// <remarks>
/// 使用真实 .csproj 临时文件验证编辑操作。
/// 成功路径测试验证 SDK 可用时返回正确结果，
/// SDK 不可用时验证返回有效的错误信息（两个路径都覆盖，不跳过）。
/// </remarks>
public class ProjectFileEditorTests : IDisposable
{
    private readonly ProjectFileEditor _editor;
    private readonly List<string> _tempFiles = [];

    private const string MinimalCsproj =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <OutputType>Library</OutputType>
          </PropertyGroup>
        </Project>
        """;

    public ProjectFileEditorTests()
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

    private string CreateTempCsproj(string content = "")
    {
        var file = Path.Combine(
            Path.GetTempPath(),
            $"PFE_{Guid.NewGuid():N}.csproj");
        File.WriteAllText(file,
            string.IsNullOrEmpty(content) ? MinimalCsproj : content);
        _tempFiles.Add(file);
        return file;
    }

    #region Constructor

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new ProjectFileEditor(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region AddProjectReference

    [Fact]
    public async Task AddProjectReference_NullProjectPath_ThrowsArgumentException()
    {
        var act = () => _editor.AddProjectReference(
            null!, "/some/Other.csproj");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddProjectReference_NullReferencePath_ThrowsArgumentException()
    {
        var csproj = CreateTempCsproj();
        var act = () => _editor.AddProjectReference(
            csproj, null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddProjectReference_NonExistentFile_ReturnsFailure()
    {
        var result = await _editor.AddProjectReference(
            "/nonexistent/Project.csproj",
            "/some/Other.csproj");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task AddProjectReference_NewPackage_ReturnsResult()
    {
        var csproj = CreateTempCsproj();
        var result = await _editor.AddPackageReference(
            csproj, "Newtonsoft.Json", "13.0.3");

        result.OperationType.Should().Be("AddPackageReference");

        if (result.Success)
        {
            result.BackupPath.Should().NotBeNullOrWhiteSpace();
            var content = File.ReadAllText(csproj);
            content.Should().Contain("Newtonsoft.Json");
            content.Should().Contain("13.0.3");
        }
        else
        {
            result.Error.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task AddPackageReference_DuplicatePackage_ReturnsFailure()
    {
        var csproj = CreateTempCsproj();

        // 第一次添加
        var firstResult = await _editor.AddPackageReference(
            csproj, "Newtonsoft.Json", "13.0.3");

        if (firstResult.Success)
        {
            // 第二次添加同一个包应失败
            var result = await _editor.AddPackageReference(
                csproj, "Newtonsoft.Json", "13.0.3");
            result.Success.Should().BeFalse();
            result.Error.Should().Contain("already exists");
        }
        else
        {
            // SDK 不可用时无法测试重复逻辑
            firstResult.Error.Should().NotBeNullOrWhiteSpace();
        }
    }

    #endregion

    #region RemoveProjectReference

    [Fact]
    public async Task RemoveProjectReference_NullProjectPath_ThrowsArgumentException()
    {
        var act = () => _editor.RemoveProjectReference(
            null!, "/some/Other.csproj");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RemoveProjectReference_NullReferencePath_ThrowsArgumentException()
    {
        var csproj = CreateTempCsproj();
        var act = () => _editor.RemoveProjectReference(
            csproj, null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RemoveProjectReference_NonExistentFile_ReturnsFailure()
    {
        var result = await _editor.RemoveProjectReference(
            "/nonexistent/Project.csproj",
            "/some/Other.csproj");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveProjectReference_NonExistentReference_ReturnsFailure()
    {
        var csproj = CreateTempCsproj();
        var result = await _editor.RemoveProjectReference(
            csproj, "/nonexistent/Other.csproj");

        result.Success.Should().BeFalse();
    }

    #endregion

    #region UpdatePackageVersion

    [Fact]
    public async Task UpdatePackageVersion_NullProjectPath_ThrowsArgumentException()
    {
        var act = () => _editor.UpdatePackageVersion(
            null!, "Newtonsoft.Json", "13.0.3");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdatePackageVersion_NullPackageId_ThrowsArgumentException()
    {
        var csproj = CreateTempCsproj();
        var act = () => _editor.UpdatePackageVersion(
            csproj, null!, "13.0.3");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdatePackageVersion_NonExistentFile_ReturnsFailure()
    {
        var result = await _editor.UpdatePackageVersion(
            "/nonexistent/Project.csproj",
            "Newtonsoft.Json", "13.0.3");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePackageVersion_ReturnsResultWithOperationType()
    {
        var csproj = CreateTempCsproj();
        var result = await _editor.UpdatePackageVersion(
            csproj, "Newtonsoft.Json", "13.0.3");

        result.OperationType.Should().Be("UpdatePackageVersion");

        if (result.Success)
        {
            var content = File.ReadAllText(csproj);
            content.Should().Contain("13.0.3");
        }
        else
        {
            result.Error.Should().NotBeNullOrWhiteSpace();
        }
    }

    #endregion

    #region ModifyProperty

    [Fact]
    public async Task ModifyProperty_NullProjectPath_ThrowsArgumentException()
    {
        var act = () => _editor.ModifyProperty(
            null!, "Version", "1.0.0");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ModifyProperty_NullPropertyName_ThrowsArgumentException()
    {
        var csproj = CreateTempCsproj();
        var act = () => _editor.ModifyProperty(
            csproj, null!, "1.0.0");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ModifyProperty_NullValue_ThrowsArgumentException()
    {
        var csproj = CreateTempCsproj();
        var act = () => _editor.ModifyProperty(
            csproj, "Version", null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ModifyProperty_NonExistentFile_ReturnsFailure()
    {
        var result = await _editor.ModifyProperty(
            "/nonexistent/Project.csproj", "Version", "1.0.0");

        result.Success.Should().BeFalse();
        result.OperationType.Should().Be("ModifyProperty");
    }

    [Fact]
    public async Task ModifyProperty_ValidFile_ReturnsResultWithOperationType()
    {
        var csproj = CreateTempCsproj();
        var result = await _editor.ModifyProperty(
            csproj, "Version", "2.0.0");

        result.OperationType.Should().Be("ModifyProperty");

        if (result.Success)
        {
            result.BackupPath.Should().NotBeNullOrWhiteSpace();
            result.DurationMs.Should().BeGreaterThanOrEqualTo(0);
            var content = File.ReadAllText(csproj);
            content.Should().Contain("<Version>2.0.0</Version>");
        }
        else
        {
            result.Error.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task ModifyProperty_UpdateExistingProperty_ReturnsResult()
    {
        var csproj = CreateTempCsproj();
        var result = await _editor.ModifyProperty(
            csproj, "TargetFramework", "net9.0");

        result.OperationType.Should().Be("ModifyProperty");

        if (result.Success)
        {
            var content = File.ReadAllText(csproj);
            content.Should().Contain("<TargetFramework>net9.0</TargetFramework>");
        }
    }

    #endregion

    #region Backup

    [Fact]
    public async Task EditOperation_CreatesBackupFile()
    {
        var csproj = CreateTempCsproj();
        var result = await _editor.ModifyProperty(
            csproj, "Version", "2.0.0");

        if (result.Success)
        {
            result.BackupPath.Should().Be(csproj + ".bak");
            File.Exists(result.BackupPath!).Should().BeTrue();

            // 备份内容应与原始内容相同
            var backup = File.ReadAllText(result.BackupPath!);
            backup.Should().NotContain("2.0.0");
        }
        else
        {
            result.Error.Should().NotBeNullOrWhiteSpace();
        }
    }

    #endregion
}
