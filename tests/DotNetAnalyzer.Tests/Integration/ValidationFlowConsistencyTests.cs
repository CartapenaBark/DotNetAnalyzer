using System.Text.Json;
using FluentAssertions;
using DotNetAnalyzer.Tests.Helpers;
using Xunit;

namespace DotNetAnalyzer.Tests.Integration;

public class ValidationFlowConsistencyTests
{
    private readonly string _repoRoot = TestHelper.GetRepositoryRoot();

    [Fact]
    public void ScriptsWorkflowsAndDocs_ShouldMatchAuthoritativeValidationFlow()
    {
        var flow = JsonDocument.Parse(File.ReadAllText(Path.Combine(_repoRoot, "eng", "validation-flow.json"))).RootElement;
        var solutionPath = flow.GetProperty("solutionPath").GetString()!;
        var configuration = flow.GetProperty("configuration").GetString()!;
        var testFramework = flow.GetProperty("testFramework").GetString()!;
        var testFilter = flow.GetProperty("testFilter").GetString()!;
        var packageProject = flow.GetProperty("packageProject").GetString()!;
        var packageOutputDir = flow.GetProperty("packageOutputDir").GetString()!;

        var sh = File.ReadAllText(Path.Combine(_repoRoot, "scripts", "validate-ci-cd.sh"));
        sh.Should().Contain($"dotnet restore \"$SOLUTION\" -p:Configuration=\"$CONFIGURATION\" --verbosity minimal");
        sh.Should().Contain("dotnet build \"$SOLUTION\" -c \"$CONFIGURATION\" --no-restore --verbosity minimal");
        sh.Should().Contain("dotnet test \"$SOLUTION\" -c \"$CONFIGURATION\" --framework \"$TARGET_FRAMEWORK\" --no-build --verbosity normal --filter \"$TEST_FILTER\"");
        sh.Should().Contain("OUTPUT_DIR=\"./Bin/nupkg\"");

        var ps1 = File.ReadAllText(Path.Combine(_repoRoot, "scripts", "validate-ci-cd.ps1"));
        ps1.Should().Contain("dotnet restore $SOLUTION -p:Configuration=$CONFIGURATION --verbosity minimal");
        ps1.Should().Contain("dotnet test $SOLUTION -c $CONFIGURATION --framework $TARGET_FRAMEWORK --no-build --verbosity normal --filter $TEST_FILTER");
        ps1.Should().Contain("$OUTPUT_DIR = \".\\Bin\\nupkg\"");

        var bat = File.ReadAllText(Path.Combine(_repoRoot, "scripts", "validate-ci-cd.bat"));
        bat.Should().Contain("call dotnet restore DotNetAnalyzer.slnx -p:Configuration=%CONFIGURATION% --verbosity minimal");
        bat.Should().Contain("call dotnet build DotNetAnalyzer.slnx -c %CONFIGURATION% --no-restore --verbosity minimal");
        bat.Should().Contain("call dotnet test DotNetAnalyzer.slnx -c %CONFIGURATION% --framework %TARGET_FRAMEWORK% --no-build --verbosity normal --filter \"%TEST_FILTER%\"");
        bat.Should().Contain("set \"OUTPUT_DIR=Bin\\nupkg\"");

        var buildAndTest = File.ReadAllText(Path.Combine(_repoRoot, ".github", "workflows", "build-and-test.yml"));
        buildAndTest.Should().Contain($"dotnet restore {solutionPath} -p:Configuration={configuration} --verbosity minimal");
        buildAndTest.Should().Contain($"dotnet build {solutionPath} -c {configuration} --no-restore --verbosity minimal");
        buildAndTest.Should().Contain($"dotnet test {solutionPath} -c {configuration} --framework {testFramework} --no-build --verbosity normal --filter \"{testFilter}\"");

        var buildAndPublish = File.ReadAllText(Path.Combine(_repoRoot, ".github", "workflows", "build-and-publish.yml"));
        buildAndPublish.Should().Contain($"dotnet restore {solutionPath} -p:Configuration={configuration} --verbosity minimal");
        buildAndPublish.Should().Contain($"dotnet build {solutionPath} -c {configuration} --no-restore --verbosity minimal -p:Version=${{{{ needs.extract_version.outputs.version }}}}");
        buildAndPublish.Should().Contain($"dotnet test {solutionPath} -c {configuration} --framework {testFramework} --no-build --verbosity normal --filter \"{testFilter}\"");

        var workflowDoc = File.ReadAllText(Path.Combine(_repoRoot, "docs", "development-workflow.md"));
        workflowDoc.Should().Contain($"dotnet restore {solutionPath} -p:Configuration={configuration} --verbosity minimal");
        workflowDoc.Should().Contain($"dotnet build {solutionPath} -c {configuration} --no-restore --verbosity minimal");
        workflowDoc.Should().Contain($"dotnet test {solutionPath} -c {configuration} --framework {testFramework} --no-build --verbosity normal --filter \"{testFilter}\"");
        workflowDoc.Should().Contain($"dotnet pack {packageProject} -c {configuration} --no-build --output ./{packageOutputDir}");

        var contributing = File.ReadAllText(Path.Combine(_repoRoot, "CONTRIBUTING.md"));
        contributing.Should().Contain("bash scripts/validate-ci-cd.sh");
        contributing.Should().Contain($"dotnet test {solutionPath} -c {configuration} --framework {testFramework} --no-build --verbosity normal --filter \"{testFilter}\"");
    }
}
