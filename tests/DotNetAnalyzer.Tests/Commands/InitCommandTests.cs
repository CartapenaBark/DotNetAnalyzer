using Xunit;
using Moq;
using DotNetAnalyzer.Cli.Commands;
using DotNetAnalyzer.Core.Configuration;
using System.CommandLine;
using System.CommandLine.IO;
using System.Text.Json;

namespace DotNetAnalyzer.Tests.Commands;

/// <summary>
/// Init 命令单元测试
/// </summary>
public class InitCommandTests : IDisposable
{
    private const string TestTempDir = "./TestTemp_Init";

    public InitCommandTests()
    {
        // 创建测试临时目录
        if (Directory.Exists(TestTempDir))
        {
            Directory.Delete(TestTempDir, recursive: true);
        }
        Directory.CreateDirectory(TestTempDir);
    }

    public void Dispose()
    {
        // 清理测试临时目录
        if (Directory.Exists(TestTempDir))
        {
            Directory.Delete(TestTempDir, recursive: true);
        }
    }

    [Fact]
    public async Task InitCommand_Create_ReturnsCommand()
    {
        // Act
        var command = InitCommand.Create();

        // Assert
        Assert.NotNull(command);
        Assert.Equal("init", command.Name);
        Assert.Equal(7, command.Options.Count());
    }

    [Fact]
    public void InitOptions_DefaultValues()
    {
        // Arrange
        var options = new InitOptions();

        // Assert
        Assert.Equal("project", options.Scope);
        Assert.False(options.Force);
        Assert.True(options.Verify);
        Assert.False(options.Verbose);
        Assert.False(options.Yes);
        Assert.False(options.DryRun);
    }

    [Fact]
    public async Task EnvironmentDetector_DetectAsync_ReturnsEnvironmentInfo()
    {
        // Arrange
        var detector = new EnvironmentDetector();

        // Act
        var info = await detector.DetectAsync();

        // Assert
        Assert.NotNull(info);
        Assert.NotEmpty(info.DotnetAnalyzerPath);
        Assert.NotEmpty(info.DotnetSdkVersion);
        Assert.NotEmpty(info.OperatingSystem);
        Assert.NotNull(info.ShellType);
        Assert.NotNull(info.ProjectFiles);
        Assert.NotNull(info.ExistingConfig);
    }

    [Fact]
    public async Task ConfigGenerator_GenerateMcpConfigAsync_ReturnsValidConfig()
    {
        // Arrange
        var generator = new ConfigGenerator();
        var options = new InitOptions { Scope = "project" };
        var env = new EnvironmentInfo
        {
            DotnetAnalyzerPath = "/usr/local/bin/dotnet-analyzer",
            DotnetSdkVersion = "8.0.100",
            OperatingSystem = "macOS",
            ShellType = "zsh"
        };

        // Act
        var result = await generator.GenerateConfigsAsync(options, env);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.McpConfigJson);
        Assert.NotEmpty(result.SettingsJson);

        // 验证 MCP 配置 JSON 格式
        using var mcpDoc = JsonDocument.Parse(result.McpConfigJson);
        Assert.True(mcpDoc.RootElement.TryGetProperty("mcpServers", out var servers));
        Assert.True(servers!.ValueKind == JsonValueKind.Object);

        // 验证包含 dotnet-analyzer 服务器
        var hasDotnetAnalyzer = servers!.Value.EnumerateObject()
            .Any(p => p.Name == "dotnet-analyzer");
        Assert.True(hasDotnetAnalyzer);
    }

    [Fact]
    public async Task ConfigGenerator_GenerateClaudeSettingsAsync_ReturnsValidSettings()
    {
        // Arrange
        var generator = new ConfigGenerator();
        var options = new InitOptions { Scope = "project" };
        var env = new EnvironmentInfo();

        // Act
        var result = await generator.GenerateConfigsAsync(options, env);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.SettingsJson);

        // 验证 Settings JSON 格式
        using var settingsDoc = JsonDocument.Parse(result.SettingsJson);
        Assert.True(settingsDoc.RootElement.TryGetProperty("enabledMcpjsonServers", out var servers));
        Assert.True(servers!.ValueKind == JsonValueKind.Array);

        // 验证包含 dotnet-analyzer
        var serversArray = servers!.Value.EnumerateArray().ToArray();
        Assert.Contains("dotnet-analyzer", serversArray.Select(e => e.GetString()));
    }

    [Fact]
    public async Task ConfigValidator_ValidateAsync_WithValidConfig_ReturnsValidResult()
    {
        // Arrange
        var validator = new ConfigValidator();
        var validConfig = new ConfigGenerationResult
        {
            McpConfigJson = """
                {
                    "mcpServers": {
                        "dotnet-analyzer": {
                            "command": "/usr/local/bin/dotnet-analyzer",
                            "args": ["mcp", "serve"]
                        }
                    }
                }
                """,
            SettingsJson = """
                {
                    "enabledMcpjsonServers": ["dotnet-analyzer"],
                    "permissions": {
                        "allow": ["Bash(dotnet *)"]
                    }
                }
                """
        };

        // Act
        var result = await ConfigValidator.ValidateAsync(validConfig);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Checks);
        Assert.Equal(4, result.Checks.Count); // 4 个检查项
    }

    [Fact]
    public async Task ConfigValidator_ValidateAsync_WithInvalidMcpJson_ReturnsInvalidResult()
    {
        // Arrange
        var validator = new ConfigValidator();
        var invalidConfig = new ConfigGenerationResult
        {
            McpConfigJson = "{ invalid json }",
            SettingsJson = """
                {
                    "enabledMcpjsonServers": ["dotnet-analyzer"]
                }
                """
        };

        // Act
        var result = await ConfigValidator.ValidateAsync(invalidConfig);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid); // 应该验证失败
    }

    [Fact]
    public void InitOptions_Scope_Project_DefaultsToProject()
    {
        // Arrange & Act
        var options = new InitOptions { Scope = "project" };

        // Assert
        Assert.Equal("project", options.Scope);
    }

    [Fact]
    public void InitOptions_Scope_User_SetsToUser()
    {
        // Arrange & Act
        var options = new InitOptions { Scope = "user" };

        // Assert
        Assert.Equal("user", options.Scope);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void InitOptions_ForceAndVerify_CanBeSetIndependently(bool force, bool verify)
    {
        // Arrange & Act
        var options = new InitOptions { Force = force, Verify = verify };

        // Assert
        Assert.Equal(force, options.Force);
        Assert.Equal(verify, options.Verify);
    }

    [Fact]
    public async Task EnvironmentDetector_FindProjectFiles_FindsSolutionFiles()
    {
        // Arrange
        var detector = new EnvironmentDetector();
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            // 在测试目录创建测试 .sln 文件
            Directory.SetCurrentDirectory(TestTempDir);
            await File.WriteAllTextAsync("Test.sln", "");

            // Act
            var info = await detector.DetectAsync();

            // Assert
            Assert.Contains("Test.sln", info.ProjectFiles);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task EnvironmentDetector_DetectExistingConfig_DetectsExistingFiles()
    {
        // Arrange
        var detector = new EnvironmentDetector();
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(TestTempDir);

            // 创建测试配置文件
            Directory.CreateDirectory(".claude");
            await File.WriteAllTextAsync(".mcp.json", "{}");
            await File.WriteAllTextAsync(".claude/settings.json", "{}");

            // Act
            var info = await detector.DetectAsync();

            // Assert
            Assert.True(info.ExistingConfig.HasMcpJson);
            Assert.True(info.ExistingConfig.HasClaudeSettings);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }
}
