using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using System.Text;

namespace DotNetAnalyzer.Core.Roslyn.Formatting
{
    /// <summary>
    /// 文档格式化器实现（简化版）
    /// </summary>
    public class DocumentFormatter : IDocumentFormatter
    {
        /// <summary>
        /// 格式化文档
        /// </summary>
        public async Task<FormatResult> FormatDocumentAsync(Document document, FormatOptions options)
        {
            try
            {
                // 使用 Roslyn 的内置格式化
                var formattedDocument = await Formatter.FormatAsync(document);

                // 获取格式化后的代码
                var root = await formattedDocument.GetSyntaxRootAsync();
                if (root == null)
                {
                    return new FormatResult
                    {
                        Success = false,
                        ErrorMessage = "Unable to get syntax root"
                    };
                }

                var formattedCode = root.ToFullString();

                // 应用换行符转换
                formattedCode = ApplyLineEndings(formattedCode, options.NewLineStyle);

                return new FormatResult
                {
                    Success = true,
                    FormattedDocument = formattedDocument,
                    FormattedCode = formattedCode,
                    ChangesCount = 1 // 简化：总是返回 1，表示文档被格式化
                };
            }
            catch (Exception ex)
            {
                return new FormatResult
                {
                    Success = false,
                    ErrorMessage = $"Error formatting document: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 应用换行符转换
        /// </summary>
        private string ApplyLineEndings(string code, NewLineStyle style)
        {
            if (style == NewLineStyle.Auto)
            {
                return code; // 不转换
            }

            string normalizedCode = code.Replace("\r\n", "\n").Replace("\r", "\n");

            if (style == NewLineStyle.Windows)
            {
                return normalizedCode.Replace("\n", "\r\n");
            }

            return normalizedCode; // Unix
        }
    }
}
