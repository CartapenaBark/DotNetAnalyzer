using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Localization;
using Microsoft.CodeAnalysis;

namespace DotNetAnalyzer.Core.Tools;

/// <summary>
/// MCP 工具公共基类，提供统一的错误处理和响应序列化
/// </summary>
public abstract class ToolBase
{
    /// <summary>
    /// 处理异常并返回错误响应
    /// </summary>
    /// <param name="ex">异常对象</param>
    /// <param name="operation">操作名称</param>
    /// <param name="culture">文化信息（可选）</param>
    /// <returns>JSON 错误响应</returns>
    protected static string HandleError(Exception ex, string operation, System.Globalization.CultureInfo? culture = null)
    {
        var errorMessage = ErrorMessages.GetErrorMessage(
            operation,
            ex.Message,
            culture ?? System.Globalization.CultureInfo.CurrentCulture);

        return JsonSerializer.Serialize(new
        {
            success = false,
            error = errorMessage
        }, JsonOptions.Default);
    }

    /// <summary>
    /// 序列化成功响应
    /// </summary>
    /// <param name="data">响应数据</param>
    /// <returns>JSON 成功响应</returns>
    protected static string SerializeSuccess(object data)
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            data
        }, JsonOptions.Default);
    }

    /// <summary>
    /// 创建错误响应
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <returns>JSON 错误响应</returns>
    protected static string CreateErrorResponse(string message)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = message
        }, JsonOptions.Default);
    }

    /// <summary>
    /// 验证字符串参数非空
    /// </summary>
    /// <param name="value">参数值</param>
    /// <param name="parameterName">参数名称</param>
    /// <returns>验证结果</returns>
    protected static bool ValidateNotEmpty(string? value, string parameterName)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 验证数值参数非负
    /// </summary>
    /// <param name="value">参数值</param>
    /// <param name="parameterName">参数名称</param>
    /// <returns>验证结果</returns>
    protected static bool ValidateNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 验证文件存在
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>验证结果</returns>
    protected static bool ValidateFileExists(string filePath)
    {
        return File.Exists(filePath);
    }

    /// <summary>
    /// 获取文档（带验证）
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="filePath">文件路径</param>
    /// <returns>文档对象，如果失败返回 null</returns>
    protected static async Task<Document?> GetValidatedDocumentAsync(
        IWorkspaceManager workspaceManager,
        string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        if (!File.Exists(filePath))
        {
            return null;
        }

        var project = await workspaceManager.GetProjectAsync(filePath);
        if (project == null)
        {
            return null;
        }

        return project.Documents.FirstOrDefault(d => d.FilePath == filePath);
    }

    /// <summary>
    /// 获取文档列表（带过滤）
    /// </summary>
    /// <param name="project">项目</param>
    /// <param name="filter">文件过滤器（可选）</param>
    /// <returns>文档列表</returns>
    protected static IEnumerable<Document> GetFilteredDocuments(Project project, string? filter = null)
    {
        var documents = project.Documents;

        if (!string.IsNullOrEmpty(filter))
        {
            var extension = filter.Replace("*", "");
            documents = documents.Where(d => d.FilePath?.EndsWith(extension) == true);
        }

        return documents;
    }
}
