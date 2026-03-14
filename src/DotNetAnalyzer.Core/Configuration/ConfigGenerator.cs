using System.Text.Json;

namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// 配置文件生成器
/// </summary>
public class ConfigGenerator
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    /// <summary>
    /// 生成所有配置文件
    /// </summary>
    public static async Task<ConfigGenerationResult> GenerateConfigsAsync(
        InitOptions options,
        EnvironmentInfo env)
    {
        var mcpConfig = await GenerateMcpConfigAsync(options, env);
        var settingsConfig = await GenerateClaudeSettingsAsync(options, env);

        return new ConfigGenerationResult
        {
            McpConfigJson = JsonSerializer.Serialize(mcpConfig, s_jsonOptions),
            SettingsJson = JsonSerializer.Serialize(settingsConfig, s_jsonOptions)
        };
    }

    /// <summary>
    /// 生成 MCP 配置
    /// </summary>
    private static async Task<McpConfig> GenerateMcpConfigAsync(
        InitOptions options,
        EnvironmentInfo env)
    {
        return new McpConfig
        {
            McpServers = new Dictionary<string, McpServer>
            {
                ["dotnet-analyzer"] = new McpServer
                {
                    Command = env.DotnetAnalyzerPath,
                    Args = new[] { "mcp", "serve" },
                    Env = new Dictionary<string, string>
                    {
                        ["DOTNET_ENVIRONMENT"] = "Production",
                        ["DOTNET_ANALYZER_LOG_LEVEL"] = "Information"
                    }
                }
            }
        };
    }

    /// <summary>
    /// 生成 Claude Settings 配置
    /// </summary>
    private static async Task<ClaudeSettings> GenerateClaudeSettingsAsync(
        InitOptions options,
        EnvironmentInfo env)
    {
        // 如果是项目级配置，添加技能引用
        if (options.Scope == "project")
        {
            return new ClaudeSettings
            {
                EnabledMcpjsonServers = new[] { "dotnet-analyzer" },
                Permissions = new Permissions
                {
                    Allow = new[]
                    {
                        "Bash(dotnet *)",
                        "mcp__dotnet-analyzer__*"
                    }
                },
                Skills = new Dictionary<string, string>
                {
                    ["dotnet-analyze"] = ".claude/skills/dotnet-analyze/SKILL.md",
                    ["dotnet-refactor"] = ".claude/skills/dotnet-refactor/SKILL.md",
                    ["dotnet-diagnose"] = ".claude/skills/dotnet-diagnose/SKILL.md"
                }
            };
        }

        // 用户级配置，不包含技能引用
        return new ClaudeSettings
        {
            EnabledMcpjsonServers = new[] { "dotnet-analyzer" },
            Permissions = new Permissions
            {
                Allow = new[]
                {
                    "Bash(dotnet *)",
                    "mcp__dotnet-analyzer__*"
                }
            }
        };
    }
}

/// <summary>
/// MCP 配置
/// </summary>
public record McpConfig
{
    /// <summary>
    /// MCP 服务器字典
    /// </summary>
    public Dictionary<string, McpServer> McpServers { get; init; } = new();
}

/// <summary>
/// MCP 服务器配置
/// </summary>
public record McpServer
{
    /// <summary>
    /// 命令
    /// </summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>
    /// 命令参数
    /// </summary>
    public string[] Args { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 环境变量
    /// </summary>
    public Dictionary<string, string> Env { get; init; } = new();
}

/// <summary>
/// Claude Settings 配置
/// </summary>
public record ClaudeSettings
{
    /// <summary>
    /// 启用的 MCP JSON 服务器
    /// </summary>
    public string[] EnabledMcpjsonServers { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 权限设置
    /// </summary>
    public Permissions Permissions { get; init; } = new();

    /// <summary>
    /// 技能配置
    /// </summary>
    public Dictionary<string, string> Skills { get; init; } = new();
}

/// <summary>
/// 权限配置
/// </summary>
public record Permissions
{
    /// <summary>
    /// 允许的权限列表
    /// </summary>
    public string[] Allow { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 配置生成结果
/// </summary>
public record ConfigGenerationResult
{
    /// <summary>
    /// MCP 配置 JSON
    /// </summary>
    public string McpConfigJson { get; init; } = string.Empty;

    /// <summary>
    /// Settings 配置 JSON
    /// </summary>
    public string SettingsJson { get; init; } = string.Empty;
}
