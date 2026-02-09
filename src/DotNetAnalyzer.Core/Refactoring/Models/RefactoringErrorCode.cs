namespace DotNetAnalyzer.Core.Refactoring.Models;

/// <summary>
/// 重构错误代码
/// </summary>
public static class RefactoringErrorCode
{
    // 验证错误
    /// <summary>
    /// 选择无效
    /// </summary>
    public const string INVALID_SELECTION = "INVALID_SELECTION";

    /// <summary>
    /// 符号位置无效
    /// </summary>
    public const string INVALID_SYMBOL_LOCATION = "INVALID_SYMBOL_LOCATION";

    /// <summary>
    /// 名称无效
    /// </summary>
    public const string INVALID_NAME = "INVALID_NAME";

    /// <summary>
    /// 名称冲突
    /// </summary>
    public const string NAME_CONFLICT = "NAME_CONFLICT";

    // 语义错误
    /// <summary>
    /// 类型推断失败
    /// </summary>
    public const string TYPE_INFERENCE_FAILED = "TYPE_INFERENCE_FAILED";

    /// <summary>
    /// 语义变更警告
    /// </summary>
    public const string SEMANTIC_CHANGE_WARNING = "SEMANTIC_CHANGE_WARNING";

    /// <summary>
    /// 无法转换模式
    /// </summary>
    public const string CANNOT_CONVERT_PATTERN = "CANNOT_CONVERT_PATTERN";

    // 操作错误
    /// <summary>
    /// 只读符号
    /// </summary>
    public const string READONLY_SYMBOL = "READONLY_SYMBOL";

    /// <summary>
    /// 存在外部引用
    /// </summary>
    public const string EXTERNAL_REFERENCES = "EXTERNAL_REFERENCES";

    /// <summary>
    /// 集合在循环体中被修改
    /// </summary>
    public const string COLLECTION_MODIFIED_IN_BODY = "COLLECTION_MODIFIED_IN_BODY";

    // 系统错误
    /// <summary>
    /// 内部错误
    /// </summary>
    public const string INTERNAL_ERROR = "INTERNAL_ERROR";

    /// <summary>
    /// 项目加载失败
    /// </summary>
    public const string PROJECT_LOAD_FAILED = "PROJECT_LOAD_FAILED";

    /// <summary>
    /// 编译失败
    /// </summary>
    public const string COMPILATION_FAILED = "COMPILATION_FAILED";

    /// <summary>
    /// 文档未找到
    /// </summary>
    public const string DOCUMENT_NOT_FOUND = "DOCUMENT_NOT_FOUND";
}
