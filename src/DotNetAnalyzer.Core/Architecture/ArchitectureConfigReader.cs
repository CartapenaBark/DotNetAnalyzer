using DotNetAnalyzer.Core.Architecture.Models;
using DotNetAnalyzer.Core.Json;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Architecture;

/// <summary>
/// 架构规则配置读取器
/// </summary>
/// <remarks>
/// 从项目目录中读取 <c>dotnet-analyzer.rules.json</c> 配置文件并解析为规则配置。
/// 如果文件不存在则返回空列表；如果 JSON 格式无效则抛出描述性异常。
/// </remarks>
public class ArchitectureConfigReader
{
    private static readonly Action<ILogger, string, Exception?> s_logConfigLoaded =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(1, nameof(ArchitectureConfigReader)),
            "已加载架构规则配置: {FilePath}");

    private static readonly Action<ILogger, Exception?> s_logConfigNotFound =
        LoggerMessage.Define(LogLevel.Debug,
            new EventId(2, nameof(ArchitectureConfigReader)),
            "未找到架构规则配置文件，使用空规则列表");

    private readonly ILogger<ArchitectureConfigReader> _logger;

    /// <summary>
    /// 架构规则配置文件名
    /// </summary>
    public const string ConfigFileName = "dotnet-analyzer.rules.json";

    public ArchitectureConfigReader(ILogger<ArchitectureConfigReader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 从指定路径读取架构规则配置
    /// </summary>
    /// <param name="rulesFilePath">规则配置文件的完整路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>解析后的规则配置列表</returns>
    /// <exception cref="FileNotFoundException">配置文件不存在</exception>
    /// <exception cref="InvalidDataException">配置文件包含无效 JSON</exception>
    public async Task<List<RuleConfig>> ReadRulesFromPathAsync(
        string rulesFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(rulesFilePath))
        {
            throw new FileNotFoundException(
                $"架构规则配置文件不存在: {rulesFilePath}",
                rulesFilePath);
        }

        var json = await File.ReadAllTextAsync(
            rulesFilePath, cancellationToken);

        var config = System.Text.Json.JsonSerializer
            .Deserialize<ArchitectureRuleConfig>(
                json, JsonOptions.Default)
            ?? new ArchitectureRuleConfig { Rules = [] };

        s_logConfigLoaded(_logger, rulesFilePath, null);

        return config.Rules;
    }

    /// <summary>
    /// 从项目目录读取架构规则配置
    /// </summary>
    /// <param name="project">Roslyn 项目对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>解析后的规则配置列表；文件不存在时返回空列表</returns>
    /// <exception cref="InvalidDataException">
    /// 配置文件存在但包含无效 JSON
    /// </exception>
    public async Task<List<RuleConfig>> ReadRulesAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        // 从项目文件路径推导项目目录
        var projectDirectory = Path.GetDirectoryName(project.FilePath);
        if (string.IsNullOrEmpty(projectDirectory))
        {
            s_logConfigNotFound(_logger, null);
            return [];
        }

        var configPath = Path.Combine(projectDirectory, ConfigFileName);

        if (!File.Exists(configPath))
        {
            s_logConfigNotFound(_logger, null);
            return [];
        }

        var json = await File.ReadAllTextAsync(configPath, cancellationToken);

        ArchitectureRuleConfig config;
        try
        {
            config = System.Text.Json.JsonSerializer.Deserialize<ArchitectureRuleConfig>(
                json,
                JsonOptions.Default)
                ?? new ArchitectureRuleConfig { Rules = [] };
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidDataException(
                $"架构规则配置文件 '{configPath}' 包含无效 JSON: {ex.Message}", ex);
        }

        s_logConfigLoaded(_logger, configPath, null);

        return config.Rules;
    }
}
