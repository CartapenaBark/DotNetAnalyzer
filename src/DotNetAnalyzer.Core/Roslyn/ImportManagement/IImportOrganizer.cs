using Microsoft.CodeAnalysis;

namespace DotNetAnalyzer.Core.Roslyn.ImportManagement
{
    /// <summary>
    /// 导入整理器接口
    /// </summary>
    public interface IImportOrganizer
    {
        /// <summary>
        /// 整理文档中的 using 语句
        /// </summary>
        Task<ImportOrganizationResult> OrganizeImportsAsync(Document document, ImportOrganizeOptions options);
    }

    /// <summary>
    /// 导入整理结果
    /// </summary>
    public class ImportOrganizationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Document? OrganizedDocument { get; set; }
        public List<string> RemovedUsings { get; set; } = new();
        public int ChangesCount { get; set; }
        public string? FormattedCode { get; set; }
    }

    /// <summary>
    /// 导入整理选项
    /// </summary>
    public class ImportOrganizeOptions
    {
        public ImportSortStyle SortStyle { get; set; } = ImportSortStyle.SystemFirst;
        public bool RemoveUnused { get; set; } = true;
        public bool AddGroupSeparation { get; set; } = true;
    }

    /// <summary>
    /// 导入排序样式
    /// </summary>
    public enum ImportSortStyle
    {
        SystemFirst,    // System.* 命名空间优先
        Alphabetical,   // 按字母顺序
    }
}
