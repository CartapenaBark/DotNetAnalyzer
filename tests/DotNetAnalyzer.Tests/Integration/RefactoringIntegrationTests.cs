using System.Text.Json;
using FluentAssertions;
using DotNetAnalyzer.Cli.Tools;
using DotNetAnalyzer.Tests.Helpers;
using Xunit;

namespace DotNetAnalyzer.Tests.Integration;

public class RefactoringIntegrationTests
{
    [Fact]
    public async Task RenameSymbol_ShouldResolveTargetDocumentFromProjectPathAndReturnPreview()
    {
        var source = """
namespace Sample;

public class Calculator
{
    public int Add(int left, int right)
    {
        return left + right;
    }
}

public static class CalculatorFactory
{
    public static Calculator Create()
    {
        return new Calculator();
    }
}
""";

        using var tempProject = TempProject.Create(source);
        using var workspaceManager = TestHelper.CreateWorkspaceManager();

        var response = await RefactoringTools.RenameSymbol(
            workspaceManager,
            tempProject.ProjectPath,
            tempProject.SourceFilePath,
            line: 2,
            column: 13,
            newName: "MathCalculator",
            applyChanges: false);

        using var document = JsonDocument.Parse(response);
        document.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("isPreview").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("affectedFileCount").GetInt32().Should().BeGreaterThan(0);
        document.RootElement.GetProperty("fileChanges")[0].GetProperty("filePath").GetString().Should().Be(tempProject.SourceFilePath);
    }

    private sealed class TempProject : IDisposable
    {
        private TempProject(string directory, string projectPath, string sourceFilePath)
        {
            DirectoryPath = directory;
            ProjectPath = projectPath;
            SourceFilePath = sourceFilePath;
        }

        public string DirectoryPath { get; }
        public string ProjectPath { get; }
        public string SourceFilePath { get; }

        public static TempProject Create(string sourceCode)
        {
            var directory = Path.Combine(Path.GetTempPath(), "dotnet-analyzer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            var projectPath = Path.Combine(directory, "SampleProject.csproj");
            File.WriteAllText(projectPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""");

            var sourceFilePath = Path.Combine(directory, "Calculator.cs");
            File.WriteAllText(sourceFilePath, sourceCode);

            return new TempProject(directory, projectPath, sourceFilePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
