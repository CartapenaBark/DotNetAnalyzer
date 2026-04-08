using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Xaml;
using DotNetAnalyzer.Resources;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// XAML 分析 MCP 工具，提供 XAML 文件解析、Binding 验证、资源分析和 View-ViewModel 映射
/// </summary>
[McpServerToolType]
public static class XamlTools
{
    /// <summary>
    /// 解析 XAML 文件为结构化模型
    /// </summary>
    /// <param name="parser">XAML 解析器</param>
    /// <param name="xamlFilePath">XAML 文件路径</param>
    /// <returns>XAML 文档结构信息（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.AnalyzeXaml)]
    public static async Task<string> AnalyzeXaml(
        XamlParser parser,
        [Description(ToolStrings.XamlFilePathParam)] string xamlFilePath)
    {
        try
        {
            var xamlInfo = await parser.ParseAsync(xamlFilePath)
                .ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                xamlInfo.FilePath,
                xamlInfo.RootElement,
                xamlInfo.ClassAttribute,
                xamlInfo.Namespaces,
                xamlInfo.Elements,
                xamlInfo.Bindings,
                xamlInfo.ResourceReferences,
                summary = new
                {
                    elementCount = xamlInfo.Elements.Count,
                    bindingCount = xamlInfo.Bindings.Count,
                    resourceRefCount =
                        xamlInfo.ResourceReferences.Count,
                    namespaceCount = xamlInfo.Namespaces.Count
                }
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorAnalyzingXaml(ex.Message));
        }
    }

    /// <summary>
    /// 验证 XAML 文件中的 Binding 表达式
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="parser">XAML 解析器</param>
    /// <param name="validator">Binding 验证器</param>
    /// <param name="xamlFilePath">XAML 文件路径</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>Binding 验证结果（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.ValidateBindings)]
    public static async Task<string> ValidateBindings(
        IWorkspaceManager workspaceManager,
        XamlParser parser,
        XamlBindingValidator validator,
        [Description(ToolStrings.XamlFilePathParam)] string xamlFilePath,
        [Description(ToolStrings.ProjectFilePathParam)] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    ToolStrings.FailedToLoadProject(projectPath));
            }

            var xamlInfo = await parser.ParseAsync(xamlFilePath)
                .ConfigureAwait(false);

            var result = await validator
                .ValidateAsync(xamlInfo, project)
                .ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                xamlFilePath,
                totalBindings = result.TotalBindings,
                validBindings = result.ValidBindings.Count,
                invalidBindings = result.InvalidBindings.Count,
                validBindingsList = result.ValidBindings.Select(v =>
                    new
                    {
                        v.BindingInfo.Path,
                        v.BindingInfo.BindingType,
                        v.BindingInfo.HostElementName,
                        v.BindingInfo.Line,
                        v.IsValid
                    }),
                invalidBindingsList = result.InvalidBindings
                    .Select(v => new
                    {
                        v.BindingInfo.Path,
                        v.BindingInfo.BindingType,
                        v.BindingInfo.HostElementName,
                        v.BindingInfo.Line,
                        v.IsValid,
                        v.ErrorMessage
                    })
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorValidatingBindings(ex.Message));
        }
    }

    /// <summary>
    /// 分析项目中的 XAML 资源定义和引用
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="analyzer">XAML 资源分析器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>资源分析结果（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.AnalyzeXamlResources)]
    public static async Task<string> AnalyzeXamlResources(
        IWorkspaceManager workspaceManager,
        XamlResourceAnalyzer analyzer,
        [Description(ToolStrings.ProjectFilePathParam)] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    ToolStrings.FailedToLoadProject(projectPath));
            }

            var result = await analyzer.AnalyzeAsync(project)
                .ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                totalDefinedResources =
                    result.TotalDefinedResources,
                totalReferences = result.TotalReferences,
                hasErrors = result.HasErrors,
                issues = result.Issues.Select(i => new
                {
                    i.IssueType,
                    i.Severity,
                    i.Key,
                    i.Message,
                    i.FilePath,
                    i.Line
                }),
                definedResources = result.DefinedResources
                    .Select(r => new
                    {
                        r.Key,
                        r.ResourceType,
                        r.FilePath,
                        r.Line
                    })
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorAnalyzingXamlResources(ex.Message));
        }
    }

    /// <summary>
    /// 映射项目中的 View-ViewModel 关联关系
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="mapper">View-ViewModel 映射器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>View-ViewModel 映射结果（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.MapViewViewModel)]
    public static async Task<string> MapViewViewModel(
        IWorkspaceManager workspaceManager,
        ViewModelMapper mapper,
        [Description(ToolStrings.ProjectFilePathParam)] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    ToolStrings.FailedToLoadProject(projectPath));
            }

            var result = await mapper.MapAsync(project)
                .ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                totalMappings = result.TotalMappings,
                mappings = result.Mappings.Select(m => new
                {
                    m.ViewFilePath,
                    m.ViewClassName,
                    m.ViewModelClassName,
                    m.ViewModelFilePath,
                    m.MappingSource
                })
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorMappingViewViewModel(ex.Message));
        }
    }
}
