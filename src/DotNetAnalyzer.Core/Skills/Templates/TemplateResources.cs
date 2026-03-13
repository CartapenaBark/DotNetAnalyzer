using System.Globalization;
using System.Reflection;

namespace DotNetAnalyzer.Core.Skills.Templates;

/// <summary>
/// 报告模板本地化资源管理器
/// </summary>
public static class TemplateResources
{
    private static readonly Dictionary<string, Dictionary<string, string>> _resources = new()
    {
        ["zh-CN"] = new()
        {
            // 标题
            ["Report.Title"] = "📊 .NET 代码分析报告",
            ["Report.Project"] = "项目",
            ["Report.GeneratedAt"] = "生成时间",
            ["Report.Duration"] = "分析时长",
            ["Report.Version"] = "分析工具",

            // 摘要
            ["Summary.Title"] = "📋 执行摘要",
            ["Summary.Overview"] = "概览",
            ["Summary.KeyMetrics"] = "关键指标",
            ["Summary.ProjectPath"] = "项目路径",
            ["Summary.ProjectType"] = "项目类型",
            ["Summary.FileCount"] = "分析文件数",
            ["Summary.LineCount"] = "代码行数",
            ["Summary.Status"] = "分析状态",
            ["Summary.ErrorCount"] = "编译错误",
            ["Summary.WarningCount"] = "编译警告",
            ["Summary.AvgComplexity"] = "圈复杂度（平均）",
            ["Summary.MaintainabilityIndex"] = "维护性指数",
            ["Summary.TestCoverage"] = "测试覆盖率",

            // 诊断
            ["Diagnostics.Title"] = "🔍 编译诊断",
            ["Diagnostics.Errors"] = "❌ 错误",
            ["Diagnostics.Warnings"] = "⚠️ 警告",
            ["Diagnostics.NoErrors"] = "✅ 无编译错误",
            ["Diagnostics.NoWarnings"] = "✅ 无编译警告",
            ["Diagnostics.File"] = "文件",
            ["Diagnostics.Line"] = "行号",
            ["Diagnostics.Code"] = "错误代码",
            ["Diagnostics.Message"] = "消息",
            ["Diagnostics.Severity"] = "严重程度",
            ["Diagnostics.Suggestion"] = "建议",

            // 度量
            ["Metrics.Title"] = "📈 代码度量",
            ["Metrics.ComplexityAnalysis"] = "复杂度分析",
            ["Metrics.CyclomaticComplexity"] = "圈复杂度（平均）",
            ["Metrics.MaxCyclomaticComplexity"] = "圈复杂度（最大）",
            ["Metrics.CognitiveComplexity"] = "认知复杂度（平均）",
            ["Metrics.NestingDepth"] = "嵌套深度（最大）",
            ["Metrics.MaintainabilityAnalysis"] = "可维护性分析",
            ["Metrics.MaintainabilityIndex"] = "维护性指数",
            ["Metrics.CodeVolume"] = "代码体积",
            ["Metrics.FileCount"] = "文件数量",
            ["Metrics.ClassCount"] = "类数量",
            ["Metrics.DuplicationAnalysis"] = "重复代码分析",
            ["Metrics.DuplicationPercentage"] = "代码复制率",
            ["Metrics.DuplicateBlockCount"] = "复制块数量",
            ["Metrics.TotalDuplicateLines"] = "总复制行数",
            ["Metrics.Threshold"] = "阈值",
            ["Metrics.Status"] = "状态",

            // 死代码
            ["DeadCode.Title"] = "💀 死代码检测",
            ["DeadCode.UnusedMembers"] = "未使用的成员",
            ["DeadCode.UnusedMethods"] = "未使用的方法",
            ["DeadCode.UnusedClasses"] = "未使用的类",
            ["DeadCode.UnusedFields"] = "未使用的字段",
            ["DeadCode.NoDeadCode"] = "✅ 未发现明显的死代码",
            ["DeadCode.DefinedAt"] = "定义于",
            ["DeadCode.Accessibility"] = "可访问性",
            ["DeadCode.Namespace"] = "命名空间",

            // 性能
            ["Performance.Title"] = "⚡ 性能问题",
            ["Performance.IssuesDetected"] = "检测到的问题",
            ["Performance.NoIssues"] = "✅ 未检测到明显的性能问题",
            ["Performance.Problem"] = "问题",
            ["Performance.Location"] = "位置",
            ["Performance.Impact"] = "影响",
            ["Performance.Recommendation"] = "建议",
            ["Performance.CodeExample"] = "代码示例",
            ["Performance.SuggestedFix"] = "建议改进",

            // 架构
            ["Architecture.Title"] = "🏗️ 架构分析",
            ["Architecture.Dependencies"] = "依赖关系",
            ["Architecture.ProjectDependencyCount"] = "项目依赖数",
            ["Architecture.ExternalReferenceCount"] = "外部引用数",
            ["Architecture.CircularDependencies"] = "循环依赖",
            ["Architecture.TypeHierarchy"] = "类型层次",
            ["Architecture.NamespaceCount"] = "命名空间数量",
            ["Architecture.TypeCount"] = "类型数量",
            ["Architecture.InterfaceCount"] = "接口数量",
            ["Architecture.EnumCount"] = "枚举数量",

            // 测试覆盖率
            ["TestCoverage.Title"] = "🧪 测试覆盖率",
            ["TestCoverage.CoverageStats"] = "覆盖率统计",
            ["TestCoverage.LineCoverage"] = "行覆盖率",
            ["TestCoverage.BranchCoverage"] = "分支覆盖率",
            ["TestCoverage.MethodCoverage"] = "方法覆盖率",
            ["TestCoverage.Target"] = "目标",
            ["TestCoverage.NoCoverageInfo"] = "⚠️ 未检测到测试覆盖率信息",

            // 建议
            ["Recommendations.Title"] = "💡 改进建议",
            ["Recommendations.PriorityHigh"] = "优先级：高",
            ["Recommendations.PriorityMedium"] = "优先级：中",
            ["Recommendations.PriorityLow"] = "优先级：低",
            ["Recommendations.Title"] = "标题",
            ["Recommendations.Description"] = "问题描述",
            ["Recommendations.Impact"] = "影响",
            ["Recommendations.Effort"] = "工作量",
            ["Recommendations.RelatedFiles"] = "相关文件",

            // 趋势
            ["Trends.Title"] = "📊 趋势分析",
            ["Trends.CodeQualityTrends"] = "代码质量趋势",
            ["Trends.Timepoint"] = "时间点",
            ["Trends.Complexity"] = "复杂度",
            ["Trends.Maintainability"] = "维护性",
            ["Trends.Coverage"] = "覆盖率",
            ["Trends.TechnicalDebt"] = "技术债务",
            ["Trends.ChangeTrends"] = "变化趋势",
            ["Trends.ComplexityChange"] = "复杂度变化",
            ["Trends.MaintainabilityChange"] = "维护性变化",
            ["Trends.CoverageChange"] = "覆盖率变化",
            ["Trends.DebtChange"] = "技术债务变化",
            ["Trends.NoHistory"] = "⚠️ 暂无历史数据",

            // 总结
            ["Conclusion.Title"] = "📝 总结",
            ["Conclusion.OverallScore"] = "整体评分",
            ["Conclusion.Strengths"] = "优点",
            ["Conclusion.Weaknesses"] = "需改进",
            ["Conclusion.NextSteps"] = "下一步行动",
            ["Conclusion.Immediate"] = "立即处理",
            ["Conclusion.ShortTerm"] = "短期改进",
            ["Conclusion.LongTerm"] = "长期规划",

            // 通用
            ["Common.Yes"] = "是",
            ["Common.No"] = "否",
            ["Common.Total"] = "总计",
            ["Common.Average"] = "平均",
            ["Common.Maximum"] = "最大",
            ["Common.Minimum"] = "最小",
            ["Common.RatingExcellent"] = "优秀",
            ["Common.RatingGood"] = "良好",
            ["Common.RatingFair"] = "一般",
            ["Common.RatingPoor"] = "较差",
            ["Common.StatusOk"] = "✅",
            ["Common.StatusWarning"] = "⚠️",
            ["Common.StatusError"] = "❌"
        },
        ["en"] = new()
        {
            // Titles
            ["Report.Title"] = "📊 .NET Code Analysis Report",
            ["Report.Project"] = "Project",
            ["Report.GeneratedAt"] = "Generated At",
            ["Report.Duration"] = "Duration",
            ["Report.Version"] = "Analyzer Version",

            // Summary
            ["Summary.Title"] = "📋 Executive Summary",
            ["Summary.Overview"] = "Overview",
            ["Summary.KeyMetrics"] = "Key Metrics",
            ["Summary.ProjectPath"] = "Project Path",
            ["Summary.ProjectType"] = "Project Type",
            ["Summary.FileCount"] = "Files Analyzed",
            ["Summary.LineCount"] = "Lines of Code",
            ["Summary.Status"] = "Status",
            ["Summary.ErrorCount"] = "Compile Errors",
            ["Summary.WarningCount"] = "Compile Warnings",
            ["Summary.AvgComplexity"] = "Avg Cyclomatic Complexity",
            ["Summary.MaintainabilityIndex"] = "Maintainability Index",
            ["Summary.TestCoverage"] = "Test Coverage",

            // Diagnostics
            ["Diagnostics.Title"] = "🔍 Compiler Diagnostics",
            ["Diagnostics.Errors"] = "❌ Errors",
            ["Diagnostics.Warnings"] = "⚠️ Warnings",
            ["Diagnostics.NoErrors"] = "✅ No compile errors",
            ["Diagnostics.NoWarnings"] = "✅ No compile warnings",
            ["Diagnostics.File"] = "File",
            ["Diagnostics.Line"] = "Line",
            ["Diagnostics.Code"] = "Error Code",
            ["Diagnostics.Message"] = "Message",
            ["Diagnostics.Severity"] = "Severity",
            ["Diagnostics.Suggestion"] = "Suggestion",

            // Metrics
            ["Metrics.Title"] = "📈 Code Metrics",
            ["Metrics.ComplexityAnalysis"] = "Complexity Analysis",
            ["Metrics.CyclomaticComplexity"] = "Cyclomatic Complexity (Avg)",
            ["Metrics.MaxCyclomaticComplexity"] = "Cyclomatic Complexity (Max)",
            ["Metrics.CognitiveComplexity"] = "Cognitive Complexity (Avg)",
            ["Metrics.NestingDepth"] = "Nesting Depth (Max)",
            ["Metrics.MaintainabilityAnalysis"] = "Maintainability Analysis",
            ["Metrics.MaintainabilityIndex"] = "Maintainability Index",
            ["Metrics.CodeVolume"] = "Code Volume",
            ["Metrics.FileCount"] = "File Count",
            ["Metrics.ClassCount"] = "Class Count",
            ["Metrics.DuplicationAnalysis"] = "Code Duplication",
            ["Metrics.DuplicationPercentage"] = "Duplication %",
            ["Metrics.DuplicateBlockCount"] = "Duplicate Blocks",
            ["Metrics.TotalDuplicateLines"] = "Total Duplicate Lines",
            ["Metrics.Threshold"] = "Threshold",
            ["Metrics.Status"] = "Status",

            // Dead Code
            ["DeadCode.Title"] = "💀 Dead Code Detection",
            ["DeadCode.UnusedMembers"] = "Unused Members",
            ["DeadCode.UnusedMethods"] = "Unused Methods",
            ["DeadCode.UnusedClasses"] = "Unused Classes",
            ["DeadCode.UnusedFields"] = "Unused Fields",
            ["DeadCode.NoDeadCode"] = "✅ No obvious dead code found",
            ["DeadCode.DefinedAt"] = "Defined at",
            ["DeadCode.Accessibility"] = "Accessibility",
            ["DeadCode.Namespace"] = "Namespace",

            // Performance
            ["Performance.Title"] = "⚡ Performance Issues",
            ["Performance.IssuesDetected"] = "Issues Detected",
            ["Performance.NoIssues"] = "✅ No obvious performance issues detected",
            ["Performance.Problem"] = "Problem",
            ["Performance.Location"] = "Location",
            ["Performance.Impact"] = "Impact",
            ["Performance.Recommendation"] = "Recommendation",
            ["Performance.CodeExample"] = "Code Example",
            ["Performance.SuggestedFix"] = "Suggested Fix",

            // Architecture
            ["Architecture.Title"] = "🏗️ Architecture Analysis",
            ["Architecture.Dependencies"] = "Dependencies",
            ["Architecture.ProjectDependencyCount"] = "Project Dependencies",
            ["Architecture.ExternalReferenceCount"] = "External References",
            ["Architecture.CircularDependencies"] = "Circular Dependencies",
            ["Architecture.TypeHierarchy"] = "Type Hierarchy",
            ["Architecture.NamespaceCount"] = "Namespaces",
            ["Architecture.TypeCount"] = "Types",
            ["Architecture.InterfaceCount"] = "Interfaces",
            ["Architecture.EnumCount"] = "Enums",

            // Test Coverage
            ["TestCoverage.Title"] = "🧪 Test Coverage",
            ["TestCoverage.CoverageStats"] = "Coverage Statistics",
            ["TestCoverage.LineCoverage"] = "Line Coverage",
            ["TestCoverage.BranchCoverage"] = "Branch Coverage",
            ["TestCoverage.MethodCoverage"] = "Method Coverage",
            ["TestCoverage.Target"] = "Target",
            ["TestCoverage.NoCoverageInfo"] = "⚠️ No test coverage info available",

            // Recommendations
            ["Recommendations.Title"] = "💡 Recommendations",
            ["Recommendations.PriorityHigh"] = "Priority: High",
            ["Recommendations.PriorityMedium"] = "Priority: Medium",
            ["Recommendations.PriorityLow"] = "Priority: Low",
            ["Recommendations.Title"] = "Title",
            ["Recommendations.Description"] = "Description",
            ["Recommendations.Impact"] = "Impact",
            ["Recommendations.Effort"] = "Effort",
            ["Recommendations.RelatedFiles"] = "Related Files",

            // Trends
            ["Trends.Title"] = "📊 Trend Analysis",
            ["Trends.CodeQualityTrends"] = "Code Quality Trends",
            ["Trends.Timepoint"] = "Time",
            ["Trends.Complexity"] = "Complexity",
            ["Trends.Maintainability"] = "Maintainability",
            ["Trends.Coverage"] = "Coverage",
            ["Trends.TechnicalDebt"] = "Technical Debt",
            ["Trends.ChangeTrends"] = "Change Trends",
            ["Trends.ComplexityChange"] = "Complexity Change",
            ["Trends.MaintainabilityChange"] = "Maintainability Change",
            ["Trends.CoverageChange"] = "Coverage Change",
            ["Trends.DebtChange"] = "Debt Change",
            ["Trends.NoHistory"] = "⚠️ No historical data available",

            // Conclusion
            ["Conclusion.Title"] = "📝 Conclusion",
            ["Conclusion.OverallScore"] = "Overall Score",
            ["Conclusion.Strengths"] = "Strengths",
            ["Conclusion.Weaknesses"] = "Weaknesses",
            ["Conclusion.NextSteps"] = "Next Steps",
            ["Conclusion.Immediate"] = "Immediate (This Week)",
            ["Conclusion.ShortTerm"] = "Short-term (This Month)",
            ["Conclusion.LongTerm"] = "Long-term (This Quarter)",

            // Common
            ["Common.Yes"] = "Yes",
            ["Common.No"] = "No",
            ["Common.Total"] = "Total",
            ["Common.Average"] = "Average",
            ["Common.Maximum"] = "Max",
            ["Common.Minimum"] = "Min",
            ["Common.RatingExcellent"] = "Excellent",
            ["Common.RatingGood"] = "Good",
            ["Common.RatingFair"] = "Fair",
            ["Common.RatingPoor"] = "Poor",
            ["Common.StatusOk"] = "✅",
            ["Common.StatusWarning"] = "⚠️",
            ["Common.StatusError"] = "❌"
        }
    };

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="key">资源键</param>
    /// <param name="culture">文化信息（默认使用当前文化）</param>
    /// <returns>本地化字符串</returns>
    public static string GetString(string key, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var cultureName = culture.Name;

        // 尝试获取特定文化的资源
        if (_resources.TryGetValue(cultureName, out var cultureResources))
        {
            if (cultureResources.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        // 回退到英语
        if (_resources.TryGetValue("en", out var enResources))
        {
            if (enResources.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        // 未找到资源，返回键本身
        return key;
    }

    /// <summary>
    /// 获取本地化字符串（使用格式化参数）
    /// </summary>
    /// <param name="key">资源键</param>
    /// <param name="args">格式化参数</param>
    /// <returns>格式化的本地化字符串</returns>
    public static string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        return string.Format(format, args);
    }

    /// <summary>
    /// 获取所有支持的文化
    /// </summary>
    public static IEnumerable<string> SupportedCultures => _resources.Keys;

    /// <summary>
    /// 检查是否支持指定的文化
    /// </summary>
    public static bool IsCultureSupported(string cultureName)
    {
        return _resources.ContainsKey(cultureName);
    }
}
