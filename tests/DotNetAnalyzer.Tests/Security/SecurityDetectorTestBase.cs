using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.Security.Models;

namespace DotNetAnalyzer.Tests.Security;

/// <summary>
/// 安全检测器单元测试基类，提供通用测试辅助方法。
/// 使用 AdhocWorkspace 创建 Document，添加运行时引用以支持语义分析。
/// </summary>
public abstract class SecurityDetectorTestBase
{
    /// <summary>
    /// 创建包含运行时引用的 MetadataReference 列表。
    /// </summary>
    private static List<MetadataReference> CreateMetadataReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.IO.Stream).Assembly.Location),
        };

        // Process 可能在单独的程序集中
        var processAssembly = typeof(System.Diagnostics.Process).Assembly.Location;
        if (!string.IsNullOrEmpty(processAssembly) && File.Exists(processAssembly))
        {
            references.Add(MetadataReference.CreateFromFile(processAssembly));
        }

        return references;
    }

    /// <summary>
    /// 创建一个 AdhocWorkspace，包含必要的运行时引用以支持语义分析。
    /// 调用者负责 Dispose 工作区。
    /// </summary>
    protected static AdhocWorkspace CreateWorkspace()
    {
        var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            metadataReferences: CreateMetadataReferences());
        workspace.AddProject(projectInfo);
        return workspace;
    }

    /// <summary>
    /// 通过编译创建 Document，包含必要的运行时引用以支持语义分析。
    /// 调用者负责 Dispose 工作区。
    /// </summary>
    protected static async Task<(AdhocWorkspace Workspace, Document Document)> CreateDocumentWithWorkspaceAsync(string source)
    {
        var workspace = CreateWorkspace();
        var project = workspace.CurrentSolution.Projects.First();
        var documentId = DocumentId.CreateNewId(project.Id);

        workspace.AddDocument(
            DocumentInfo.Create(documentId, "Test.cs",
                loader: TextLoader.From(
                    TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())),
                filePath: "/Test.cs"));

        var doc = workspace.CurrentSolution.GetDocument(documentId)
            ?? throw new InvalidOperationException("Document not found in workspace");

        // 触发编译以确保 SemanticModel 可用
        var compilation = await doc.Project.GetCompilationAsync().ConfigureAwait(false);
        _ = compilation ?? throw new InvalidOperationException("Compilation returned null");

        return (workspace, doc);
    }

    /// <summary>
    /// 使用指定源代码创建 Document（简单方式）。
    /// 调用者负责 Dispose 工作区。
    /// </summary>
    protected static async Task<Document> CreateDocumentFromSourceAsync(string source)
    {
        var (workspace, document) = await CreateDocumentWithWorkspaceAsync(source).ConfigureAwait(false);
        // 注意：调用者无法 Dispose workspace，仅用于短生命周期场景
        return document;
    }

    /// <summary>
    /// 执行检测并返回发现列表。
    /// Workspace 在检测期间保持存活，避免 GC 导致 NullReferenceException。
    /// </summary>
    protected static async Task<IReadOnlyList<SecurityFinding>> DetectAsync(
        ISecurityDetector detector,
        string source,
        SecurityAnalysisOptions? options = null)
    {
        var (workspace, document) = await CreateDocumentWithWorkspaceAsync(source).ConfigureAwait(false);
        try
        {
            return await detector.DetectAsync(document, options).ConfigureAwait(false);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    /// <summary>
    /// 断言发现包含指定规则 ID
    /// </summary>
    protected static void AssertHasFinding(
        IReadOnlyList<SecurityFinding> findings,
        string ruleId,
        int expectedCount = -1)
    {
        var matches = findings.Where(f => f.RuleId == ruleId).ToList();

        Assert.NotEmpty(matches);

        if (expectedCount >= 0)
        {
            Assert.Equal(expectedCount, matches.Count);
        }
    }

    /// <summary>
    /// 断言发现不包含指定规则 ID
    /// </summary>
    protected static void AssertNoFinding(
        IReadOnlyList<SecurityFinding> findings,
        string ruleId)
    {
        Assert.Empty(findings.Where(f => f.RuleId == ruleId));
    }
}
