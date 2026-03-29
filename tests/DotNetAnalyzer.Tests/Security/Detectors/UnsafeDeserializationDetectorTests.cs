using DotNetAnalyzer.Core.Security.Detectors;
using DotNetAnalyzer.Core.Security.Models;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Security.Detectors;

public class UnsafeDeserializationDetectorTests : SecurityDetectorTestBase
{
    private readonly UnsafeDeserializationDetector _detector = new();

    [Fact]
    public async Task DetectAsync_BinaryFormatter_ShouldDetect()
    {
        var source = """
            using System;
            using System.IO;
            using System.Runtime.Serialization.Formatters.Binary;
            class Test {
                object Deserialize(Stream stream) {
                    var formatter = new BinaryFormatter();
                    return formatter.Deserialize(stream);
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC004");
    }

    [Fact]
    public async Task DetectAsync_SoapFormatter_ShouldDetect()
    {
        var source = """
            using System;
            using System.IO;
            using System.Runtime.Serialization.Formatters.Soap;
            class Test {
                object Deserialize(Stream stream) {
                    var formatter = new SoapFormatter();
                    return formatter.Deserialize(stream);
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC004");
    }

    [Fact]
    public async Task DetectAsync_NetDataContractSerializer_ShouldDetect()
    {
        var source = """
            using System;
            using System.IO;
            using System.Runtime.Serialization;
            class Test {
                T Deserialize<T>(Stream stream) {
                    var serializer = new NetDataContractSerializer();
                    return (T)serializer.ReadObject(stream);
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC004");
    }

    [Fact]
    public async Task DetectAsync_JsonSerializer_ShouldNotDetect()
    {
        var source = """
            using System;
            using System.IO;
            using System.Text.Json;
            class Test {
                T Deserialize<T>(Stream stream) {
                    return JsonSerializer.Deserialize<T>(stream)!;
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertNoFinding(findings, "SEC004");
    }

    [Fact]
    public async Task DetectAsync_Properties_ShouldBeSet()
    {
        _detector.RuleId.Should().Be("SEC004");
        _detector.Name.Should().Be("unsafe-deserialization");
        _detector.OwaspCategory.Should().Be("A08:2021");
        _detector.CweId.Should().Be("CWE-502");
    }
}
