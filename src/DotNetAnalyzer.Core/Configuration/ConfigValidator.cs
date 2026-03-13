using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// 配置验证器
/// </summary>
public class ConfigValidator
{
    /// <summary>
    /// 验证配置
    /// </summary>
    public static async Task<ValidationResult> ValidateAsync(ConfigGenerationResult result)
    {
        var checks = new List<ValidationCheck>();

        // 1. 检查 dotnet-analyzer 可执行
        checks.Add(await CheckDotnetAnalyzerAsync());

        // 2. 检查 .mcp.json 格式
        checks.Add(await CheckMcpJsonFormatAsync(result.McpConfigJson));

        // 3. 检查 MCP 服务器配置
        checks.Add(await CheckMcpServerConfigAsync(result.McpConfigJson));

        // 4. 检查 settings.json 格式
        checks.Add(await CheckSettingsJsonFormatAsync(result.SettingsJson));

        return new ValidationResult(checks);
    }

    /// <summary>
    /// 检查 dotnet-analyzer 可执行
    /// </summary>
    private static async Task<ValidationCheck> CheckDotnetAnalyzerAsync()
    {
        try
        {
            var result = await RunCommandAsync("dotnet-analyzer", "--version");
            var passed = !string.IsNullOrEmpty(result) && !result.Contains("error");

            return new ValidationCheck
            {
                Name = "dotnet-analyzer 可执行",
                Passed = passed,
                Error = passed ? null : "dotnet-analyzer 命令失败"
            };
        }
        catch (Exception ex)
        {
            return new ValidationCheck
            {
                Name = "dotnet-analyzer 可执行",
                Passed = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// 检查 .mcp.json 格式
    /// </summary>
    private static async Task<ValidationCheck> CheckMcpJsonFormatAsync(string mcpJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(mcpJson);
            var hasMcpServers = doc.RootElement.TryGetProperty("mcpServers", out var servers);
            var passed = hasMcpServers && servers.ValueKind != JsonValueKind.Undefined;

            return new ValidationCheck
            {
                Name = ".mcp.json 格式",
                Passed = passed,
                Error = passed ? null : "缺少 mcpServers 配置"
            };
        }
        catch (JsonException ex)
        {
            return new ValidationCheck
            {
                Name = ".mcp.json 格式",
                Passed = false,
                Error = $"JSON 格式错误: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 检查 MCP 服务器配置
    /// </summary>
    private static async Task<ValidationCheck> CheckMcpServerConfigAsync(string mcpJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(mcpJson);
            var hasServers = doc.RootElement.TryGetProperty("mcpServers", out var servers);

            if (!hasServers || servers.ValueKind != JsonValueKind.Object)
            {
                return new ValidationCheck
                {
                    Name = "MCP 服务器配置",
                    Passed = false,
                    Error = "mcpServers 不是对象"
                };
            }

            var hasDotnetAnalyzer = servers.EnumerateObject().Any(p => p.Name == "dotnet-analyzer");
            if (!hasDotnetAnalyzer)
            {
                return new ValidationCheck
                {
                    Name = "MCP 服务器配置",
                    Passed = false,
                    Error = "缺少 dotnet-analyzer 服务器配置"
                };
            }

            return new ValidationCheck
            {
                Name = "MCP 服务器配置",
                Passed = true
            };
        }
        catch (Exception ex)
        {
            return new ValidationCheck
            {
                Name = "MCP 服务器配置",
                Passed = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// 检查 settings.json 格式
    /// </summary>
    private static async Task<ValidationCheck> CheckSettingsJsonFormatAsync(string settingsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            var hasEnabledServers = doc.RootElement.TryGetProperty("enabledMcpjsonServers", out var servers);

            return new ValidationCheck
            {
                Name = "settings.json 格式",
                Passed = true // 格式正确即可
            };
        }
        catch (JsonException ex)
        {
            return new ValidationCheck
            {
                Name = "settings.json 格式",
                Passed = false,
                Error = $"JSON 格式错误: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 运行命令
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
/// 验证结果
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// 所有检查项
    /// </summary>
    public List<ValidationCheck> Checks { get; init; } = new();

    /// <summary>
    /// 是否全部通过
    /// </summary>
    public bool IsValid => Checks.All(c => c.Passed);

    /// <summary>
    /// 构造函数
    /// </summary>
    public ValidationResult()
    {
    }

    /// <summary>
    /// 构造函数（带检查项）
    /// </summary>
    public ValidationResult(List<ValidationCheck> checks)
    {
        Checks = checks;
    }
}

/// <summary>
/// 验证检查项
/// </summary>
public record ValidationCheck
{
    /// <summary>
    /// 检查项名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 是否通过
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// 错误信息（如果未通过）
    /// </summary>
    public string? Error { get; init; }
}
