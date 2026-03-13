using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using DotNetAnalyzer.Core.Analysis.CodeQuality;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Tests.Analysis.CodeQuality;

/// <summary>
/// 代码异味检测器测试基类
/// </summary>
/// <remarks>
/// 提供测试辅助方法，减少重复代码。
/// </remarks>
public abstract class CodeSmellDetectorTestBase
{
    /// <summary>
    /// 从源代码创建文档
    /// </summary>
    protected async Task<Document> CreateDocumentAsync(string sourceCode, string fileName = "Test.cs")
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp);

        workspace.AddProject(projectInfo);

        var documentInfo = DocumentInfo.Create(
            documentId,
            fileName,
            filePath: $"/{fileName}",
            sourceCodeKind: SourceCodeKind.Regular,
            loader: TextLoader.From(TextAndVersion.Create(
                Microsoft.CodeAnalysis.Text.SourceText.From(sourceCode),
                VersionStamp.Create())));

        workspace.AddDocument(documentInfo);

        return workspace.CurrentSolution.GetDocument(documentId)!;
    }

    /// <summary>
    /// 断言代码异味
    /// </summary>
    protected static void AssertCodeSmell(
        CodeSmell smell,
        string expectedType,
        CodeSmellSeverity expectedSeverity,
        string? expectedSymbolName = null)
    {
        Assert.Equal(expectedType, smell.Type);
        Assert.True(smell.Severity >= expectedSeverity,
            $"预期严重程度至少为 {expectedSeverity}，实际为 {smell.Severity}");

        if (expectedSymbolName != null)
        {
            Assert.Equal(expectedSymbolName, smell.SymbolName);
        }
    }

    /// <summary>
    /// 断言代码位置
    /// </summary>
    protected static void AssertLocation(
        CodeLocation location,
        int expectedStartLine,
        int expectedStartColumn,
        int expectedEndLine,
        int expectedEndColumn)
    {
        Assert.Equal(expectedStartLine, location.StartLine);
        Assert.Equal(expectedStartColumn, location.StartColumn);
        Assert.Equal(expectedEndLine, location.EndLine);
        Assert.Equal(expectedEndColumn, location.EndColumn);
    }

    /// <summary>
    /// 创建测试用的代码分析选项
    /// </summary>
    protected static CodeAnalysisOptions CreateAnalysisOptions(
        CodeSmellSeverity minSeverity = CodeSmellSeverity.Minor,
        bool includeSuggestions = true,
        bool enableDeepAnalysis = false)
    {
        return new CodeAnalysisOptions
        {
            MinSeverity = minSeverity,
            IncludeSuggestions = includeSuggestions,
            EnableDeepAnalysis = enableDeepAnalysis
        };
    }
}

/// <summary>
/// 元数据引用辅助类
/// </summary>
internal static class MetadataReferences
{
    public static MetadataReference Corlib { get; } =
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

    public static MetadataReference SystemRuntime { get; } =
        MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location);

    public static MetadataReference SystemCollections { get; } =
        MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location);

    public static MetadataReference SystemLinq { get; } =
        MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location);

    public static MetadataReference MicrosoftCodeAnalysis { get; } =
        MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location);
}

/// <summary>
/// 测试辅助方法
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// 计算代码行数
    /// </summary>
    public static int CountLines(string sourceCode)
    {
        return sourceCode.Split('\n').Length;
    }

    /// <summary>
    /// 计算方法中的语句数
    /// </summary>
    public static int CountStatements(string methodBody)
    {
        var semicolons = methodBody.Count(c => c == ';');
        var braces = methodBody.Count(c => c == '{');
        return semicolons + braces;
    }

    /// <summary>
    /// 提取方法参数数量
    /// </summary>
    public static int CountParameters(string methodSignature)
    {
        var start = methodSignature.IndexOf('(');
        var end = methodSignature.IndexOf(')');
        if (start < 0 || end < 0) return 0;

        var parameters = methodSignature.Substring(start + 1, end - start - 1);
        if (string.IsNullOrWhiteSpace(parameters)) return 0;

        return parameters.Split(',').Length;
    }

    /// <summary>
    /// 生成测试用的长方法
    /// </summary>
    public static string GenerateLongMethod(int lineCount)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("public void LongMethod()");
        builder.AppendLine("{");
        for (int i = 0; i < lineCount - 2; i++)
        {
            builder.AppendLine($"    var line{i} = {i};");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>
    /// 生成测试用的大类
    /// </summary>
    public static string GenerateLargeClass(int memberCount)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("public class LargeClass");
        builder.AppendLine("{");
        for (int i = 0; i < memberCount; i++)
        {
            builder.AppendLine($"    public int Property{i} {{ get; set; }}");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }
}
