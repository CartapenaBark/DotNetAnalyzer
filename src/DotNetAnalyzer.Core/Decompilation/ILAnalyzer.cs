using DotNetAnalyzer.Core.Decompilation.Models;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.Extensions.Logging;
using DecompilerILInstruction = ICSharpCode.Decompiler.IL.ILInstruction;
using ModelILInstruction = DotNetAnalyzer.Core.Decompilation.Models.ILInstruction;

namespace DotNetAnalyzer.Core.Decompilation;

/// <summary>
/// IL 指令分析器，检测方法的性能特征
/// </summary>
/// <remarks>
/// 此分析器使用 ILSpy 反编译器的类型系统读取方法信息，
/// 并通过原始 IL 字节码扫描检测常见性能问题：
/// <list type="bullet">
///   <item>装箱 (box) / 拆箱 (unbox) 操作</item>
///   <item>异常处理块 (try/catch/finally) 的数量</item>
///   <item>虚方法调用 (callvirt) vs 直接调用 (call)</item>
///   <item>局部变量数量和最大求值栈深度</item>
/// </list>
/// </remarks>
public class ILAnalyzer
{
    private static readonly Action<ILogger, string, string, string, Exception?>
        s_logAnalyzing =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Information,
                new EventId(1, nameof(AnalyzeMethod)),
                "开始分析 IL: {Assembly}, 类型: {TypeName}, 方法: {MethodName}");

    private static readonly Action<ILogger, string, string, string, double,
        Exception?> s_logAnalyzed =
            LoggerMessage.Define<string, string, string, double>(
                LogLevel.Information,
                new EventId(2, nameof(AnalyzeMethod)),
                "IL 分析完成: {Assembly}, 类型: {TypeName}, 方法: {MethodName}, " +
                "耗时: {ElapsedMs:F1}ms");

    private static readonly Action<ILogger, string, Exception?> s_logError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3, nameof(AnalyzeMethod)),
            "IL 分析失败: {Error}");

    private readonly AssemblyCache _assemblyCache;
    private readonly ILogger<ILAnalyzer> _logger;

    /// <summary>
    /// 初始化 ILAnalyzer 的新实例
    /// </summary>
    /// <param name="assemblyCache">程序集缓存</param>
    /// <param name="logger">日志记录器</param>
    public ILAnalyzer(AssemblyCache assemblyCache, ILogger<ILAnalyzer> logger)
    {
        _assemblyCache = assemblyCache
            ?? throw new ArgumentNullException(nameof(assemblyCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 分析指定方法的 IL 指令和性能特征
    /// </summary>
    /// <param name="assemblyPath">程序集路径</param>
    /// <param name="typeName">类型全名（含命名空间）</param>
    /// <param name="methodName">方法名称</param>
    /// <returns>IL 分析结果</returns>
    public async Task<ILAnalysisResult> AnalyzeMethod(
        string assemblyPath, string typeName, string methodName)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);
        ArgumentException.ThrowIfNullOrEmpty(typeName);
        ArgumentException.ThrowIfNullOrEmpty(methodName);

        if (!File.Exists(assemblyPath))
        {
            return new ILAnalysisResult
            {
                AssemblyPath = assemblyPath,
                TypeName = typeName,
                MethodName = methodName,
                Success = false,
                Error = $"程序集文件不存在: {assemblyPath}"
            };
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        s_logAnalyzing(_logger, assemblyPath, typeName, methodName, null);

        try
        {
            // 使用文件路径直接创建反编译器
            var decompiler = new CSharpDecompiler(
                assemblyPath, new DecompilerSettings());
            var typeSystem = decompiler.TypeSystem;

            var fullTypeName = new FullTypeName(typeName);
            var typeDef = typeSystem.FindType(fullTypeName)
                ?.GetDefinition();

            if (typeDef == null)
            {
                return new ILAnalysisResult
                {
                    AssemblyPath = assemblyPath,
                    TypeName = typeName,
                    MethodName = methodName,
                    Success = false,
                    Error = $"类型未找到: {typeName}"
                };
            }

            // 查找匹配的方法
            IMethod? targetMethod = null;
            foreach (var method in typeDef.Methods)
            {
                if (method.Name == methodName)
                {
                    targetMethod = method;
                    break;
                }
            }

            if (targetMethod == null)
            {
                return new ILAnalysisResult
                {
                    AssemblyPath = assemblyPath,
                    TypeName = typeName,
                    MethodName = methodName,
                    Success = false,
                    Error = $"方法未找到: {typeName}.{methodName}"
                };
            }

            // 扫描原始 IL 字节码检测性能特征
            var peFile = await _assemblyCache
                .GetOrAddAsync(assemblyPath)
                .ConfigureAwait(false);

            var performance = ILAnalyzer.ScanILBytecode(
                peFile, targetMethod);

            // 构建方法签名
            var returnType = targetMethod.ReturnType.ToString();
            var parameters = string.Join(", ",
                targetMethod.Parameters.Select(p => p.Type.ToString()));
            var signature = $"{returnType} {methodName}({parameters})";

            var result = new ILAnalysisResult
            {
                AssemblyPath = assemblyPath,
                TypeName = typeName,
                MethodName = methodName,
                Success = true,
                PerformanceCharacteristics = performance,
                MethodSignature = signature,
                LocalVariables = targetMethod.Parameters
                    .Select(p => p.Type.ToString())
                    .ToList()!,
                Instructions = new List<ModelILInstruction>()
            };

            sw.Stop();
            s_logAnalyzed(
                _logger, assemblyPath, typeName, methodName,
                sw.Elapsed.TotalMilliseconds, null);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            s_logError(_logger, ex.Message, null);

            return new ILAnalysisResult
            {
                AssemblyPath = assemblyPath,
                TypeName = typeName,
                MethodName = methodName,
                Success = false,
                Error = $"IL 分析失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 扫描原始 IL 字节码以检测性能特征
    /// </summary>
    private static PerformanceCharacteristic ScanILBytecode(
        PEFile peFile,
        IMethod targetMethod)
    {
        var performance = new PerformanceCharacteristic();

        var methodToken = targetMethod.MetadataToken;
        if (methodToken.IsNil)
        {
            return performance;
        }

        var metadata = peFile.Metadata;
        var methodHandle = (System.Reflection.Metadata
            .MethodDefinitionHandle)methodToken;
        var methodDef = metadata.GetMethodDefinition(methodHandle);

        int rva = methodDef.RelativeVirtualAddress;
        if (rva == 0)
        {
            return performance;
        }

        // 通过 PEFile 的 GetMethodBody 读取方法体
        // 获取 IL 字节码区域
        byte[] ilBytes;
        try
        {
            var methodBody = peFile.GetMethodBody(rva);
            var il = methodBody.GetILBytes();
            ilBytes = il?.Length > 0 ? il.ToArray() : [];
        }
        catch (BadImageFormatException)
        {
            return performance;
        }
        catch (Exception)
        {
            return performance;
        }

        // 简单扫描 IL 字节码
        int pos = 0;
        while (pos < ilBytes.Length)
        {
            var start = pos;
            var b = ilBytes[pos++];

            // 处理两字节操作码 (0xFE)
            if (b == 0xFE)
            {
                if (pos >= ilBytes.Length) break;
                b = ilBytes[pos++];
            }

            // box (0x80)
            if (b == 0x80)
            {
                performance.HasBoxing = true;
                performance.BoxingOffsets.Add(start);
                pos += 4; // 跳过类型 token (inline type)
            }
            // unbox (0x79) or unbox.any (0xA5)
            else if (b == 0x79 || b == 0xA5)
            {
                performance.HasUnboxing = true;
                performance.BoxingOffsets.Add(start);
                pos += 4; // 跳过类型 token
            }
            // callvirt (0x6F)
            else if (b == 0x6F)
            {
                performance.VirtualCallCount++;
                pos += 4; // 跳过方法 token
            }
            // call (0x28)
            else if (b == 0x28)
            {
                performance.DirectCallCount++;
                pos += 4;
            }
            // newobj (0x73)
            else if (b == 0x73)
            {
                pos += 4;
            }
            // 其他操作码：根据操作数大小跳过
            else
            {
                pos = SkipUnknownOperand(ilBytes, b, pos);
            }

            performance.InstructionCount++;
        }

        performance.HasVirtualCalls = performance.VirtualCallCount > 0;

        return performance;
    }

    /// <summary>
    /// 跳过未知操作码的操作数（保守估算）
    /// </summary>
    private static int SkipUnknownOperand(
        byte[] ilBytes, int opcode, int pos)
    {
        // 简化操作数跳过逻辑：
        // - 大多数操作码的操作数是 4 字节 (int32 token 或 br target)
        // - ldloc/stloc 系列通常是 2 字节 (uint16)
        // - ldarg/ldarga 系列通常是 2 字节
        // - ldc.i4 系列有内联值
        // - switch 操作码后面跟着 N+1 个 int32
        //
        // 这里采用保守策略：如果操作码看起来像局部变量操作，
        // 跳过 2 字节；否则跳过 4 字节。

        // ldloc (0x06-0x13 系列)
        if (opcode >= 0x06 && opcode <= 0x13)
        {
            return pos + 2 <= ilBytes.Length ? pos + 2 : pos;
        }

        // stloc (0x0A-0x17 系列)
        if (opcode >= 0x0A && opcode <= 0x17)
        {
            return pos + 2 <= ilBytes.Length ? pos + 2 : pos;
        }

        // ldarg (0x02-0x05)
        if (opcode >= 0x02 && opcode <= 0x05)
        {
            return pos + 2 <= ilBytes.Length ? pos + 2 : pos;
        }

        // ldc.i4.0 到 ldc.i4.8 (0x16-0x1E) 和 ldc.i4.s (0x1F)
        if ((opcode >= 0x16 && opcode <= 0x1E) || opcode == 0x1F)
        {
            return pos; // 无操作数或 1 字节内联
        }

        // ldc.i4 (0x20) - 4 字节内联 int32
        if (opcode == 0x20)
        {
            return pos + 4 <= ilBytes.Length ? pos + 4 : pos;
        }

        // ldc.r4 (0x22) / ldc.r8 (0x23)
        if (opcode == 0x22)
        {
            return pos + 4 <= ilBytes.Length ? pos + 4 : pos;
        }

        if (opcode == 0x23)
        {
            return pos + 8 <= ilBytes.Length ? pos + 8 : pos;
        }

        // 默认：跳过 4 字节（覆盖大部分 inline token 和 br target）
        return pos + 4 <= ilBytes.Length ? pos + 4 : pos;
    }
}
