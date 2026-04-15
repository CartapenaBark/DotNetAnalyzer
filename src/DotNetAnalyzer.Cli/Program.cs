using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Architecture;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Roslyn;
using DotNetAnalyzer.Core.Memory;
using DotNetAnalyzer.Core.Analysis;
using DotNetAnalyzer.Core.Analysis.CodeQuality;
using DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;
using DotNetAnalyzer.Core.Monitoring;
using DotNetAnalyzer.Core.Caching;
using DotNetAnalyzer.Core.Visualization;
using DotNetAnalyzer.Core.Decompilation;
using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.Security.Detectors;
using DotNetAnalyzer.Core.DependencyHealth;
using DotNetAnalyzer.Core.Performance;
using DotNetAnalyzer.Core.Xaml;
using DotNetAnalyzer.Core.Analysis.Desktop;
using DotNetAnalyzer.Core.ProjectManipulation;

namespace DotNetAnalyzer.Cli;

internal sealed class Program
{
    private static async Task Main(string[] args)
    {
        // 处理命令行参数
        if (args.Length > 0)
        {
            switch (args[0])
            {
                case "--version":
                case "-v":
                    Console.WriteLine("dotnet-analyzer version " + GetVersion());
                    return;
                case "--help":
                case "-h":
                    ShowHelp();
                    return;
                case "mcp":
                    // MCP serve 子命令（默认行为）
                    if (args.Length > 1 && args[1] == "serve")
                    {
                        // 继续执行 MCP 服务器启动
                        args = [.. args.Skip(2)];
                    }
                    break;
            }
        }

        var builder = Host.CreateApplicationBuilder(args);

        // 添加配置支持
        // 注意：appsettings.json 设为可选，以便 CLI 工具可以在任何目录运行（如 MCP 服务器）
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);

        // Per-project 配置：用户级（最低优先级）
        var userConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotnet-analyzer", "config.json");
        if (File.Exists(userConfigPath))
        {
            builder.Configuration.AddJsonFile(userConfigPath, optional: true, reloadOnChange: true);
        }

        // 配置日志
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // 注册配置选项
        builder.Services.AddOptions<WorkspaceManagerOptions>()
            .Bind(builder.Configuration.GetSection("WorkspaceManager"));
        builder.Services.AddOptions<CompilationCacheOptions>()
            .Bind(builder.Configuration.GetSection("CompilationCache"));
        builder.Services.AddOptions<MemoryMonitoringOptions>()
            .Bind(builder.Configuration.GetSection("MemoryMonitoring"));
        builder.Services.AddOptions<SecurityOptions>()
            .Bind(builder.Configuration.GetSection("Security"));
        builder.Services.AddOptions<DependencyHealthOptions>()
            .Bind(builder.Configuration.GetSection("DependencyHealth"));
        builder.Services.AddOptions<AnalyzerOptions>()
            .Bind(builder.Configuration.GetSection("Analyzer"));

        // 注册 HttpClient 用于 NuGet API 调用（带超时配置）
        builder.Services.AddSingleton(
            new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

        // 注册核心服务为 Scoped，以支持依赖注入和更好的资源管理
        builder.Services.AddScoped<IWorkspaceManager, WorkspaceManager>();
        builder.Services.AddScoped<ICompilationCache, CompilationCache>();

        // 注册代码质量分析服务
        builder.Services.AddScoped<CodeSmellAnalyzer>();
        builder.Services.AddScoped<TechnicalDebtCalculator>();
        builder.Services.AddScoped<ChangeImpactAnalyzer>();

        // 注册覆盖率分析服务
        builder.Services.AddScoped<CoverageDataParser>();
        builder.Services.AddScoped<TestCoverageAnalyzer>();

        // 注册所有代码异味检测器
        builder.Services.AddScoped<ICodeSmellDetector, LongMethodDetector>();
        builder.Services.AddScoped<ICodeSmellDetector, LargeClassDetector>();
        builder.Services.AddScoped<ICodeSmellDetector, LongParameterListDetector>();
        builder.Services.AddScoped<ICodeSmellDetector, FeatureEnvyDetector>();
        builder.Services.AddScoped<ICodeSmellDetector, DataClumpsDetector>();
        builder.Services.AddScoped<ICodeSmellDetector, PrimitiveObsessionDetector>();
        builder.Services.AddScoped<ICodeSmellDetector, CircularDependencyDetector>();
        builder.Services.AddScoped<ICodeSmellDetector, InappropriateIntimacyDetector>();
        builder.Services.AddScoped<ICodeSmellDetector, GodClassDetector>();
        builder.Services.AddScoped<ICodeSmellDetector, ShotgunSurgeryDetector>();
        builder.Services.AddScoped<ICodeSmellDetector, DuplicateCodeDetector>();
        builder.Services.AddScoped<ICodeSmellDetector, MagicNumberDetector>();

        // 注册监控和缓存服务
        builder.Services.AddScoped<IFileWatcher, FileSystemFileWatcher>();
        builder.Services.AddScoped<IAnalysisResultCache, InMemoryAnalysisResultCache>();

        // 注册可视化服务
        builder.Services.AddScoped<DependencyGraphVisualizer>();
        builder.Services.AddScoped<HeatmapGenerator>();
        builder.Services.AddScoped<GraphLayoutEngine>();

        // 注册架构分析服务
        builder.Services.AddScoped<ArchitectureConfigReader>();
        builder.Services.AddScoped<ArchitectureRuleEngine>();

        // 注册反编译服务
        builder.Services.AddSingleton<AssemblyCache>();
        builder.Services.AddScoped<IDecompilationService,
            CSharpDecompilerService>();
        builder.Services.AddScoped<AssemblyMetadataReader>();
        builder.Services.AddScoped<ILAnalyzer>();

        // 注册安全检测服务
        builder.Services.AddScoped<SecurityAnalysisEngine>();
        builder.Services.AddSingleton<ISecurityDetector, HardcodedCredentialDetector>();
        builder.Services.AddSingleton<ISecurityDetector, SqlInjectionDetector>();
        builder.Services.AddSingleton<ISecurityDetector, CommandInjectionDetector>();
        builder.Services.AddSingleton<ISecurityDetector, UnsafeDeserializationDetector>();
        builder.Services.AddSingleton<ISecurityDetector, PathTraversalDetector>();
        builder.Services.AddSingleton<ISecurityDetector, XssInAspNetDetector>();

        // 注册依赖健康度服务
        builder.Services.AddScoped<INuGetClient, NuGetApiClient>();
        builder.Services.AddScoped<ProjectFileDependencyExtractor>();
        builder.Services.AddScoped<NuGetAssetsFileParser>();
        builder.Services.AddScoped<DependencyHealthAnalyzer>();
        builder.Services.AddScoped<DependencyConflictDetector>();

        // 注册性能分析服务
        builder.Services.AddScoped<DotNetAnalyzer.Core.Performance.PerformanceAnalyzer>();

        // 注册 XAML 分析服务
        builder.Services.AddScoped<XamlParser>();
        builder.Services.AddScoped<XamlBindingValidator>();
        builder.Services.AddScoped<XamlResourceAnalyzer>();
        builder.Services.AddScoped<ViewModelMapper>();

        // 注册桌面应用模式检测服务
        builder.Services.AddScoped<MvvmViolationDetector>();
        builder.Services.AddScoped<AsyncPatternAnalyzer>();
        builder.Services.AddScoped<DependencyInjectionAnalyzer>();
        builder.Services.AddScoped<MemoryLeakDetector>();

        // 注册项目文件操作服务
        builder.Services.AddScoped<ProjectFileEditor>();
        builder.Services.AddScoped<ProjectFileAnalyzer>();
        builder.Services.AddScoped<NuGetPackageService>();

        // 配置 MCP 服务器
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }

    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyVersion = assembly.GetName().Version;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var normalizedInformationalVersion = informationalVersion?.Split('+', 2)[0];

        // 优先使用 InformationalVersion（如果存在），否则使用 AssemblyVersion
        // informationalVersion 通常包含语义化版本（如 0.6.1）
        // assemblyVersion 通常只是 AssemblyVersion（如 0.6.1.0）
        return !string.IsNullOrEmpty(normalizedInformationalVersion) ? normalizedInformationalVersion
               : assemblyVersion?.ToString() ?? "unknown";
    }

    private static void ShowHelp()
    {
        Console.WriteLine("DotNetAnalyzer - .NET MCP Server for Claude Code");
        Console.WriteLine("Version: " + GetVersion());
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet-analyzer [options] [command]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --version     Show version information");
        Console.WriteLine("  -h, --help        Show help information");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  mcp serve         Start MCP server (default)");
        Console.WriteLine();
        Console.WriteLine("When run without options, dotnet-analyzer starts as an MCP server");
        Console.WriteLine("and waits for stdio input (for use with Claude Code).");
        Console.WriteLine();
        Console.WriteLine("For more information, visit:");
        Console.WriteLine("  https://github.com/CartapenaBark/DotNetAnalyzer");
    }
}
