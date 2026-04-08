using System.ComponentModel;
using System.Reflection.Metadata;
using System.Text.Json;
using DotNetAnalyzer.Core.Decompilation;
using DotNetAnalyzer.Core.Decompilation.Models;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Resources;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 工具类：提供基于 ILSpy 的程序集反编译和分析功能
/// </summary>
[McpServerToolType]
public static class DecompilationTools
{
    /// <summary>
    /// 反编译 .NET 程序集为 C# 源代码
    /// </summary>
    [McpServerTool, Description(ToolStrings.DecompileAssembly)]
    public static async Task<string> DecompileAssembly(
        IDecompilationService decompilationService,
        [Description(ToolStrings.AssemblyPathParam)] string assemblyPath,
        [Description(ToolStrings.OptionalTypeNameFilterParam)]
        string? typeName = null)
    {
        try
        {
            var error = ValidateAssemblyPath(assemblyPath);
            if (error != null)
            {
                return error;
            }

            var result = await decompilationService
                .DecompileAsync(assemblyPath, typeNameFilter: typeName)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                return BaseTool.CreateErrorResponse(result.Error
                    ?? ToolStrings.DecompileFailed());
            }

            // 限制输出长度，避免过大响应
            var sourceCode = result.SourceCode;
            bool truncated = false;
            const int maxLength = 500_000;

            if (sourceCode.Length > maxLength)
            {
                sourceCode = sourceCode[..maxLength]
                    + "\n// " + ToolStrings.SourceCodeTruncated();
                truncated = true;
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    sourceCode,
                    decompiledTypeCount = result
                        .DecompiledTypeCount,
                    totalLines = result.TotalLines,
                    truncated
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorDecompilingAssembly(ex.Message));
        }
    }

    /// <summary>
    /// 分析程序集的 IL 字节码性能特征
    /// </summary>
    [McpServerTool, Description(ToolStrings.AnalyzeIL)]
    public static async Task<string> AnalyzeIL(
        AssemblyCache assemblyCache,
        ILogger<ILAnalyzer> logger,
        [Description(ToolStrings.AssemblyPathParam)] string assemblyPath,
        [Description(ToolStrings.OptionalTypeNameFilterParam)]
        string? typeName = null)
    {
        try
        {
            var error = ValidateAssemblyPath(assemblyPath);
            if (error != null)
            {
                return error;
            }

            var analyzer = new ILAnalyzer(assemblyCache, logger);
            var peFile = await assemblyCache
                .GetOrAddAsync(assemblyPath)
                .ConfigureAwait(false);

            var decompiler = new CSharpDecompiler(
                assemblyPath, new DecompilerSettings());
            var typeSystem = decompiler.TypeSystem;

            var results = new List<ILAnalysisResult>();
            var analyzedMethods = 0;

            foreach (var typeHandle in peFile.Metadata.TypeDefinitions)
            {
                var typeDef = peFile.Metadata
                    .GetTypeDefinition(typeHandle);
                var ns = peFile.Metadata.GetString(typeDef.Namespace);
                var currentTypeName = peFile.Metadata
                    .GetString(typeDef.Name);

                // 跳过编译器生成的类型
                if (currentTypeName.StartsWith('<') ||
                    currentTypeName.StartsWith('>'))
                {
                    continue;
                }

                // 类型名称过滤
                if (!string.IsNullOrEmpty(typeName) &&
                    !currentTypeName.Contains(typeName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fullTypeName = string.IsNullOrEmpty(ns)
                    ? currentTypeName
                    : $"{ns}.{currentTypeName}";

                var fullType = typeSystem.FindType(
                    new FullTypeName(fullTypeName))
                    ?.GetDefinition();

                if (fullType == null)
                {
                    continue;
                }

                foreach (var method in fullType.Methods)
                {
                    // 跳过编译器生成的方法
                    if (method.Name.StartsWith('<'))
                    {
                        continue;
                    }

                    var ilResult = await analyzer
                        .AnalyzeMethod(
                            assemblyPath, fullTypeName, method.Name)
                        .ConfigureAwait(false);

                    if (ilResult.Success)
                    {
                        results.Add(ilResult);
                        analyzedMethods++;
                    }
                }
            }

            // 汇总性能特征
            var summary = AggregatePerformance(results);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    assemblyPath,
                    analyzedMethods,
                    typeNameFilter = typeName,
                    summary,
                    methods = results.Select(r => new
                    {
                        r.TypeName,
                        r.MethodName,
                        r.MethodSignature,
                        performance = r.PerformanceCharacteristics
                    })
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorAnalyzingIL(ex.Message));
        }
    }

    /// <summary>
    /// 读取程序集元数据
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetAssemblyMetadata)]
    public static async Task<string> GetAssemblyMetadata(
        AssemblyMetadataReader metadataReader,
        [Description(ToolStrings.AssemblyPathParam)] string assemblyPath)
    {
        try
        {
            var error = ValidateAssemblyPath(assemblyPath);
            if (error != null)
            {
                return error;
            }

            var result = await metadataReader.Read(assemblyPath)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                return BaseTool.CreateErrorResponse(result.Error
                    ?? ToolStrings.ReadMetadataFailed());
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    assemblyPath = result.AssemblyPath,
                    assemblyName = result.AssemblyName,
                    version = result.Version,
                    targetFramework = result.TargetFramework,
                    targetFrameworkIdentifier =
                        result.TargetFrameworkIdentifier,
                    targetFrameworkVersion =
                        result.TargetFrameworkVersion,
                    typeCount = result.TypeCount,
                    references = result.References.Select(r => new
                    {
                        r.Name,
                        r.Version,
                        r.PublicKeyToken,
                        r.IsStrongNamed
                    }),
                    compatibilityIssues =
                        result.CompatibilityIssues,
                    missingDependencies =
                        result.MissingDependencies
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorGettingAssemblyMetadata(ex.Message));
        }
    }

    /// <summary>
    /// 提取程序集的公共 API surface
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetApiSurface)]
    public static async Task<string> GetApiSurface(
        AssemblyCache assemblyCache,
        [Description(ToolStrings.AssemblyPathParam)] string assemblyPath,
        [Description(ToolStrings.AccessibilityParam)]
        string? accessibility = null)
    {
        try
        {
            var error = ValidateAssemblyPath(assemblyPath);
            if (error != null)
            {
                return error;
            }

            var peFile = await assemblyCache
                .GetOrAddAsync(assemblyPath)
                .ConfigureAwait(false);

            var decompiler = new CSharpDecompiler(
                assemblyPath, new DecompilerSettings());
            var typeSystem = decompiler.TypeSystem;

            var surfaceItems = new List<ApiSurfaceItem>();

            foreach (var typeHandle in peFile.Metadata.TypeDefinitions)
            {
                var typeDef = peFile.Metadata
                    .GetTypeDefinition(typeHandle);
                var ns = peFile.Metadata.GetString(typeDef.Namespace);
                var name = peFile.Metadata.GetString(typeDef.Name);

                // 跳过编译器生成的类型
                if (name.StartsWith('<') || name.StartsWith('>'))
                {
                    continue;
                }

                var fullTypeName = string.IsNullOrEmpty(ns)
                    ? name
                    : $"{ns}.{name}";

                var typeDefSymbol = typeSystem.FindType(
                    new FullTypeName(fullTypeName))
                    ?.GetDefinition();

                if (typeDefSymbol == null)
                {
                    continue;
                }

                var typeAccessibility = typeDefSymbol
                    .Accessibility.ToString();

                // 可访问性过滤
                if (!string.IsNullOrEmpty(accessibility) &&
                    !string.Equals(typeAccessibility, accessibility,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var item = new ApiSurfaceItem
                {
                    TypeName = fullTypeName,
                    TypeKind = GetEntityKind(typeDefSymbol),
                    Accessibility = typeAccessibility,
                    Namespace = string.IsNullOrEmpty(ns)
                        ? null : ns,
                    BaseType = typeDefSymbol.DirectBaseTypes
                        .FirstOrDefault()?.FullName,
                    Interfaces = typeDefSymbol
                        .DirectBaseTypes
                        .Where(t => t.Kind == TypeKind.Interface)
                        .Select(i => i.FullName)
                        .ToList(),
                    IsGeneric = typeDefSymbol.TypeParameters.Count > 0
                };

                // 提取成员
                foreach (var member in typeDefSymbol.Members)
                {
                    var memberAccessibility = member
                        .Accessibility.ToString();

                    // 如果指定了过滤，只包含匹配的成员
                    if (!string.IsNullOrEmpty(accessibility) &&
                        !string.Equals(memberAccessibility,
                            accessibility,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var surfaceMember = new ApiSurfaceMember
                    {
                        Name = member.Name,
                        MemberType = member.SymbolKind.ToString(),
                        Accessibility = memberAccessibility,
                        IsStatic = member.IsStatic,
                        IsVirtual = member.IsVirtual,
                        IsAbstract = member.IsAbstract
                    };

                    if (member is IMethod { ReturnType: not null } m)
                    {
                        surfaceMember.ReturnType =
                            m.ReturnType.FullName;
                    }

                    item.Members.Add(surfaceMember);
                }

                surfaceItems.Add(item);
            }

            var totalCount = surfaceItems.Sum(s => s.Members.Count);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    assemblyPath,
                    accessibilityFilter = accessibility,
                    types = surfaceItems.Select(s => new
                    {
                        s.TypeName,
                        s.TypeKind,
                        s.Accessibility,
                        s.Namespace,
                        s.BaseType,
                        s.Interfaces,
                        s.IsGeneric,
                        memberCount = s.Members.Count,
                        members = s.Members
                    }),
                    summary = new
                    {
                        totalTypes = surfaceItems.Count,
                        totalMembers = totalCount,
                        accessibilityFilter = accessibility
                    }
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorGettingApiSurface(ex.Message));
        }
    }

    #region Helper Methods

    /// <summary>
    /// 验证程序集路径的有效性
    /// </summary>
    private static string? ValidateAssemblyPath(string assemblyPath)
    {
        if (string.IsNullOrEmpty(assemblyPath))
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.AssemblyPathRequired());
        }

        var ext = Path.GetExtension(assemblyPath)
            .ToLowerInvariant();
        if (ext != ".dll" && ext != ".exe")
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.AssemblyFileMustBeDllOrExe());
        }

        return BaseTool.ValidateFileExists(assemblyPath);
    }

    /// <summary>
    /// 汇总多个方法的性能特征
    /// </summary>
    private static object AggregatePerformance(
        List<ILAnalysisResult> results)
    {
        var hasBoxing = results.Any(r =>
            r.PerformanceCharacteristics.HasBoxing);
        var hasUnboxing = results.Any(r =>
            r.PerformanceCharacteristics.HasUnboxing);
        var hasVirtualCalls = results.Any(r =>
            r.PerformanceCharacteristics.HasVirtualCalls);
        var totalVirtualCalls = results.Sum(r =>
            r.PerformanceCharacteristics.VirtualCallCount);
        var totalDirectCalls = results.Sum(r =>
            r.PerformanceCharacteristics.DirectCallCount);
        var totalInstructions = results.Sum(r =>
            r.PerformanceCharacteristics.InstructionCount);

        return new
        {
            methodCount = results.Count,
            hasBoxing,
            hasUnboxing,
            hasVirtualCalls,
            totalVirtualCalls,
            totalDirectCalls,
            totalInstructions,
            boxingMethods = results.Where(r =>
                r.PerformanceCharacteristics.HasBoxing)
                .Select(r => $"{r.TypeName}.{r.MethodName}")
                .ToList(),
            heavyVirtualCallMethods = results
                .Where(r => r.PerformanceCharacteristics
                    .VirtualCallCount > 10)
                .Select(r => new
                {
                    method = $"{r.TypeName}.{r.MethodName}",
                    virtualCallCount = r.PerformanceCharacteristics
                        .VirtualCallCount
                })
                .ToList()
        };
    }

    /// <summary>
    /// 获取实体的种类名称
    /// </summary>
    private static string GetEntityKind(ITypeDefinition typeDef)
    {
        if (typeDef.Kind == TypeKind.Interface)
        {
            return "Interface";
        }

        if (typeDef.Kind == TypeKind.Enum)
        {
            return "Enum";
        }

        if (typeDef.Kind == TypeKind.Struct)
        {
            return "Struct";
        }

        if (typeDef.Kind == TypeKind.Delegate)
        {
            return "Delegate";
        }

        return "Class";
    }

    #endregion
}
