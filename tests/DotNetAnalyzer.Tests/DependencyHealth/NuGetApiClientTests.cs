using System.Net;
using System.Text.Json;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.DependencyHealth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetAnalyzer.Tests.DependencyHealth;

public class NuGetApiClientTests : IDisposable
{
    private readonly Mock<ILogger<NuGetApiClient>> _loggerMock = new();
    private readonly MockHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly DependencyHealthOptions _options;

    public NuGetApiClientTests()
    {
        _handler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_handler)
        { BaseAddress = new Uri("https://api.nuget.org") };

        _options = new DependencyHealthOptions
        {
            ApiTimeout = 30,
            NuGetApiUrl = "https://api.nuget.org/v3/index.json"
        };
    }

    public void Dispose()
    {
        _handler.Dispose();
        _httpClient.Dispose();
    }

    private NuGetApiClient CreateClient()
    {
        return new NuGetApiClient(
            _httpClient,
            Options.Create(_options),
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetLatestVersionAsync_WithValidResponse_ReturnsPackageInfo()
    {
        // Arrange
        var serviceIndex = new
        {
            version = "3.0.0",
            resources = new[]
            {
                new { id = "https://api.nuget.org/v3/search-query", type = "SearchQueryService" }
            }
        };

        var searchResponse = new
        {
            totalHits = 1,
            data = new[]
            {
                new
                {
                    id = "TestPackage",
                    version = "2.0.0",
                    versions = new[] { new { version = "2.0.0" } }
                }
            }
        };

        _handler.Setup("https://api.nuget.org/v3/index.json", HttpStatusCode.OK, JsonSerializer.Serialize(serviceIndex));
        _handler.Setup("https://api.nuget.org/v3/search-query*", HttpStatusCode.OK, JsonSerializer.Serialize(searchResponse));

        var client = CreateClient();

        // Act
        var result = await client.GetLatestVersionAsync("TestPackage");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestPackage", result.PackageId);
        Assert.Equal("2.0.0", result.LatestStableVersion);
    }

    [Fact]
    public async Task GetLatestVersionAsync_WhenPackageNotFound_ReturnsNull()
    {
        // Arrange
        var serviceIndex = new
        {
            version = "3.0.0",
            resources = new[]
            {
                new { id = "https://api.nuget.org/v3/search-query", type = "SearchQueryService" }
            }
        };

        var emptySearch = new { totalHits = 0, data = Array.Empty<object>() };

        _handler.Setup("https://api.nuget.org/v3/index.json", HttpStatusCode.OK, JsonSerializer.Serialize(serviceIndex));
        _handler.Setup("https://api.nuget.org/v3/search-query*", HttpStatusCode.OK, JsonSerializer.Serialize(emptySearch));

        var client = CreateClient();

        // Act
        var result = await client.GetLatestVersionAsync("NonExistentPackage");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestVersionAsync_WhenApiFails_ReturnsNull()
    {
        // Arrange
        _handler.Setup("*", HttpStatusCode.InternalServerError, "Server Error");

        var client = CreateClient();

        // Act
        var result = await client.GetLatestVersionAsync("TestPackage");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetVulnerabilitiesAsync_WithNoVulnerabilities_ReturnsEmptyList()
    {
        // Arrange
        var registrationLeaf = new
        {
            licenseUrl = (string?)null,
            licenseExpression = (string?)null,
            vulnerability = (object?)null
        };

        _handler.Setup(
            "https://api.nuget.org/v3/registration-semver2/testpackage/1.0.0.json",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(registrationLeaf));

        var client = CreateClient();

        // Act
        var result = await client.GetVulnerabilitiesAsync("TestPackage", "1.0.0");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetVulnerabilitiesAsync_WhenApiFails_ReturnsEmptyList()
    {
        // Arrange
        _handler.Setup("*", HttpStatusCode.InternalServerError, "Server Error");

        var client = CreateClient();

        // Act
        var result = await client.GetVulnerabilitiesAsync("TestPackage", "1.0.0");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLicenseInfoAsync_WithValidLicense_ReturnsLicenseInfo()
    {
        // Arrange
        var registrationLeaf = new
        {
            licenseExpression = "MIT",
            licenseUrl = (string?)null,
            vulnerability = (object?)null
        };

        _handler.Setup(
            "https://api.nuget.org/v3/registration-semver2/testpackage/1.0.0.json",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(registrationLeaf));

        var client = CreateClient();

        // Act
        var result = await client.GetLicenseInfoAsync("TestPackage", "1.0.0");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MIT", result.LicenseType);
        Assert.Equal("MIT", result.LicenseExpression);
    }

    [Fact]
    public async Task GetLicenseInfoAsync_WhenApiFails_ReturnsNull()
    {
        // Arrange
        _handler.Setup("*", HttpStatusCode.InternalServerError, "Server Error");

        var client = CreateClient();

        // Act
        var result = await client.GetLicenseInfoAsync("TestPackage", "1.0.0");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLicenseInfoAsync_WithLicenseUrl_ExtractsLicenseType()
    {
        // Arrange
        var registrationLeaf = new
        {
            licenseUrl = "https://licenses.nuget.org/MIT",
            licenseExpression = (string?)null,
            vulnerability = (object?)null
        };

        _handler.Setup(
            "https://api.nuget.org/v3/registration-semver2/testpackage/1.0.0.json",
            HttpStatusCode.OK,
            JsonSerializer.Serialize(registrationLeaf));

        var client = CreateClient();

        // Act
        var result = await client.GetLicenseInfoAsync("TestPackage", "1.0.0");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MIT", result.LicenseType);
    }

    /// <summary>
    /// 用于测试的 Mock HttpMessageHandler
    /// </summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<(string Pattern, Func<HttpRequestMessage, Task<HttpResponseMessage>> Handler)>
            _handlers = [];

        public void Setup(string urlPattern, HttpStatusCode statusCode, string content)
        {
            _handlers.Add((
                urlPattern,
                _ => Task.FromResult(new HttpResponseMessage(statusCode)
                { Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json") })
            ));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";
            foreach (var (pattern, handler) in _handlers)
            {
                if (pattern.EndsWith('*'))
                {
                    if (url.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase))
                        return handler(request);
                }
                else if (string.Equals(url, pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return handler(request);
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            { Content = new StringContent("Not Found") });
        }
    }
}
