using System.Text.Json;
using DotNetAnalyzer.Core.Json;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 工具基类：提供通用的工具方法和辅助功能
/// </summary>
/// <remarks>
/// 设计原则：
/// 1. 消除重复代码 - 所有工具类共享的通用方法
/// 2. 统一错误处理 - 提供一致的错误响应格式
/// 3. 可扩展性 - 支持未来添加通用功能
/// 4. 静态方法 - 工具类都是静态类，基类方法也应该是静态的
/// </remarks>
public static class BaseTool
{
    /// <summary>
    /// 创建标准的错误响应 JSON 字符串
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <returns>符合项目约定的错误响应 JSON</returns>
    /// <remarks>
    /// 响应格式：
    /// {
    ///   "success": false,
    ///   "error": "错误消息内容"
    /// }
    /// </remarks>
    public static string CreateErrorResponse(string message)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = message
        }, options);
    }

    /// <summary>
    /// 验证文件路径是否存在
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="errorMessage">自定义错误消息（可选）</param>
    /// <returns>如果文件存在返回 null，否则返回错误响应</returns>
    /// <remarks>
    /// 使用场景：快速验证文件是否存在并返回标准错误响应
    ///
    /// 示例：
    /// <code>
    /// var error = ValidateFileExists(filePath);
    /// if (error != null) return error;
    /// </code>
    /// </remarks>
    public static string? ValidateFileExists(string filePath, string? errorMessage = null)
    {
        if (!File.Exists(filePath))
        {
            return CreateErrorResponse(errorMessage ?? $"文件不存在: {filePath}");
        }
        return null;
    }

    /// <summary>
    /// 验证路径是否存在
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>如果路径存在返回 null，否则返回错误响应</returns>
    public static string? ValidatePathExists(string path)
    {
        if (!Path.Exists(path))
        {
            return CreateErrorResponse($"路径不存在: {path}");
        }
        return null;
    }
}
