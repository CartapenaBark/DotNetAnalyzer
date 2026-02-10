using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetAnalyzer.Core.Roslyn.CodeGeneration;

/// <summary>
/// 构造函数生成器
/// </summary>
public class ConstructorGenerator
{
    /// <summary>
    /// 生成构造函数
    /// </summary>
    public string GenerateConstructor(
        string className,
        List<FieldInfo> fields,
        string? baseConstructorCall = null)
    {
        var code = new System.Text.StringBuilder();

        // 生成参数列表
        var parameters = string.Join(", ", fields.Select(f => $"{f.Type} {f.Name}"));

        // 生成构造函数声明
        code.AppendLine($"    public {className}({parameters})");
        code.AppendLine("    {");

        // 添加基类构造函数调用
        if (!string.IsNullOrEmpty(baseConstructorCall))
        {
            code.AppendLine($"        : base({baseConstructorCall})");
            code.Append("    ");
            code.AppendLine("{");
        }

        // 生成字段初始化
        foreach (var field in fields)
        {
            code.AppendLine($"        {field.FieldName} = {field.Name};");
        }

        code.AppendLine("    }");

        return code.ToString();
    }

    /// <summary>
    /// 字段信息
    /// </summary>
    public class FieldInfo
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
    }
}
