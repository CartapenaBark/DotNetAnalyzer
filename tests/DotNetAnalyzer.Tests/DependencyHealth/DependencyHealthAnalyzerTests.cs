using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.DependencyHealth;
using DotNetAnalyzer.Core.DependencyHealth.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetAnalyzer.Tests.DependencyHealth;

public class DependencyHealthAnalyzerTests
{
    private readonly Mock<INuGetClient> _nuGetClientMock = new();
    private readonly Mock<ILogger<ProjectFileDependencyExtractor>> _extractorLoggerMock = new();
    private readonly Mock<ILogger<NuGetAssetsFileParser>> _parserLoggerMock = new();
    private readonly Mock<ILogger<DependencyHealthAnalyzer>> _loggerMock = new();
    private readonly DependencyHealthOptions _options;

    public DependencyHealthAnalyzerTests()
    {
        _options = new DependencyHealthOptions
        {
            ConcurrentApiCalls = 2,
            ApiTimeout = 30,
            AllowedLicenses = []
        };
    }

    private DependencyHealthAnalyzer CreateAnalyzer(
        Action<Mock<INuGetClient>>? setupClient = null)
    {
        _nuGetClientMock.Reset();

        // 先设置默认返回值
        _nuGetClientMock.Setup(c => c.GetLatestVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string pkgId, CancellationToken _) => new PackageVersionInfo
            {
                PackageId = pkgId,
                CurrentVersion = "1.0.0",
                LatestStableVersion = "2.0.0"
            });

        _nuGetClientMock.Setup(c => c.GetVulnerabilitiesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PackageVulnerability>);

        _nuGetClientMock.Setup(c => c.GetLicenseInfoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string pkgId, string ver, CancellationToken _) => new PackageLicenseInfo
            {
                PackageId = pkgId,
                Version = ver,
                LicenseType = "MIT",
                IsAllowed = true
            });

        // 再调用自定义设置（会覆盖默认值）
        setupClient?.Invoke(_nuGetClientMock);

        return new DependencyHealthAnalyzer(
            _nuGetClientMock.Object,
            new ProjectFileDependencyExtractor(_extractorLoggerMock.Object),
            new NuGetAssetsFileParser(_parserLoggerMock.Object),
            Options.Create(_options),
            _loggerMock.Object);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNonexistentFile_ReturnsEmptyReport()
    {
        // Arrange
        var analyzer = CreateAnalyzer();

        // Act
        var report = await analyzer.AnalyzeAsync("/nonexistent/project.csproj");

        // Assert
        report.ProjectPath.Should().Be("/nonexistent/project.csproj");
        report.Packages.Should().BeEmpty();
        report.Vulnerabilities.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidProject_ReturnsReport()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "DotNetAnalyzer_Tests_DHA_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var csproj = Path.Combine(tempDir, "Test.csproj");
            await File.WriteAllTextAsync(csproj, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                  </ItemGroup>
                </Project>
                """);

            var analyzer = CreateAnalyzer();

            // Act
            var report = await analyzer.AnalyzeAsync(csproj);

            // Assert
            report.ProjectPath.Should().Be(csproj);
            report.Packages.Should().HaveCount(1);
            report.Packages[0].PackageId.Should().Be("Newtonsoft.Json");
            report.Packages[0].CurrentVersion.Should().Be("13.0.3");
            report.Packages[0].LatestStableVersion.Should().Be("2.0.0");
            report.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task AnalyzeAsync_WithVulnerabilities_ReturnsVulnerabilities()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "DotNetAnalyzer_Tests_DHA2_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var csproj = Path.Combine(tempDir, "Test.csproj");
            await File.WriteAllTextAsync(csproj, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="VulnPackage" Version="1.0.0" />
                  </ItemGroup>
                </Project>
                """);

            var analyzer = CreateAnalyzer(client =>
            {
                client.Setup(c => c.GetVulnerabilitiesAsync(
                        "VulnPackage", "1.0.0", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<PackageVulnerability>
                    {
                        new() { PackageId = "VulnPackage", AffectedVersion = "1.0.0", CveId = "CVE-2024-1234", Severity = "High" }
                    });
            });

            // Act
            var report = await analyzer.AnalyzeAsync(csproj);

            // Assert
            report.Vulnerabilities.Should().HaveCount(1);
            report.Summary.VulnerablePackages.Should().Be(1);
            report.Summary.TotalVulnerabilities.Should().Be(1);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void DependencyHealthSummary_DefaultValues_AreZero()
    {
        var summary = new DependencyHealthSummary();

        summary.TotalPackages.Should().Be(0);
        summary.OutdatedPackages.Should().Be(0);
        summary.DeprecatedPackages.Should().Be(0);
        summary.PrereleasePackages.Should().Be(0);
        summary.VulnerablePackages.Should().Be(0);
        summary.TotalVulnerabilities.Should().Be(0);
        summary.LicenseViolations.Should().Be(0);
    }
}
