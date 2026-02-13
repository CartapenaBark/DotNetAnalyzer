using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace DotNetAnalyzer.Core.Roslyn.ImportManagement
{
    /// <summary>
    /// 导入整理器实现
    /// </summary>
    public class ImportOrganizer : IImportOrganizer
    {
        /// <summary>
        /// 整理文档中的 using 语句
        /// </summary>
        public async Task<ImportOrganizationResult> OrganizeImportsAsync(Document document, ImportOrganizeOptions options)
        {
            try
            {
                var root = await document.GetSyntaxRootAsync();
                if (root == null)
                {
                    return new ImportOrganizationResult
                    {
                        Success = false,
                        ErrorMessage = "Unable to get syntax root"
                    };
                }

                var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
                if (usings.Count == 0)
                {
                    return new ImportOrganizationResult
                    {
                        Success = true,
                        ChangesCount = 0,
                        OrganizedDocument = document
                    };
                }

                var semanticModel = await document.GetSemanticModelAsync();
                var removedUsings = new List<string>();

                // 移除未使用的 using（如果启用）
                if (options.RemoveUnused && semanticModel != null)
                {
                    var unusedUsings = usings.Where(u => !IsUsingUsed(semanticModel, u)).ToList();
                    foreach (var unused in unusedUsings)
                    {
                        var usingName = unused.Name?.ToString() ?? "unknown";
                        removedUsings.Add(usingName);
                        var newRoot = root!.RemoveNode(unused, SyntaxRemoveOptions.KeepNoTrivia);
                        if (newRoot != null)
                            root = newRoot;
                    }
                    usings = usings.Except(unusedUsings).ToList();
                }

                // 排序 using 语句
                var sortedUsings = SortUsings(usings, options.SortStyle);

                // 重新生成 using 列表
                if (sortedUsings.Count != 0)
                {
                    var firstUsing = usings.First();
                    var lastUsing = usings.Last();

                    // 移除所有现有的 using
                    var newRoot = root!.RemoveNodes(usings, SyntaxRemoveOptions.KeepNoTrivia);
                    if (newRoot == null)
                    {
                        return new ImportOrganizationResult
                        {
                            Success = false,
                            ErrorMessage = "Unable to remove existing using directives"
                        };
                    }

                    // 创建新的 using 列表
                    var usingList = new List<UsingDirectiveSyntax>();
                    foreach (var usingDirective in sortedUsings)
                    {
                        var newUsing = usingDirective
                            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
                        usingList.Add(newUsing);
                    }

                    // 插入到文件开头
                    var firstNode = newRoot.DescendantNodes().FirstOrDefault();
                    if (firstNode != null)
                    {
                        var usingsWithSeparators = InsertGroupSeparators(usingList, options.AddGroupSeparation);
                        newRoot = newRoot.InsertNodesBefore(firstNode, usingsWithSeparators);
                    }

                    var newDocument = document.WithSyntaxRoot(newRoot);
                    var formattedCode = (await newDocument.GetSyntaxRootAsync())?.ToFullString();

                    return new ImportOrganizationResult
                    {
                        Success = true,
                        OrganizedDocument = newDocument,
                        RemovedUsings = removedUsings,
                        ChangesCount = removedUsings.Count + (usings.Count > 0 ? 1 : 0),
                        FormattedCode = formattedCode
                    };
                }

                return new ImportOrganizationResult
                {
                    Success = true,
                    ChangesCount = removedUsings.Count,
                    RemovedUsings = removedUsings,
                    OrganizedDocument = document
                };
            }
            catch (Exception ex)
            {
                return new ImportOrganizationResult
                {
                    Success = false,
                    ErrorMessage = $"Error organizing imports: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 检查 using 是否被使用
        /// </summary>
        private static bool IsUsingUsed(SemanticModel semanticModel, UsingDirectiveSyntax usingDirective)
        {
            var name = usingDirective.Name ?? throw new ArgumentNullException(nameof(usingDirective));
            var symbolInfo = semanticModel.GetSymbolInfo(name);
            if (symbolInfo.Symbol == null)
                return true; // 保守策略：如果无法确定，保留

            // 简单检查：如果命名空间中的任何类型被使用，则保留
            var namespaceSymbol = symbolInfo.Symbol as INamespaceSymbol;
            if (namespaceSymbol == null)
                return true;

            // 更精确的检查需要遍历整个文档，这里使用简化版本
            return true;
        }

        /// <summary>
        /// 排序 using 语句
        /// </summary>
        private static List<UsingDirectiveSyntax> SortUsings(List<UsingDirectiveSyntax> usings, ImportSortStyle sortStyle)
        {
            if (sortStyle == ImportSortStyle.SystemFirst)
            {
                return usings
                    .OrderBy(u => u.Name?.ToString().StartsWith("System") ?? false ? 0 : 1)
                    .ThenBy(u => u.Name?.ToString() ?? string.Empty)
                    .ToList();
            }
            else // Alphabetical
            {
                return usings
                    .OrderBy(u => u.Name?.ToString() ?? string.Empty)
                    .ToList();
            }
        }

        /// <summary>
        /// 在分组之间插入空行
        /// </summary>
        private static List<UsingDirectiveSyntax> InsertGroupSeparators(List<UsingDirectiveSyntax> usings, bool addSeparation)
        {
            if (!addSeparation || usings.Count == 0)
                return usings;

            var result = new List<UsingDirectiveSyntax>();
            bool inSystemGroup = true;

            for (int i = 0; i < usings.Count; i++)
            {
                var name = usings[i].Name?.ToString() ?? string.Empty;
                var currentIsSystem = name.StartsWith("System");

                if (i > 0 && currentIsSystem != inSystemGroup)
                {
                    // 添加空行
                    var lastWithTrivia = usings[i - 1].WithTrailingTrivia(
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.CarriageReturnLineFeed);
                    result[result.Count - 1] = lastWithTrivia;
                }

                result.Add(usings[i]);
                inSystemGroup = currentIsSystem;
            }

            return result;
        }
    }
}
