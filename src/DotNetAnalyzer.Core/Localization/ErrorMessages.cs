using System.Globalization;

namespace DotNetAnalyzer.Core.Localization;

/// <summary>
/// 错误消息本地化
/// </summary>
public static class ErrorMessages
{
    /// <summary>
    /// 获取错误消息
    /// </summary>
    /// <param name="operation">操作名称</param>
    /// <param name="details">详细信息</param>
    /// <param name="culture">文化信息</param>
    /// <returns>本地化的错误消息</returns>
    public static string GetErrorMessage(
        string operation,
        string details,
        CultureInfo culture)
    {
        var cultureName = culture.Name;

        if (cultureName.StartsWith("zh"))
        {
            return $"{operation}时出错: {details}";
        }
        else
        {
            return $"Error in {operation}: {details}";
        }
    }

    /// <summary>
    /// 文件未找到错误
    /// </summary>
    public static string FileNotFound(string filePath, CultureInfo culture)
    {
        return culture.Name.StartsWith("zh")
            ? $"找不到文件: {filePath}"
            : $"File not found: {filePath}";
    }

    /// <summary>
    /// 无效参数错误
    /// </summary>
    public static string InvalidParameter(string paramName, CultureInfo culture)
    {
        return culture.Name.StartsWith("zh")
            ? $"参数无效: {paramName}"
            : $"Invalid parameter: {paramName}";
    }

    /// <summary>
    /// 参数为空错误
    /// </summary>
    public static string ParameterCannotBeEmpty(string paramName, CultureInfo culture)
    {
        return culture.Name.StartsWith("zh")
            ? $"{paramName}不能为空"
            : $"{paramName} cannot be empty";
    }

    /// <summary>
    /// 无法加载项目错误
    /// </summary>
    public static string CannotLoadProject(string projectPath, CultureInfo culture)
    {
        return culture.Name.StartsWith("zh")
            ? $"无法加载项目: {projectPath}"
            : $"Cannot load project: {projectPath}";
    }

    /// <summary>
    /// 无法获取语义模型错误
    /// </summary>
    public static string CannotGetSemanticModel(CultureInfo culture)
    {
        return culture.Name.StartsWith("zh")
            ? "无法获取语义模型"
            : "Cannot get semantic model";
    }

    /// <summary>
    /// 无法解析符号错误
    /// </summary>
    public static string CannotResolveSymbol(CultureInfo culture)
    {
        return culture.Name.StartsWith("zh")
            ? "无法解析该位置的符号"
            : "Cannot resolve symbol at this location";
    }
}
