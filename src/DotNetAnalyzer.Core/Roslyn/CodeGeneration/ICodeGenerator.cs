using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace DotNetAnalyzer.Core.Roslyn.CodeGeneration
{
    /// <summary>
    /// 代码生成器接口
    /// </summary>
    public interface ICodeGenerator
    {
        /// <summary>
        /// 生成代码并返回修改后的文档
        /// </summary>
        Task<GenerationResult> GenerateAsync(Document document, GenerationOptions options);

        /// <summary>
        /// 预览生成的代码（不修改文档）
        /// </summary>
        Task<string> PreviewAsync(Document document, GenerationOptions options);
    }

    /// <summary>
    /// 代码生成结果
    /// </summary>
    public class GenerationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Document? ModifiedDocument { get; set; }
        public string? GeneratedCode { get; set; }
        public int? InsertLine { get; set; }
        public List<string> GeneratedMembers { get; set; } = new();
    }

    /// <summary>
    /// 代码生成选项
    /// </summary>
    public class GenerationOptions
    {
        public string? ClassName { get; set; }
        public string? MemberName { get; set; }
        public string? InterfaceName { get; set; }
        public bool GenerateStub { get; set; } = true;
        public bool AddToDoComments { get; set; } = true;
    }
}
