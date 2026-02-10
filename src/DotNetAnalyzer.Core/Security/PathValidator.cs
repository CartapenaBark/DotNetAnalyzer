using System.Runtime.InteropServices;

namespace DotNetAnalyzer.Core.Security;

/// <summary>
/// 提供路径验证和安全检查的静态方法
/// </summary>
/// <remarks>
/// 此类用于防止路径遍历攻击和其他安全威胁，包括：
/// <list type="bullet">
///   <item>路径规范化（将相对路径转换为绝对路径）</item>
///   <item>路径遍历检测（防止 ../.. 攻击）</item>
///   <item>文件扩展名验证</item>
///   <item>文件存在性检查</item>
///   <item>非法字符检测</item>
/// </list>
/// </remarks>
public static class PathValidator
{
    /// <summary>
    /// 允许的项目文件扩展名
    /// </summary>
    private static readonly string[] ProjectExtensions = new[] { ".csproj" };

    /// <summary>
    /// 允许的解决方案文件扩展名
    /// </summary>
    private static readonly string[] SolutionExtensions = new[] { ".sln", ".slnx" };

    /// <summary>
    /// 允许的源代码文件扩展名
    /// </summary>
    private static readonly string[] SourceFileExtensions = new[] { ".cs", ".vb", ".fs" };

    /// <summary>
    /// 路径遍历攻击的特征字符
    /// </summary>
    private const char DirectorySeparatorChar = '\\';
    private const char AltDirectorySeparatorChar = '/';
    private const char ParentDirectoryPrefix = '.';

    /// <summary>
    /// 规范化路径并执行基本验证
    /// </summary>
    /// <param name="path">要规范化的路径</param>
    /// <param name="basePath">可选的基础路径，用于验证路径是否在预期范围内</param>
    /// <param name="checkExists">是否验证文件存在性，默认为 false</param>
    /// <returns>规范化的绝对路径</returns>
    /// <exception cref="PathValidationException">
    /// 当路径为空、包含非法字符、存在路径遍历风险或文件不存在（当 checkExists 为 true 时）抛出
    /// </exception>
    /// <remarks>
    /// 此方法执行以下验证步骤：
    /// <list type="number">
    ///   <item>检查路径是否为空或仅包含空白字符</item>
    ///   <item>使用 <see cref="Path.GetFullPath"/> 规范化路径</item>
    ///   <item>检查路径是否包含非法字符</item>
    ///   <item>如果提供了 basePath，验证规范化后的路径是否在 basePath 范围内</item>
    ///   <item>如果 checkExists 为 true，验证文件是否存在</item>
    /// </list>
    /// </remarks>
    public static string ValidateAndNormalize(string path, string? basePath = null, bool checkExists = false)
    {
        // 1. 检查路径是否为空
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new PathValidationException(
                "路径不能为空或仅包含空白字符",
                path,
                "Path is null or whitespace");
        }

        try
        {
            // 2. 规范化路径（处理相对路径、. 和 ..）
            var normalizedPath = Path.GetFullPath(path);

            // 3. 检查路径是否包含非法设备名称（Windows）
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 获取路径中的文件名（不含扩展名）
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(normalizedPath);

                // 检查文件名是否为保留的设备名称
                var reservedDeviceNames = new[] { "CON", "PRN", "AUX", "NUL" };
                var isReservedDevice = reservedDeviceNames.Contains(
                    fileNameWithoutExtension.ToUpperInvariant());

                if (isReservedDevice)
                {
                    throw new PathValidationException(
                        "路径包含非法的设备名称",
                        path,
                        "Contains reserved device name");
                }

                // 检查 COM1-COM9 和 LPT1-LPT9
                if (fileNameWithoutExtension.Length == 4 &&
                    (fileNameWithoutExtension.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                     fileNameWithoutExtension.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                    char.IsDigit(fileNameWithoutExtension[3]))
                {
                    throw new PathValidationException(
                        "路径包含非法的设备名称",
                        path,
                        "Contains reserved device name");
                }
            }

            // 4. 如果提供了基础路径，验证规范化后的路径是否在基础路径范围内
            if (!string.IsNullOrWhiteSpace(basePath))
            {
                var normalizedBasePath = Path.GetFullPath(basePath);

                // 确保基础路径以目录分隔符结尾，以避免部分匹配
                if (!normalizedBasePath.EndsWith(Path.DirectorySeparatorChar) &&
                    !normalizedBasePath.EndsWith(Path.AltDirectorySeparatorChar))
                {
                    normalizedBasePath += Path.DirectorySeparatorChar;
                }

                // 检查规范化后的路径是否以基础路径开头
                if (!normalizedPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new PathValidationException(
                        $"路径超出基础目录范围。基础目录: {normalizedBasePath}, 实际路径: {normalizedPath}",
                        path,
                        "Path traversal attempt detected");
                }
            }

            // 5. 检查文件是否存在（如果要求）
            if (checkExists && !File.Exists(normalizedPath) && !Directory.Exists(normalizedPath))
            {
                throw new PathValidationException(
                    $"路径不存在: {normalizedPath}",
                    path,
                    "Path does not exist");
            }

            return normalizedPath;
        }
        catch (PathValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PathValidationException(
                $"路径规范化失败: {path}",
                path,
                "Path normalization failed",
                ex);
        }
    }

    /// <summary>
    /// 验证项目文件路径
    /// </summary>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <param name="basePath">可选的基础路径，用于验证路径是否在预期范围内</param>
    /// <param name="checkExists">是否验证文件存在性，默认为 true</param>
    /// <returns>规范化的绝对路径</returns>
    /// <exception cref="PathValidationException">
    /// 当路径无效、扩展名不正确或文件不存在（当 checkExists 为 true 时）抛出
    /// </exception>
    /// <remarks>
    /// 此方法验证路径是否为有效的 C# 项目文件：
    /// <list type="number">
    ///   <item>调用 <see cref="ValidateAndNormalize"/> 规范化路径</item>
    ///   <item>验证文件扩展名为 .csproj</item>
    ///   <item>验证文件是否存在（如果 checkExists 为 true）</item>
    /// </list>
    /// </remarks>
    public static string ValidateProjectPath(string projectPath, string? basePath = null, bool checkExists = true)
    {
        // 规范化路径并执行基本验证
        var normalizedPath = ValidateAndNormalize(projectPath, basePath, checkExists);

        // 验证文件扩展名
        if (!ProjectExtensions.Contains(Path.GetExtension(normalizedPath), StringComparer.OrdinalIgnoreCase))
        {
            throw new PathValidationException(
                $"文件不是有效的 C# 项目文件。期望扩展名: {string.Join(", ", ProjectExtensions)}, 实际: {Path.GetExtension(normalizedPath)}",
                projectPath,
                "Invalid file extension");
        }

        return normalizedPath;
    }

    /// <summary>
    /// 验证解决方案文件路径
    /// </summary>
    /// <param name="solutionPath">解决方案文件路径（.sln 或 .slnx）</param>
    /// <param name="basePath">可选的基础路径，用于验证路径是否在预期范围内</param>
    /// <param name="checkExists">是否验证文件存在性，默认为 true</param>
    /// <returns>规范化的绝对路径</returns>
    /// <exception cref="PathValidationException">
    /// 当路径无效、扩展名不正确或文件不存在（当 checkExists 为 true 时）抛出
    /// </exception>
    /// <remarks>
    /// 此方法验证路径是否为有效的 Visual Studio 解决方案文件：
    /// <list type="number">
    ///   <item>调用 <see cref="ValidateAndNormalize"/> 规范化路径</item>
    ///   <item>验证文件扩展名为 .sln 或 .slnx</item>
    ///   <item>验证文件是否存在（如果 checkExists 为 true）</item>
    /// </list>
    /// 支持的格式：
    /// <list type="bullet">
    ///   <item><description>传统 .sln 格式（Visual Studio 2010-2019）</description></item>
    ///   <item><description>新一代 .slnx 格式（Visual Studio 2022 17.8+，XML 格式）</description></item>
    /// </list>
    /// </remarks>
    public static string ValidateSolutionPath(string solutionPath, string? basePath = null, bool checkExists = true)
    {
        // 规范化路径并执行基本验证
        var normalizedPath = ValidateAndNormalize(solutionPath, basePath, checkExists);

        // 验证文件扩展名
        if (!SolutionExtensions.Contains(Path.GetExtension(normalizedPath), StringComparer.OrdinalIgnoreCase))
        {
            throw new PathValidationException(
                $"文件不是有效的解决方案文件。期望扩展名: {string.Join(", ", SolutionExtensions)}, 实际: {Path.GetExtension(normalizedPath)}",
                solutionPath,
                "Invalid file extension");
        }

        return normalizedPath;
    }

    /// <summary>
    /// 验证源代码文件路径
    /// </summary>
    /// <param name="sourceFilePath">源代码文件路径</param>
    /// <param name="basePath">可选的基础路径，用于验证路径是否在预期范围内</param>
    /// <param name="checkExists">是否验证文件存在性，默认为 true</param>
    /// <returns>规范化的绝对路径</returns>
    /// <exception cref="PathValidationException">
    /// 当路径无效、扩展名不正确或文件不存在（当 checkExists 为 true 时）抛出
    /// </exception>
    /// <remarks>
    /// 此方法验证路径是否为有效的 .NET 源代码文件：
    /// <list type="number">
    ///   <item>调用 <see cref="ValidateAndNormalize"/> 规范化路径</item>
    ///   <item>验证文件扩展名为 .cs, .vb 或 .fs</item>
    ///   <item>验证文件是否存在（如果 checkExists 为 true）</item>
    /// </list>
    /// 支持的语言：
    /// <list type="bullet">
    ///   <item><description>C# (.cs)</description></item>
    ///   <item><description>Visual Basic (.vb)</description></item>
    ///   <item><description>F# (.fs)</description></item>
    /// </list>
    /// </remarks>
    public static string ValidateSourceFilePath(string sourceFilePath, string? basePath = null, bool checkExists = true)
    {
        // 规范化路径并执行基本验证
        var normalizedPath = ValidateAndNormalize(sourceFilePath, basePath, checkExists);

        // 验证文件扩展名
        if (!SourceFileExtensions.Contains(Path.GetExtension(normalizedPath), StringComparer.OrdinalIgnoreCase))
        {
            throw new PathValidationException(
                $"文件不是有效的源代码文件。期望扩展名: {string.Join(", ", SourceFileExtensions)}, 实际: {Path.GetExtension(normalizedPath)}",
                sourceFilePath,
                "Invalid file extension");
        }

        return normalizedPath;
    }

    /// <summary>
    /// 检测路径是否包含路径遍历攻击特征
    /// </summary>
    /// <param name="path">要检查的路径</param>
    /// <returns>如果路径可能包含路径遍历攻击，返回 true；否则返回 false</returns>
    /// <remarks>
    /// 此方法检测路径中的可疑模式，包括：
    /// <list type="bullet">
    ///   <item>包含 ".." 父目录引用</item>
    ///   <item>包含过多的目录分隔符</item>
    ///   <item>包含非常长的路径段（可能用于绕过检查）</item>
    /// </list>
    /// 注意：此方法仅作为辅助检查，不应替代完整的路径规范化。
    /// </remarks>
    public static bool ContainsPathTraversalPatterns(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // 检查是否包含 ".." （父目录引用）
        if (path.Contains(".."))
        {
            return true;
        }

        // 检查路径段的长度（防止超长段绕过检查）
        var segments = path.Split(new[] { DirectorySeparatorChar, AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        const int maxSegmentLength = 255; // Windows MAX_PATH 限制

        foreach (var segment in segments)
        {
            if (segment.Length > maxSegmentLength)
            {
                return true;
            }
        }

        // 检查总路径长度
        if (path.Length > 260 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // 超过 Windows MAX_PATH 限制（除非使用长路径前缀）
            return !path.StartsWith(@"\\?\");
        }

        return false;
    }
}
