using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Roslyn;
using DotNetAnalyzer.Core.Memory;

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

        // 注册核心服务为 Scoped，以支持依赖注入和更好的资源管理
        builder.Services.AddScoped<IWorkspaceManager, WorkspaceManager>();
        builder.Services.AddScoped<ICompilationCache, CompilationCache>();

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

        // 优先使用 InformationalVersion（如果存在），否则使用 AssemblyVersion
        // informationalVersion 通常包含语义化版本（如 0.6.1）
        // assemblyVersion 通常只是 AssemblyVersion（如 0.6.1.0）
        return !string.IsNullOrEmpty(informationalVersion) ? informationalVersion
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
        Console.WriteLine("  https://github.com/yourusername/DotNetAnalyzer");
    }
}
