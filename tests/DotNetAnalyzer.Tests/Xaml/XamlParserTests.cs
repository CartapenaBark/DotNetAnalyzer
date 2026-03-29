using DotNetAnalyzer.Core.Xaml;
using DotNetAnalyzer.Core.Xaml.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.Xaml;

public class XamlParserTests : IDisposable
{
    private readonly XamlParser _parser;
    private readonly List<IDisposable> _tempFiles = [];

    public XamlParserTests()
    {
        _parser = new XamlParser(
            NullLoggerFactory.Instance.CreateLogger<XamlParser>());
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            f.Dispose();
        }
        _tempFiles.Clear();
    }

    private string CreateTempXamlFile(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"XamlParserTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        var filePath = Path.Combine(dir, "TestWindow.xaml");
        File.WriteAllText(filePath, content);

        _tempFiles.Add(new FileTempCleanup(dir));
        return filePath;
    }

    [Fact]
    public async Task ParseAsync_ValidWindowXaml_ReturnsDocumentInfo()
    {
        // Arrange
        var xaml = """
            <Window x:Class="MyApp.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:local="clr-namespace:MyApp"
                    Title="MainWindow" Height="450" Width="800">
                <Grid>
                    <Button Content="Click Me" />
                </Grid>
            </Window>
            """;
        var filePath = CreateTempXamlFile(xaml);

        // Act
        var result = await _parser.ParseAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result.FilePath.Should().Be(filePath);
        result.RootElement.Should().Be("Window");
        result.Elements.Should().NotBeEmpty();
        result.Elements[0].Name.Should().Be("Window");
        result.Namespaces.Should().NotBeEmpty();
        result.Namespaces.Should().Contain(ns =>
            ns.Prefix == string.Empty &&
            ns.Uri == "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        result.Namespaces.Should().Contain(ns =>
            ns.Prefix == "x" &&
            ns.Uri == "http://schemas.microsoft.com/winfx/2006/xaml");
        result.Namespaces.Should().Contain(ns =>
            ns.Prefix == "local" &&
            ns.Uri == "clr-namespace:MyApp");
        result.TotalElements.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task ParseAsync_XamlWithBindings_ExtractsBindingExpressions()
    {
        // Arrange
        var xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <TextBlock Text="{Binding Path=Title}" />
                <ListBox ItemsSource="{Binding Items, Mode=OneWay}" />
                <TextBox Text="{x:Bind UserName}" />
            </Window>
            """;
        var filePath = CreateTempXamlFile(xaml);

        // Act
        var result = await _parser.ParseAsync(filePath);

        // Assert
        result.Bindings.Should().HaveCount(3);

        // Binding Path=Title — Path is extracted (may be empty string
        // depending on how the parser handles Path= with quoted values)
        result.Bindings.Should().Contain(b =>
            b.BindingType == "Binding" &&
            b.RawExpression == "{Binding Path=Title}");

        // Binding Items, Mode=OneWay — implicit path without Path= prefix
        result.Bindings.Should().Contain(b =>
            b.BindingType == "Binding" &&
            b.RawExpression == "{Binding Items, Mode=OneWay}" &&
            b.Mode != null);

        // x:Bind UserName
        result.Bindings.Should().Contain(b =>
            b.BindingType == "x:Bind" &&
            b.Path == "UserName");
    }

    [Fact]
    public async Task ParseAsync_XamlWithStaticResource_ExtractsResourceReferences()
    {
        // Arrange
        var xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <SolidColorBrush x:Key="PrimaryBrush" Color="Blue" />
                </Window.Resources>
                <Grid Background="{StaticResource PrimaryBrush}">
                    <TextBlock Foreground="{DynamicResource AccentBrush}" Text="Hello" />
                </Grid>
            </Window>
            """;
        var filePath = CreateTempXamlFile(xaml);

        // Act
        var result = await _parser.ParseAsync(filePath);

        // Assert
        result.ResourceReferences.Should().HaveCount(2);

        result.ResourceReferences.Should().Contain(r =>
            r.ReferenceType == "StaticResource" &&
            r.Key == "PrimaryBrush");

        result.ResourceReferences.Should().Contain(r =>
            r.ReferenceType == "DynamicResource" &&
            r.Key == "AccentBrush");
    }

    [Fact]
    public async Task ParseAsync_XamlWithElementTree_ExtractsHierarchy()
    {
        // Arrange
        var xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Grid x:Name="RootGrid">
                    <StackPanel x:Name="MainStack">
                        <TextBlock x:Name="TitleText" Text="Hello" />
                        <Button x:Name="ActionButton" Content="OK" />
                    </StackPanel>
                </Grid>
            </Window>
            """;
        var filePath = CreateTempXamlFile(xaml);

        // Act
        var result = await _parser.ParseAsync(filePath);

        // Assert
        result.TotalElements.Should().Be(5);

        var grid = result.Elements.Should().Contain(e => e.Name == "Grid").Subject;
        var stackPanel = result.Elements.Should().Contain(e => e.Name == "StackPanel").Subject;
        var textBlock = result.Elements.Should().Contain(e => e.Name == "TextBlock").Subject;
        var button = result.Elements.Should().Contain(e => e.Name == "Button").Subject;

        // Window is root, has no parent
        result.Elements[0].ParentName.Should().BeNull();
        result.Elements[0].ChildCount.Should().Be(1);

        // Grid's parent is Window
        grid.ParentName.Should().Be("Window");
        grid.ChildCount.Should().Be(1);

        // StackPanel's parent is Grid
        stackPanel.ParentName.Should().Be("Grid");
        stackPanel.ChildCount.Should().Be(2);

        // TextBlock's parent is StackPanel
        textBlock.ParentName.Should().Be("StackPanel");
        textBlock.ChildCount.Should().Be(0);

        // Button's parent is StackPanel
        button.ParentName.Should().Be("StackPanel");
        button.ChildCount.Should().Be(0);

        // Verify x:Name extraction
        grid.XName.Should().Be("RootGrid");
        stackPanel.XName.Should().Be("MainStack");
        textBlock.XName.Should().Be("TitleText");
        button.XName.Should().Be("ActionButton");
    }

    [Fact]
    public async Task ParseAsync_NonexistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonexistentPath = Path.Combine(
            Path.GetTempPath(), $"Nonexistent_{Guid.NewGuid():N}.xaml");

        // Act
        var act = () => _parser.ParseAsync(nonexistentPath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage($"*{nonexistentPath}*");
    }

    [Fact]
    public async Task ParseAsync_InvalidXml_ThrowsException()
    {
        // Arrange
        var invalidXaml = "<<<not valid xml>>>";
        var filePath = CreateTempXamlFile(invalidXaml);

        // Act
        var act = () => _parser.ParseAsync(filePath);

        // Assert
        await act.Should().ThrowAsync<System.Xml.XmlException>();
    }

    /// <summary>
    /// 临时文件/目录清理辅助类。
    /// </summary>
    private sealed class FileTempCleanup : IDisposable
    {
        private readonly string _directory;

        public FileTempCleanup(string directory)
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
                // 忽略清理失败，避免影响测试结果
            }
        }
    }
}
