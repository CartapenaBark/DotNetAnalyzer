using System.Runtime.InteropServices;
using DotNetAnalyzer.Core.Decompilation.Models;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Decompilation;

/// <summary>
/// 程序集元数据读取器，提取程序集引用、目标框架和兼容性信息
/// </summary>
/// <remarks>
/// 此读取器使用 PEFile 的 MetadataReader 提取程序集级别的元数据信息：
/// <list type="bullet">
///   <item>程序集名称、版本和强命名信息</item>
///   <item>目标框架标识和版本</item>
///   <item>程序集引用列表（名称、版本、PublicKeyToken）</item>
///   <item>兼容性检查结果</item>
/// </list>
/// </remarks>
public class AssemblyMetadataReader
{
    private static readonly Action<ILogger, string, Exception?> s_logReading =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(Read)),
            "开始读取程序集元数据: {Path}");

    private static readonly Action<ILogger, string, int, int, Exception?>
        s_logRead =
            LoggerMessage.Define<string, int, int>(
                LogLevel.Information,
                new EventId(2, nameof(Read)),
            "程序集元数据读取完成: {Path}, 引用数: {RefCount}, 类型数: {TypeCount}");

    private static readonly Action<ILogger, string, Exception?> s_logError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3, nameof(Read)),
            "读取程序集元数据时发生错误: {Error}");

    private readonly AssemblyCache _assemblyCache;
    private readonly ILogger<AssemblyMetadataReader> _logger;

    /// <summary>
    /// 初始化 AssemblyMetadataReader 的新实例
    /// </summary>
    /// <param name="assemblyCache">程序集缓存</param>
    /// <param name="logger">日志记录器</param>
    public AssemblyMetadataReader(
        AssemblyCache assemblyCache,
        ILogger<AssemblyMetadataReader> logger)
    {
        _assemblyCache = assemblyCache
            ?? throw new ArgumentNullException(nameof(assemblyCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 读取指定程序集的元数据信息
    /// </summary>
    /// <param name="assemblyPath">程序集文件路径</param>
    /// <returns>程序集元数据</returns>
    public async Task<AssemblyMetadata> Read(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            return new AssemblyMetadata
            {
                AssemblyPath = assemblyPath,
                Success = false,
                Error = $"程序集文件不存在: {assemblyPath}"
            };
        }

        s_logReading(_logger, assemblyPath, null);

        try
        {
            var peFile = await _assemblyCache
                .GetOrAddAsync(assemblyPath)
                .ConfigureAwait(false);

            var metadata = peFile.Metadata;
            var result = new AssemblyMetadata
            {
                AssemblyPath = assemblyPath,
                Success = true,
                TypeCount = metadata.TypeDefinitions.Count
            };

            // 读取程序集名称和版本
            ReadAssemblyIdentity(metadata, result);

            // 读取目标框架
            ReadTargetFramework(peFile, metadata, result);

            // 读取程序集引用
            ReadAssemblyReferences(metadata, result);

            // 检查兼容性
            CheckCompatibility(result);

            s_logRead(
                _logger, assemblyPath,
                result.References.Count, result.TypeCount, null);

            return result;
        }
        catch (Exception ex)
        {
            s_logError(_logger, ex.Message, null);

            return new AssemblyMetadata
            {
                AssemblyPath = assemblyPath,
                Success = false,
                Error = $"读取元数据失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 读取程序集标识信息（名称、版本）
    /// </summary>
    private static void ReadAssemblyIdentity(
        System.Reflection.Metadata.MetadataReader metadata,
        AssemblyMetadata result)
    {
        var assemblyDefinition = metadata.GetAssemblyDefinition();

        result.AssemblyName = metadata
            .GetString(assemblyDefinition.Name);
        result.Version = assemblyDefinition.Version?.ToString();

        // 检查是否为强命名程序集（PublicKey 不为空）
        var publicKey = assemblyDefinition.PublicKey;
        if (!publicKey.IsNil)
        {
            var pkBytes = metadata.GetBlobBytes(publicKey);
            if (pkBytes.Length > 0)
            {
                // 强命名程序集：从公钥计算 token
                var tokenBytes = ComputePublicKeyToken(pkBytes);

                var tokenHex = Convert.ToHexString(tokenBytes)
                    .ToLowerInvariant();
                result.Version =
                    $"{result.Version} (公钥令牌: {tokenHex})";
            }
        }
    }

    /// <summary>
    /// 从公钥计算公钥令牌
    /// </summary>
    private static byte[] ComputePublicKeyToken(byte[] publicKey)
    {
#pragma warning disable CA5350 // SHA1 是 .NET 程序集公钥令牌的标准算法
        var hash = System.Security.Cryptography.SHA1.HashData(publicKey);
#pragma warning restore CA5350
        var token = new byte[8];
        Array.Copy(hash, hash.Length - 8, token, 0, 8);
        return token;
    }

    /// <summary>
    /// 读取目标框架信息
    /// </summary>
    private static void ReadTargetFramework(
        PEFile peFile,
        System.Reflection.Metadata.MetadataReader metadata,
        AssemblyMetadata result)
    {
        // 尝试从程序集特性中读取 TargetFrameworkAttribute
        var tf = ReadTargetFrameworkFromAttributes(metadata);

        if (tf != null)
        {
            result.TargetFramework = tf;
            result.TargetFrameworkVersion =
                ExtractVersionFromFramework(tf);

            if (tf.StartsWith(".NETCoreApp",
                    StringComparison.OrdinalIgnoreCase) ||
                tf.StartsWith(".NET ",
                    StringComparison.OrdinalIgnoreCase))
            {
                result.TargetFrameworkIdentifier = ".NETCoreApp";
            }
            else if (tf.StartsWith(".NETFramework",
                         StringComparison.OrdinalIgnoreCase))
            {
                result.TargetFrameworkIdentifier = ".NETFramework";
            }
            else
            {
                result.TargetFrameworkIdentifier = tf;
            }
        }
        else
        {
            result.TargetFramework = "无法检测";
            result.TargetFrameworkIdentifier = "Unknown";
        }
    }

    /// <summary>
    /// 从程序集特性中读取 TargetFrameworkAttribute 值
    /// </summary>
    private static string? ReadTargetFrameworkFromAttributes(
        System.Reflection.Metadata.MetadataReader metadata)
    {
        var assemblyDef = metadata.GetAssemblyDefinition();

        foreach (var attrHandle in assemblyDef.GetCustomAttributes())
        {
            if (((System.Reflection.Metadata.Handle)attrHandle).Kind
                != System.Reflection.Metadata.HandleKind
                    .CustomAttribute)
            {
                continue;
            }

            var attr = metadata.GetCustomAttribute(
                (System.Reflection.Metadata.CustomAttributeHandle)attrHandle);
            var ctorHandle = attr.Constructor;

            if (ctorHandle.Kind
                != System.Reflection.Metadata.HandleKind.MemberReference)
            {
                continue;
            }

            var memberRef = metadata.GetMemberReference(
                (System.Reflection.Metadata.MemberReferenceHandle)ctorHandle);

            if (memberRef.Parent.Kind
                != System.Reflection.Metadata.HandleKind.TypeReference)
            {
                continue;
            }

            var typeRef = metadata.GetTypeReference(
                (System.Reflection.Metadata.TypeReferenceHandle)
                    memberRef.Parent);
            var typeName = metadata.GetString(typeRef.Name);

            if (typeName == "TargetFrameworkAttribute")
            {
                try
                {
                    var blobReader = metadata
                        .GetBlobReader(attr.Value);

                    // 跳过 prolog (0x01)
                    if (blobReader.RemainingBytes > 0)
                    {
                        blobReader.ReadByte();
                    }

                    if (blobReader.RemainingBytes > 0)
                    {
                        return blobReader.ReadSerializedString();
                    }
                }
                catch
                {
                    // 忽略读取错误
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 从 TargetFrameworkAttribute 值中提取版本号
    /// </summary>
    private static string? ExtractVersionFromFramework(
        string frameworkString)
    {
        var versionIndex = frameworkString
            .IndexOf("Version=v", StringComparison.OrdinalIgnoreCase);
        if (versionIndex >= 0)
        {
            return frameworkString
                .Substring(versionIndex + "Version=v".Length);
        }

        return null;
    }

    /// <summary>
    /// 读取程序集引用列表
    /// </summary>
    private static void ReadAssemblyReferences(
        System.Reflection.Metadata.MetadataReader metadata,
        AssemblyMetadata result)
    {
        foreach (var handle in metadata.AssemblyReferences)
        {
            var reference = metadata.GetAssemblyReference(handle);
            var name = metadata.GetString(reference.Name);
            var version = reference.Version?.ToString();

            // 提取 PublicKeyToken
            string? publicKeyToken = null;
            bool isStrongNamed = false;
            var pkToken = reference.PublicKeyOrToken;

            if (!pkToken.IsNil)
            {
                var tokenBytes = metadata.GetBlobBytes(pkToken);

                if (tokenBytes.Length > 0)
                {
                    isStrongNamed = true;

                    if (tokenBytes.Length == 8)
                    {
                        // 已经是 token

                        publicKeyToken = Convert.ToHexString(tokenBytes)
                            .ToLowerInvariant();
                    }
                    else
                    {
                        // 完整公钥，需要计算 token
                        var computedToken =
                            ComputePublicKeyToken(tokenBytes);

                        publicKeyToken = Convert.ToHexString(computedToken)
                            .ToLowerInvariant();
                    }
                }
            }

            result.References.Add(new AssemblyReferenceInfo
            {
                Name = name,
                Version = version,
                PublicKeyToken = publicKeyToken,
                IsStrongNamed = isStrongNamed,
                IsResolved = true
            });
        }
    }

    /// <summary>
    /// 检查兼容性问题
    /// </summary>
    private static void CheckCompatibility(AssemblyMetadata metadata)
    {
        if (metadata.TargetFrameworkIdentifier == ".NETFramework")
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                metadata.CompatibilityIssues.Add(
                    ".NET Framework 程序集在非 Windows 平台可能不兼容");
            }
        }

        if (metadata.TargetFrameworkVersion != null)
        {
            if (Version.TryParse(
                    metadata.TargetFrameworkVersion, out var version))
            {
                if (version.Major < 5 &&
                    metadata.TargetFrameworkIdentifier
                        == ".NETCoreApp")
                {
                    metadata.CompatibilityIssues.Add(
                        $"目标框架版本过旧: .NET Core {version} " +
                        "已停止支持，建议升级到 .NET 8.0+");
                }
            }
        }
    }
}
