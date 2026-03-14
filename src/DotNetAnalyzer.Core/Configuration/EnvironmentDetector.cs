using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// 环境信息检测器
/// </summary>
public class EnvironmentDetector
{
    /// <summary>
    /// 检测当前环境信息
    /// </summary>
    public async Task<EnvironmentInfo> DetectAsync()
    {
        var info = new EnvironmentInfo();

        // 1. 检测 dotnet-analyzer 路径
        info.DotnetAnalyzerPath = await EnvironmentDetector.FindDotnetAnalyzerAsync();

        // 2. 检测 .NET SDK 版本
        info.DotnetSdkVersion = await EnvironmentDetector.GetDotnetSdkVersionAsync();

        // 3. 检测操作系统
        info.OperatingSystem = GetOperatingSystem();

        // 4. 检测 Shell 类型
        info.ShellType = GetShellType();

        // 5. 检测项目文件
        info.ProjectFiles = await FindProjectFilesAsync();

        // 6. 检测现有配置
        info.ExistingConfig = await DetectExistingConfigAsync();

        return info;
    }

    /// <summary>
    /// 查找 dotnet-analyzer 可执行文件路径
    /// </summary>
    private static async Task<string> FindDotnetAnalyzerAsync()
    {
        // 尝试使用 which/where 命令
        var result = await RunCommandAsync(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
            "dotnet-analyzer");

        if (!string.IsNullOrEmpty(result) && !result.Contains("not found") && !result.Contains("INFO:"))
        {
            return result.Trim().Split('\n')[0].Trim();
        }

        // 尝试常见路径
        var commonPaths = GetCommonDotnetToolPaths();
        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // 未找到时返回默认路径（用于测试和 dry-run 模式）
        return "dotnet-analyzer";
    }

    /// <summary>
    /// 获取常见的 .NET 工具路径
    /// </summary>
    private static string[] GetCommonDotnetToolPaths()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            paths.Add(Path.Combine(userProfile, ".dotnet", "tools", "dotnet-analyzer.exe"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            paths.Add(Path.Combine(userProfile, ".dotnet", "tools", "dotnet-analyzer"));
            paths.Add(Path.Combine(userProfile, ".dotnet", "tools", "dotnet-analyzer"));
        }
        else // Linux
        {
            paths.Add(Path.Combine(userProfile, ".dotnet", "tools", "dotnet-analyzer"));
            paths.Add("/usr/local/bin/dotnet-analyzer");
        }

        return paths.ToArray();
    }

    /// <summary>
    /// 获取 .NET SDK 版本
    /// </summary>
    private static async Task<string> GetDotnetSdkVersionAsync()
    {
        try
        {
            var result = await RunCommandAsync("dotnet", "--version");
            if (!string.IsNullOrEmpty(result))
            {
                var version = result.Trim().Split(' ')[0];
                return version;
            }
        }
        catch
        {
            // 忽略错误，返回默认值
        }

        return "未知";
    }

    /// <summary>
    /// 获取操作系统信息
    /// </summary>
    private static string GetOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";

        return RuntimeInformation.OSDescription;
    }

    /// <summary>
    /// 获取 Shell 类型
    /// </summary>
    private static string GetShellType()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";

        var shell = Environment.GetEnvironmentVariable("SHELL");
        return !string.IsNullOrEmpty(shell) ? Path.GetFileName(shell) : "sh";
    }

    /// <summary>
    /// 查找项目文件
    /// </summary>
    private static async Task<string[]> FindProjectFilesAsync()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var projectFiles = new List<string>();

        // 查找 .sln 文件
        var slnFiles = Directory.GetFiles(currentDir, "*.sln");
        if (slnFiles.Length > 0)
        {
            projectFiles.AddRange(slnFiles);
        }

        // 查找 .slnx 文件（Visual Studio 2022+）
        var slnxFiles = Directory.GetFiles(currentDir, "*.slnx");
        if (slnxFiles.Length > 0)
        {
            projectFiles.AddRange(slnxFiles);
        }

        // 如果没有解决方案文件，查找 .csproj 文件
        if (projectFiles.Count == 0)
        {
            var csprojFiles = Directory.GetFiles(currentDir, "*.csproj");
            if (csprojFiles.Length > 0)
            {
                projectFiles.Add(csprojFiles[0]); // 只取第一个
            }
        }

        return projectFiles.ToArray();
    }

    /// <summary>
    /// 检测现有配置
    /// </summary>
    private static async Task<ExistingConfigInfo> DetectExistingConfigAsync()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var info = new ExistingConfigInfo();

        // 检查 .mcp.json
        info.HasMcpJson = File.Exists(Path.Combine(currentDir, ".mcp.json"));

        // 检查 .claude/settings.json
        info.HasClaudeSettings = File.Exists(Path.Combine(currentDir, ".claude", "settings.json"));

        // 检查用户级配置
        var userSettings = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "settings.json");
        info.HasUserSettings = File.Exists(userSettings);

        return info;
    }

    /// <summary>
    /// 运行命令并获取输出
    /// </summary>
    private static async Task<string> RunCommandAsync(string command, string arguments)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
                return string.Empty;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output;
        }
        catch
        {
            return string.Empty;
        }
    }
}

/// <summary>
/// 环境信息
/// </summary>
public record EnvironmentInfo
{
    /// <summary>
    /// dotnet-analyzer 可执行文件路径
    /// </summary>
    public string DotnetAnalyzerPath { get; set; } = string.Empty;

    /// <summary>
    /// .NET SDK 版本
    /// </summary>
    public string DotnetSdkVersion { get; set; } = string.Empty;

    /// <summary>
    /// 操作系统
    /// </summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>
    /// Shell 类型
    /// </summary>
    public string ShellType { get; set; } = string.Empty;

    /// <summary>
    /// 项目文件列表
    /// </summary>
    public string[] ProjectFiles { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 现有配置信息
    /// </summary>
    public ExistingConfigInfo ExistingConfig { get; set; } = new();
}

/// <summary>
/// 现有配置信息
/// </summary>
public record ExistingConfigInfo
{
    /// <summary>
    /// 是否存在 .mcp.json
    /// </summary>
    public bool HasMcpJson { get; set; }

    /// <summary>
    /// 是否存在 .claude/settings.json
    /// </summary>
    public bool HasClaudeSettings { get; set; }

    /// <summary>
    /// 是否存在用户级 settings.json
    /// </summary>
    public bool HasUserSettings { get; set; }
}
