namespace DotNetAnalyzer.Core.Security;

/// <summary>
/// 路径验证失败时抛出的异常
/// </summary>
/// <remarks>
/// 此异常在以下情况抛出：
/// <list type="bullet">
///   <item>路径包含非法字符或格式</item>
///   <item>路径尝试遍历到预期目录之外（路径遍历攻击）</item>
///   <item>文件扩展名不符合预期</item>
///   <item>路径规范后超出基础目录范围</item>
/// </list>
/// </remarks>
public class PathValidationException : Exception
{
    /// <summary>
    /// 获取导致验证失败的路径
    /// </summary>
    public string? InvalidPath { get; }

    /// <summary>
    /// 获取验证失败的原因
    /// </summary>
    public string? ValidationReason { get; }

    /// <summary>
    /// 初始化 <see cref="PathValidationException"/> 类的新实例
    /// </summary>
    /// <param name="message">描述错误的消息</param>
    /// <param name="invalidPath">导致验证失败的路径</param>
    /// <param name="validationReason">验证失败的具体原因</param>
    public PathValidationException(string message, string? invalidPath = null, string? validationReason = null)
        : base(message)
    {
        InvalidPath = invalidPath;
        ValidationReason = validationReason;
    }

    /// <summary>
    /// 初始化 <see cref="PathValidationException"/> 类的新实例，包含内部异常
    /// </summary>
    /// <param name="message">描述错误的消息</param>
    /// <param name="invalidPath">导致验证失败的路径</param>
    /// <param name="validationReason">验证失败的具体原因</param>
    /// <param name="innerException">导致当前异常的内部异常</param>
    public PathValidationException(string message, string invalidPath, string validationReason, Exception innerException)
        : base(message, innerException)
    {
        InvalidPath = invalidPath;
        ValidationReason = validationReason;
    }
}
