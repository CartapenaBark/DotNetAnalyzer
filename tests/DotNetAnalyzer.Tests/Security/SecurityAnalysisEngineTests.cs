using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.Security.Detectors;
using DotNetAnalyzer.Core.Security.Models;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetAnalyzer.Tests.Security;

public class SecurityAnalysisEngineTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    private static SecurityAnalysisEngine CreateEngine()
    {
        return new SecurityAnalysisEngine(
            NullLoggerFactory.Instance.CreateLogger<SecurityAnalysisEngine>(),
            new ISecurityDetector[]
            {
                new HardcodedCredentialDetector(),
                new SqlInjectionDetector(),
                new CommandInjectionDetector(),
                new UnsafeDeserializationDetector(),
                new PathTraversalDetector(),
                new XssInAspNetDetector()
            });
    }

    /// <summary>
    /// 创建包含运行时引用的 Document，使安全检测器能正确解析类型信息。
    /// </summary>
    private static async Task<(AdhocWorkspace Workspace, Document Document)> CreateTestDocumentWithWorkspace(string source)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.IO.Stream).Assembly.Location),
        };

        var processAssembly = typeof(System.Diagnostics.Process).Assembly.Location;
        if (!string.IsNullOrEmpty(processAssembly) && File.Exists(processAssembly))
        {
            references.Add(MetadataReference.CreateFromFile(processAssembly));
        }

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            metadataReferences: references);
        workspace.AddProject(projectInfo);

        workspace.AddDocument(
            DocumentInfo.Create(documentId, "Test.cs",
                loader: TextLoader.From(
                    TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())),
                filePath: "/Test.cs"));

        var document = workspace.CurrentSolution.GetDocument(documentId)!;

        // 触发编译以确保 SemanticModel 可用
        _ = await document.Project.GetCompilationAsync().ConfigureAwait(false);

        return (workspace, document);
    }

    public void Dispose()
    {
        foreach (var d in _disposables)
        {
            d.Dispose();
        }
        _disposables.Clear();
    }

    [Fact]
    public void GetRules_ShouldReturnSixRules()
    {
        var engine = CreateEngine();
        var rules = engine.GetRules();

        rules.Should().HaveCount(6);
        rules.Select(r => r.RuleId).Should().Contain(
            "SEC001", "SEC002", "SEC003", "SEC004", "SEC005", "SEC006");
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_HardcodedPassword_ShouldDetect()
    {
        var engine = CreateEngine();
        var source = """
            using System;
            class Test {
                void Method() {
                    string password = "secret123";
                }
            }
            """;

        var (workspace, document) = await CreateTestDocumentWithWorkspace(source);
        _disposables.Add(workspace);
        var findings = await engine.AnalyzeDocumentAsync(document);

        findings.Should().Contain(f => f.RuleId == "SEC001");
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_MultipleVulnerabilities_ShouldDetectAll()
    {
        var engine = CreateEngine();
        var source = """
            using System;
            using System.Diagnostics;
            using System.IO;
            using System.Runtime.Serialization.Formatters.Binary;
            class Test {
                void Method(string userId, string fileName, Stream stream) {
                    string password = "admin123";
                    string sql = "SELECT * FROM users WHERE id = " + userId;
                    Process.Start("cmd", "/c " + userId);
                    var formatter = new BinaryFormatter();
                    formatter.Deserialize(stream);
                    File.ReadAllBytes(Path.Combine("/uploads", fileName));
                }
            }
            """;

        var (workspace, document) = await CreateTestDocumentWithWorkspace(source);
        _disposables.Add(workspace);
        var findings = await engine.AnalyzeDocumentAsync(document);

        findings.Should().Contain(f => f.RuleId == "SEC001");
        findings.Should().Contain(f => f.RuleId == "SEC002");
        findings.Should().Contain(f => f.RuleId == "SEC003");
        findings.Should().Contain(f => f.RuleId == "SEC004");
        findings.Should().Contain(f => f.RuleId == "SEC005");
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_ExcludedRule_ShouldSkip()
    {
        var engine = CreateEngine();
        var options = new SecurityAnalysisOptions
        {
            ExcludedRules = ["SEC001"]
        };

        var source = """
            using System;
            class Test {
                void Method() {
                    string password = "admin123";
                }
            }
            """;

        var (workspace, document) = await CreateTestDocumentWithWorkspace(source);
        _disposables.Add(workspace);
        var findings = await engine.AnalyzeDocumentAsync(document, options);

        findings.Should().NotContain(f => f.RuleId == "SEC001");
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_HighSeverityFilter_ShouldFilterLow()
    {
        var engine = CreateEngine();
        var options = new SecurityAnalysisOptions
        {
            MinSeverity = SecuritySeverity.Critical
        };

        var source = """
            using System;
            using System.IO;
            class Test {
                void Method(string userFile) {
                    var path = Path.Combine("/uploads", userFile);
                    File.ReadAllBytes(path);
                }
            }
            """;

        var (workspace, document) = await CreateTestDocumentWithWorkspace(source);
        _disposables.Add(workspace);
        var findings = await engine.AnalyzeDocumentAsync(document, options);

        // SEC005 is High severity, should be filtered out by Critical threshold
        findings.Should().BeEmpty();
    }
}
