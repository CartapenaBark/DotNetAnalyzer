using System.Reflection.Metadata;
using DotNetAnalyzer.Core.Decompilation.Models;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Decompilation;

/// <summary>
/// 基于 ILSpy 的 C# 反编译服务实现
/// </summary>
/// <remarks>
/// 此服务使用 ICSharpCode.Decompiler 将 .NET 程序集反编译为 C# 源代码，
/// 并通过 <see cref="AssemblyCache"/> 管理 PEFile 的生命周期以提升性能。
/// <para>支持的过滤选项：</para>
/// <list type="bullet">
///   <item>命名空间过滤：仅反编译指定命名空间下的类型</item>
///   <item>类型名称过滤：支持部分匹配的类型名称过滤</item>
///   <item>方法名称过滤：仅反编译包含指定方法的类型</item>
/// </list>
/// </remarks>
public class CSharpDecompilerService : IDecompilationService
{
    private static readonly Action<ILogger, string, Exception?> s_logDecompiling =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(DecompileAsync)),
            "开始反编译程序集: {Path}");

    private static readonly Action<ILogger, string, double, Exception?> s_logDecompiled =
        LoggerMessage.Define<string, double>(
            LogLevel.Information,
            new EventId(2, nameof(DecompileAsync)),
            "程序集反编译完成: {Path}, 耗时: {ElapsedMs:F1}ms");

    private static readonly Action<ILogger, string, int, Exception?> s_logFiltered =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(3, nameof(DecompileAsync)),
            "命名空间过滤 \"{Filter}\" 匹配到 {Count} 个类型");

    private static readonly Action<ILogger, string, Exception?> s_logError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(4, nameof(DecompileAsync)),
            "反编译程序集时发生错误: {Path}");

    private readonly AssemblyCache _assemblyCache;
    private readonly ILogger<CSharpDecompilerService> _logger;

    /// <summary>
    /// 初始化 CSharpDecompilerService 的新实例
    /// </summary>
    /// <param name="assemblyCache">程序集缓存</param>
    /// <param name="logger">日志记录器</param>
    public CSharpDecompilerService(
        AssemblyCache assemblyCache,
        ILogger<CSharpDecompilerService> logger)
    {
        _assemblyCache = assemblyCache
            ?? throw new ArgumentNullException(nameof(assemblyCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<DecompilationResult> DecompileAsync(
        string assemblyPath,
        string? namespaceFilter = null,
        string? typeNameFilter = null,
        string? methodName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            return new DecompilationResult
            {
                Success = false,
                Error = $"程序集文件不存在: {assemblyPath}"
            };
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        s_logDecompiling(_logger, assemblyPath, null);

        try
        {
            // 直接使用文件路径创建反编译器（内部会管理 PEFile 生命周期）
            var settings = CreateDecompilerSettings();
            var decompiler = new CSharpDecompiler(assemblyPath, settings);

            // 收集需要反编译的类型
            var peFile = await _assemblyCache
                .GetOrAddAsync(assemblyPath, cancellationToken)
                .ConfigureAwait(false);

            var typeHandles = CollectTypeHandles(
                peFile, namespaceFilter, typeNameFilter, methodName);

            string sourceCode;

            if (typeHandles.Count == 0 &&
                string.IsNullOrEmpty(namespaceFilter) &&
                string.IsNullOrEmpty(typeNameFilter) &&
                string.IsNullOrEmpty(methodName))
            {
                // 无过滤：全量反编译
                sourceCode = decompiler.DecompileWholeModuleAsString();
            }
            else if (typeHandles.Count > 0)
            {
                // 有过滤条件：反编译匹配的类型
                var syntaxTree = decompiler.DecompileTypes(typeHandles);
                sourceCode = syntaxTree.ToString();
            }
            else
            {
                sourceCode = string.Empty;
            }

            var lineCount = sourceCode
                .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

            sw.Stop();
            s_logDecompiled(
                _logger, assemblyPath, sw.Elapsed.TotalMilliseconds, null);

            return new DecompilationResult
            {
                Success = true,
                SourceCode = sourceCode,
                DecompiledTypeCount = typeHandles.Count > 0
                    ? typeHandles.Count
                    : CountTypesInModule(peFile),
                TotalLines = lineCount
            };
        }
        catch (OperationCanceledException)
        {
            return new DecompilationResult
            {
                Success = false,
                Error = "反编译操作已取消"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            s_logError(_logger, assemblyPath + $": {ex.Message}", null);

            return new DecompilationResult
            {
                Success = false,
                Error = $"反编译失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 创建反编译器设置
    /// </summary>
    private static DecompilerSettings CreateDecompilerSettings()
    {
        return new DecompilerSettings();
    }

    /// <summary>
    /// 根据过滤条件收集需要反编译的类型句柄
    /// </summary>
    private static List<TypeDefinitionHandle>
        CollectTypeHandles(
        PEFile peFile,
        string? namespaceFilter,
        string? typeNameFilter,
        string? methodName)
    {
        var result = new List<TypeDefinitionHandle>();
        var metadata = peFile.Metadata;

        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var typeDef = metadata.GetTypeDefinition(typeHandle);
            var ns = metadata.GetString(typeDef.Namespace);

            // 跳过编译器生成的类型（以 &lt; 或 &gt; 开头）
            var typeName = metadata.GetString(typeDef.Name);
            if (typeName.StartsWith('<') || typeName.StartsWith('>'))
            {
                continue;
            }

            // 命名空间过滤
            if (!string.IsNullOrEmpty(namespaceFilter) &&
                !string.Equals(ns, namespaceFilter,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 类型名称过滤
            if (!string.IsNullOrEmpty(typeNameFilter) &&
                !typeName.Contains(
                    typeNameFilter,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 方法名称过滤
            if (!string.IsNullOrEmpty(methodName) &&
                !MethodExistsInType(metadata, typeDef, methodName))
            {
                continue;
            }

            result.Add(typeHandle);
        }

        return result;
    }

    /// <summary>
    /// 检查类型中是否包含指定名称的方法
    /// </summary>
    private static bool MethodExistsInType(
        System.Reflection.Metadata.MetadataReader metadata,
        System.Reflection.Metadata.TypeDefinition typeDef,
        string methodName)
    {
        foreach (var methodHandle in typeDef.GetMethods())
        {
            var methodDef = metadata.GetMethodDefinition(methodHandle);
            var name = metadata.GetString(methodDef.Name);

            if (name.Contains(methodName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 统计模块中的类型总数
    /// </summary>
    private static int CountTypesInModule(PEFile peFile)
    {
        return peFile.Metadata.TypeDefinitions.Count;
    }
}
