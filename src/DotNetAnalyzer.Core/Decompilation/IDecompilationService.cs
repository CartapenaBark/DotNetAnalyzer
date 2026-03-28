using DotNetAnalyzer.Core.Decompilation.Models;

namespace DotNetAnalyzer.Core.Decompilation;

/// <summary>
/// 反编译服务接口，提供程序集到 C# 源代码的反编译功能
/// </summary>
/// <remarks>
/// 此接口定义了基于 ILSpy (ICSharpCode.Decompiler) 的反编译核心功能：
/// <list type="bullet">
///   <item>将 .NET 程序集（DLL/EXE）反编译为可读的 C# 源代码</item>
///   <item>支持按命名空间、类型名称和方法名称进行过滤</item>
///   <item>通过缓存机制提升重复反编译的性能</item>
/// </list>
/// </remarks>
public interface IDecompilationService
{
    /// <summary>
    /// 将指定程序集反编译为 C# 源代码
    /// </summary>
    /// <param name="assemblyPath">程序集文件路径（.dll 或 .exe）</param>
    /// <param name="namespaceFilter">
    /// 可选的命名空间过滤条件，仅反编译指定命名空间中的类型。
    /// 为 null 时反编译所有命名空间。
    /// </param>
    /// <param name="typeNameFilter">
    /// 可选的类型名称过滤条件，仅反编译匹配的类型（支持部分匹配）。
    /// 为 null 时反编译所有类型。
    /// </param>
    /// <param name="methodName">
    /// 可选的方法名称过滤条件，仅反编译包含指定方法的类型。
    /// 为 null 时不过滤方法。
    /// </param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反编译结果，包含 C# 源代码和元数据</returns>
    /// <exception cref="FileNotFoundException">
    /// 当程序集文件不存在时抛出
    /// </exception>
    /// <exception cref="ArgumentException">
    /// 当程序集路径无效时抛出
    /// </exception>
    Task<DecompilationResult> DecompileAsync(
        string assemblyPath,
        string? namespaceFilter = null,
        string? typeNameFilter = null,
        string? methodName = null,
        CancellationToken cancellationToken = default);
}
