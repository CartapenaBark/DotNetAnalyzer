using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.ProjectManipulation;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// 项目文件编辑 MCP 工具，提供项目引用、NuGet 包和 MSBuild 属性的编辑能力
/// </summary>
[McpServerToolType]
public static class ProjectEditorTools
{
    /// <summary>
    /// 向项目文件添加 ProjectReference 引用
    /// </summary>
    /// <param name="editor">项目文件编辑器</param>
    /// <param name="projectPath">目标项目文件路径（.csproj）</param>
    /// <param name="referencePath">要引用的项目文件路径</param>
    /// <returns>编辑操作结果（JSON 格式）</returns>
    [McpServerTool, Description(
        "向项目文件添加 ProjectReference 引用")]
    public static async Task<string> AddProjectReference(
        ProjectFileEditor editor,
        [Description("目标项目文件路径（.csproj）")] string projectPath,
        [Description("要引用的项目文件路径")] string referencePath)
    {
        try
        {
            var result = await editor
                .AddProjectReference(projectPath, referencePath)
                .ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                result.Success,
                result.Message,
                result.OperationType,
                result.ProjectPath,
                result.BackupPath,
                result.DurationMs,
                result.Error
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                $"添加项目引用时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 向项目文件添加 NuGet 包引用
    /// </summary>
    /// <param name="editor">项目文件编辑器</param>
    /// <param name="packageService">NuGet 包查询服务（用于验证包是否存在）</param>
    /// <param name="projectPath">目标项目文件路径（.csproj）</param>
    /// <param name="packageId">NuGet 包 ID</param>
    /// <param name="version">包版本号</param>
    /// <returns>编辑操作结果（JSON 格式）</returns>
    [McpServerTool, Description(
        "向项目文件添加 NuGet 包引用（PackageReference）")]
    public static async Task<string> AddNuGetPackage(
        ProjectFileEditor editor,
        NuGetPackageService packageService,
        [Description("目标项目文件路径（.csproj）")] string projectPath,
        [Description("NuGet 包 ID")] string packageId,
        [Description("包版本号")] string version)
    {
        try
        {
            var result = await editor
                .AddPackageReference(projectPath, packageId, version)
                .ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                result.Success,
                result.Message,
                result.OperationType,
                result.ProjectPath,
                result.BackupPath,
                result.DurationMs,
                result.Error
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                $"添加 NuGet 包时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 修改项目文件中的 MSBuild 属性值
    /// </summary>
    /// <param name="editor">项目文件编辑器</param>
    /// <param name="projectPath">目标项目文件路径（.csproj）</param>
    /// <param name="propertyName">MSBuild 属性名</param>
    /// <param name="value">属性值</param>
    /// <returns>编辑操作结果（JSON 格式）</returns>
    [McpServerTool, Description(
        "修改项目文件中的 MSBuild 属性值，如果属性不存在则创建，已存在则更新")]
    public static async Task<string> UpdateProjectProperty(
        ProjectFileEditor editor,
        [Description("目标项目文件路径（.csproj）")] string projectPath,
        [Description("MSBuild 属性名")] string propertyName,
        [Description("属性值")] string value)
    {
        try
        {
            var result = await editor
                .ModifyProperty(projectPath, propertyName, value)
                .ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                result.Success,
                result.Message,
                result.OperationType,
                result.ProjectPath,
                result.BackupPath,
                result.DurationMs,
                result.Error
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                $"修改项目属性时出错: {ex.Message}");
        }
    }
}
