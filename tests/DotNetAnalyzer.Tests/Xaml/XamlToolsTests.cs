using System.Text.Json;
using DotNetAnalyzer.Core.Xaml;
using DotNetAnalyzer.Core.Xaml.Models;
using DotNetAnalyzer.Cli.Tools;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.Xaml;

/// <summary>
/// XamlTools MCP 工具测试。
/// </summary>
/// <remarks>
/// 覆盖 analyze_xaml 工具的 JSON 响应格式和错误处理。
/// </remarks>
public class XamlToolsTests : IDisposable
{
    private readonly XamlParser _parser;
    private readonly List<IDisposable> _tempDirs = [];

    public XamlToolsTests()
    {
        _parser = new XamlParser(
            NullLogger<XamlParser>.Instance);
    }

    public void Dispose()
    {
        foreach (var d in _tempDirs)
        {
            d.Dispose();
        }
        _tempDirs.Clear();
    }

    private string CreateTempXaml(string content)
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            $"XamlToolsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "TestWindow.xaml");
        File.WriteAllText(file, content);
        _tempDirs.Add(new TempDirCleanup(dir));
        return file;
    }

    [Fact]
    public async Task AnalyzeXaml_ValidFile_ReturnsSuccessJson()
    {
        // Arrange
        var xaml = """
            <Window
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="MyApp.MainWindow">
                <Grid>
                    <TextBlock Text="{Binding Title}" />
                </Grid>
            </Window>
            """;
        var filePath = CreateTempXaml(xaml);

        // Act
        var json = await XamlTools.AnalyzeXaml(_parser, filePath);

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("success", out var success)
            .Should().BeTrue("response should have success property");
        success.GetBoolean().Should().BeTrue();

        doc.RootElement.TryGetProperty("data", out var data)
            .Should().BeTrue();
        data.TryGetProperty("rootElement", out var root)
            .Should().BeTrue();
        root.GetString().Should().Be("Window");
        data.TryGetProperty("summary", out var summary)
            .Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeXaml_NonExistentFile_ReturnsErrorJson()
    {
        // Act
        var json = await XamlTools.AnalyzeXaml(
            _parser, "/nonexistent/file.xaml");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("success", out var success)
            .Should().BeTrue();
        success.GetBoolean().Should().BeFalse();
        doc.RootElement.TryGetProperty("error", out var error)
            .Should().BeTrue();
        error.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AnalyzeXaml_FileWithBindings_ReturnsBindingCount()
    {
        // Arrange
        var xaml = """
            <Window
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="MyApp.MainWindow">
                <StackPanel>
                    <TextBlock Text="{Binding Name}" />
                    <TextBox Text="{Binding Value, Mode=TwoWay}" />
                    <Button Command="{Binding SaveCommand}" />
                </StackPanel>
            </Window>
            """;
        var filePath = CreateTempXaml(xaml);

        // Act
        var json = await XamlTools.AnalyzeXaml(_parser, filePath);

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("data")
            .GetProperty("summary")
            .GetProperty("bindingCount")
            .GetInt32().Should().Be(3);
    }

    private sealed class TempDirCleanup : IDisposable
    {
        private readonly string _directory;

        public TempDirCleanup(string directory)
        {
            _directory = directory;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
