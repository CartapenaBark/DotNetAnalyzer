using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.ProjectManipulation;
using DotNetAnalyzer.Resources;
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
        ToolStrings.AddProjectReference)]
    public static async Task<string> AddProjectReference(
        ProjectFileEditor editor,
        [Description(ToolStrings.TargetProjectFilePathParam)] string projectPath,
        [Description(ToolStrings.ReferencePathParam)] string referencePath)
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
                ToolStrings.ErrorAddingProjectReference(ex.Message));
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
        ToolStrings.AddNuGetPackage)]
    public static async Task<string> AddNuGetPackage(
        ProjectFileEditor editor,
        NuGetPackageService packageService,
        [Description(ToolStrings.TargetProjectFilePathParam)] string projectPath,
        [Description(ToolStrings.PackageIdParam)] string packageId,
        [Description(ToolStrings.PackageVersionParam)] string version)
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
                ToolStrings.ErrorAddingNuGetPackage(ex.Message));
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
        ToolStrings.UpdateProjectProperty)]
    public static async Task<string> UpdateProjectProperty(
        ProjectFileEditor editor,
        [Description(ToolStrings.TargetProjectFilePathParam)] string projectPath,
        [Description(ToolStrings.PropertyNameParam)] string propertyName,
        [Description(ToolStrings.PropertyValueParam)] string value)
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
                ToolStrings.ErrorUpdatingProjectProperty(ex.Message));
        }
    }
}
