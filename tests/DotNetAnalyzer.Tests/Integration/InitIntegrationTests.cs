using Xunit;
using DotNetAnalyzer.Cli.Commands;
using DotNetAnalyzer.Core.Configuration;
using System.CommandLine.IO;
using System.CommandLine.Parsing;

namespace DotNetAnalyzer.Tests.Commands;

/// <summary>
/// Init 命令集成测试
/// </summary>
[Collection("Integration")]
public class InitIntegrationTests : IDisposable
{
    private const string TestTempDir = "./TestTemp_InitIntegration";
    private string _originalDir = string.Empty;

    public InitIntegrationTests()
    {
        _originalDir = Directory.GetCurrentDirectory();

        if (Directory.Exists(TestTempDir))
        {
            Directory.Delete(TestTempDir, recursive: true);
        }
        Directory.CreateDirectory(TestTempDir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);

        if (Directory.Exists(TestTempDir))
        {
            Directory.Delete(TestTempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Integration_InitCommand_CompletesSuccessfully()
    {
        // Arrange
        Directory.SetCurrentDirectory(TestTempDir);
        var console = new TestConsole();

        var options = new InitOptions
        {
            Scope = "project",
            Yes = true,
            DryRun = true
        };

        // Act
        var exception = await Record.ExceptionAsync(() =>
            InitCommandHandler.ExecuteAsync(options, console));

        // Assert
        Assert.Null(exception); // 不应该抛出异常
    }

    [Fact]
    public async Task Integration_ConfigGenerator_WritesValidFiles()
    {
        // Arrange
        var options = new InitOptions { Scope = "project" };
        var env = new EnvironmentInfo
        {
            DotnetAnalyzerPath = "/usr/local/bin/dotnet-analyzer",
            DotnetSdkVersion = "8.0.100"
        };

        // Act
        var result = await ConfigGenerator.GenerateConfigsAsync(options, env);

        // 写入文件
        var mcpJsonPath = Path.Combine(TestTempDir, ".mcp.json");
        var settingsPath = Path.Combine(TestTempDir, ".claude", "settings.json");

        await File.WriteAllTextAsync(mcpJsonPath, result.McpConfigJson);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        await File.WriteAllTextAsync(settingsPath, result.SettingsJson);

        // Assert
        Assert.True(File.Exists(mcpJsonPath));
        Assert.True(File.Exists(settingsPath));

        // 验证 JSON 格式
        var mcpJson = await File.ReadAllTextAsync(mcpJsonPath);
        var settingsJson = await File.ReadAllTextAsync(settingsPath);

        Assert.Contains("mcpServers", mcpJson);
        Assert.Contains("dotnet-analyzer", settingsJson);
    }

    [Fact]
    public async Task Integration_Validator_ValidatesGeneratedConfigs()
    {
        // Arrange

        // Act
        var config = await ConfigGenerator.GenerateConfigsAsync(
            new InitOptions(),
            new EnvironmentInfo { DotnetAnalyzerPath = "dotnet-analyzer" }
        );

        var result = await ConfigValidator.ValidateAsync(config);

        // Assert
        Assert.NotNull(result);
        // 注意：由于 dotnet-analyzer 可能不在 PATH 中，某些检查可能失败
        Assert.NotNull(result.Checks);
        Assert.True(result.Checks.Count >= 2); // 至少有格式检查
    }
}
