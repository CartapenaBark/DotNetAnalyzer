using DotNetAnalyzer.Core.Architecture.Models;
using DotNetAnalyzer.Core.Architecture.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Architecture;

/// <summary>
/// 架构规则检查引擎，负责加载配置、执行规则并汇总报告
/// </summary>
public class ArchitectureRuleEngine
{
    private static readonly Action<ILogger, int, Exception?> s_logRulesLoaded =
        LoggerMessage.Define<int>(LogLevel.Information,
            new EventId(1, nameof(ArchitectureRuleEngine)),
            "已加载 {RuleCount} 条架构规则");

    private static readonly Action<ILogger, int, Exception?> s_logCheckCompleted =
        LoggerMessage.Define<int>(LogLevel.Information,
            new EventId(2, nameof(ArchitectureRuleEngine)),
            "架构规则检查完成，发现 {ViolationCount} 个违规");

    private static readonly Action<ILogger, string, Exception?> s_logRuleEvaluated =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(3, nameof(ArchitectureRuleEngine)),
            "已评估规则: {RuleName}");

    private readonly ILogger<ArchitectureRuleEngine> _logger;
    private readonly ArchitectureConfigReader _configReader;

    public ArchitectureRuleEngine(
        ILogger<ArchitectureRuleEngine> logger,
        ArchitectureConfigReader configReader)
    {
        _logger = logger;
        _configReader = configReader;
    }

    /// <summary>
    /// 对指定项目执行架构规则检查并生成报告
    /// </summary>
    /// <param name="project">待检查的 Roslyn 项目</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>架构规则检查报告</returns>
    public async Task<ArchitectureReport> CheckAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        var ruleConfigs = await _configReader.ReadRulesAsync(
            project, cancellationToken);

        var rules = CreateRules(ruleConfigs);

        s_logRulesLoaded(_logger, rules.Count, null);

        var allViolations = new List<ArchitectureViolation>();

        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var violations = await rule.EvaluateAsync(project, cancellationToken);
            allViolations.AddRange(violations);

            s_logRuleEvaluated(_logger, rule.Name, null);
        }

        var totalFilesChecked = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs") == true)
            .Count();

        // 通过率 = 无违规文件数 / 总文件数
        var violatedFiles = allViolations
            .Select(v => v.FilePath)
            .Distinct()
            .Count();
        var passRate = totalFilesChecked > 0
            ? (double)(totalFilesChecked - violatedFiles) / totalFilesChecked
            : 1.0;

        s_logCheckCompleted(_logger, allViolations.Count, null);

        return new ArchitectureReport
        {
            TotalRulesChecked = rules.Count,
            TotalViolations = allViolations.Count,
            Violations = allViolations,
            PassRate = Math.Round(passRate, 4)
        };
    }

    /// <summary>
    /// 使用自定义规则文件对指定项目执行架构规则评估并生成报告
    /// </summary>
    /// <param name="project">待评估的 Roslyn 项目</param>
    /// <param name="rulesFilePath">自定义规则文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>架构规则评估报告</returns>
    public async Task<ArchitectureReport> EvaluateAsync(
        Project project,
        string rulesFilePath,
        CancellationToken cancellationToken = default)
    {
        var ruleConfigs = await _configReader.ReadRulesFromPathAsync(
            rulesFilePath, cancellationToken);

        return await EvaluateWithRulesAsync(
            project, ruleConfigs, cancellationToken);
    }

    /// <summary>
    /// 使用指定的规则配置列表对项目执行架构规则评估
    /// </summary>
    private async Task<ArchitectureReport> EvaluateWithRulesAsync(
        Project project,
        List<RuleConfig> ruleConfigs,
        CancellationToken cancellationToken)
    {
        var rules = CreateRules(ruleConfigs);

        s_logRulesLoaded(_logger, rules.Count, null);

        var allViolations = new List<ArchitectureViolation>();

        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var violations = await rule.EvaluateAsync(
                project, cancellationToken);
            allViolations.AddRange(violations);

            s_logRuleEvaluated(_logger, rule.Name, null);
        }

        var totalFilesChecked = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs") == true)
            .Count();

        var violatedFiles = allViolations
            .Select(v => v.FilePath)
            .Distinct()
            .Count();
        var passRate = totalFilesChecked > 0
            ? (double)(totalFilesChecked - violatedFiles) / totalFilesChecked
            : 1.0;

        s_logCheckCompleted(_logger, allViolations.Count, null);

        return new ArchitectureReport
        {
            TotalRulesChecked = rules.Count,
            TotalViolations = allViolations.Count,
            Violations = allViolations,
            PassRate = Math.Round(passRate, 4)
        };
    }

    /// <summary>
    /// 根据配置创建对应的规则实例
    /// </summary>
    internal static List<IArchitectureRule> CreateRules(
        List<RuleConfig> ruleConfigs)
    {
        var rules = new List<IArchitectureRule>();

        foreach (var config in ruleConfigs)
        {
            IArchitectureRule? rule = config.Type switch
            {
                "dependency-direction" => CreateDependencyDirectionRule(config),
                "layer-hierarchy" => CreateLayerHierarchyRule(config),
                "naming-convention" => CreateNamingConventionRule(config),
                _ => null
            };

            if (rule != null)
            {
                rules.Add(rule);
            }
        }

        return rules;
    }

    private static DependencyDirectionRule? CreateDependencyDirectionRule(
        RuleConfig config)
    {
        if (string.IsNullOrEmpty(config.From) || string.IsNullOrEmpty(config.To))
        {
            return null;
        }

        return new DependencyDirectionRule(
            config.From,
            config.To,
            config.Severity);
    }

    private static LayerHierarchyRule? CreateLayerHierarchyRule(
        RuleConfig config)
    {
        if (config.Layers == null || config.Layers.Count == 0)
        {
            return null;
        }

        return new LayerHierarchyRule(
            config.Layers,
            config.AllowedDirection ?? "forward-only",
            config.Severity);
    }

    private static NamingConventionRule? CreateNamingConventionRule(
        RuleConfig config)
    {
        if (string.IsNullOrEmpty(config.Pattern) ||
            string.IsNullOrEmpty(config.Kind))
        {
            return null;
        }

        return new NamingConventionRule(
            config.Kind,
            config.Pattern,
            config.Namespace,
            config.Severity);
    }
}
