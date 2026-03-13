using Microsoft.Extensions.Logging;
using DotNetAnalyzer.Core.Configuration;

namespace DotNetAnalyzer.Cli.Commands;

/// <summary>
/// Init 命令 - 初始化 DotNetAnalyzer MCP 配置
/// </summary>
public static class InitCommand
{
    /// <summary>
    /// 执行 Init 命令
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <returns>退出代码</returns>
    public static async Task<int> ExecuteAsync(string[] args)
    {
        var options = ParseArguments(args);
        return await InitCommandHandler.ExecuteAsync(options, Console.Out, Console.In);
    }

    /// <summary>
    /// 解析命令行参数
    /// </summary>
    private static InitOptions ParseArguments(string[] args)
    {
        var options = new InitOptions
        {
            Scope = "project",
            Force = false,
            Verify = true,
            Verbose = false,
            Yes = false,
            DryRun = false
        };

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--scope":
                    if (i + 1 < args.Length)
                        options.Scope = args[++i];
                    break;
                case "--output":
                    if (i + 1 < args.Length)
                        options.Output = args[++i];
                    break;
                case "--force":
                    options.Force = true;
                    break;
                case "--verify":
                    options.Verify = true;
                    break;
                case "--no-verify":
                    options.Verify = false;
                    break;
                case "--verbose":
                    options.Verbose = true;
                    break;
                case "--yes":
                    options.Yes = true;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "-h":
                case "--help":
                    ShowHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        return options;
    }

    /// <summary>
    /// 显示帮助信息
    /// </summary>
    private static void ShowHelp()
    {
        Console.WriteLine("DotNetAnalyzer Init - 初始化 MCP 配置");
        Console.WriteLine();
        Console.WriteLine("用法:");
        Console.WriteLine("  dotnet-analyzer init [选项]");
        Console.WriteLine();
        Console.WriteLine("选项:");
        Console.WriteLine("  --scope <value>     配置范围：project（项目级）| user（用户级），默认：project");
        Console.WriteLine("  --output <dir>      输出目录（默认：当前目录）");
        Console.WriteLine("  --force             覆盖现有配置");
        Console.WriteLine("  --verify            配置后验证连接（默认）");
        Console.WriteLine("  --no-verify         跳过连接验证");
        Console.WriteLine("  --verbose           显示详细输出");
        Console.WriteLine("  --yes               跳过所有提示，使用默认值");
        Console.WriteLine("  --dry-run           预览将要执行的操作");
        Console.WriteLine("  -h, --help          显示帮助信息");
        Console.WriteLine();
        Console.WriteLine("示例:");
        Console.WriteLine("  dotnet-analyzer init");
        Console.WriteLine("  dotnet-analyzer init --scope user");
        Console.WriteLine("  dotnet-analyzer init --force --yes");
    }
}
