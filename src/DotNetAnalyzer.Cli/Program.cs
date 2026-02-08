using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotNetAnalyzer.Core.Roslyn;

namespace DotNetAnalyzer.Cli;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // 注册核心服务
        builder.Services.AddSingleton<WorkspaceManager>();

        // 配置 MCP 服务器
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }
}
