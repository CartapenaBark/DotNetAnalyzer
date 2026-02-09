using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Roslyn;
using DotNetAnalyzer.Core.Memory;

namespace DotNetAnalyzer.Cli;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // 添加配置支持
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
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
}
