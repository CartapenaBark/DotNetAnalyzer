using DotNetAnalyzer.Core.Configuration;

namespace DotNetAnalyzer.Cli.Commands;

/// <summary>
/// 交互式配置向导
/// </summary>
public static class InitWizard
{
    /// <summary>
    /// 运行交互式向导
    /// </summary>
    public static async Task<InitOptions> RunAsync(
        Core.Configuration.EnvironmentInfo env,
        InitOptions defaults,
        TextWriter output,
        TextReader input)
    {
        output.WriteLine();
        output.WriteLine("检测到环境信息：");
        output.WriteLine($"  ✓ dotnet-analyzer: {env.DotnetAnalyzerPath}");
        output.WriteLine($"  ✓ .NET SDK: {env.DotnetSdkVersion}");
        if (env.ProjectFiles.Length > 0)
        {
            output.WriteLine($"  ✓ 项目: {string.Join(", ", env.ProjectFiles.Select(Path.GetFileName))}");
        }
        output.WriteLine();

        // 配置范围
        string scope = defaults.Scope;
        if (!defaults.Yes)
        {
            output.Write("配置范围 [(P)roject/(U]ser]: ");
            var scopeInput = await input.ReadLineAsync();
            if (scopeInput?.Trim().Length > 0 && scopeInput.Trim()[0] == 'U')
            {
                scope = "user";
            }
        }

        output.WriteLine();
        output.WriteLine($"配置范围: {scope}");
        output.WriteLine();

        // 返回新的选项对象
        return new InitOptions
        {
            Scope = scope,
            Output = defaults.Output,
            Force = defaults.Force,
            Verify = defaults.Verify,
            Verbose = defaults.Verbose,
            Yes = defaults.Yes,
            DryRun = defaults.DryRun
        };
    }
}
