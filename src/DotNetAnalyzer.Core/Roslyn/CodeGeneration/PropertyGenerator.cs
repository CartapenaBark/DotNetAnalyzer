using Microsoft.CodeAnalysis;

namespace DotNetAnalyzer.Core.Roslyn.CodeGeneration;

/// <summary>
/// 属性生成器
/// </summary>
public class PropertyGenerator
{
    /// <summary>
    /// 生成属性
    /// </summary>
    /// <param name="propertyType">属性类型</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="hasBackingField">是否有后备字段</param>
    /// <param name="hasGetter">是否有getter</param>
    /// <param name="hasSetter">是否有setter</param>
    /// <param name="isReadOnly">是否只读</param>
    /// <returns>生成的属性代码</returns>
    public static string GenerateProperty(
        string propertyType,
        string propertyName,
        bool hasBackingField = false,
        bool hasGetter = true,
        bool hasSetter = true,
        bool isReadOnly = false)
    {
        var code = new System.Text.StringBuilder();
        code.Append($"    public {propertyType} {propertyName} ");

        if (hasBackingField)
        {
            // 生成带后备字段的属性
            code.AppendLine("{");
            if (hasGetter)
                code.AppendLine($"        get => _{char.ToLower(propertyName[0])}{propertyName[1..]};");
            if (hasSetter && !isReadOnly)
                code.AppendLine($"        set => _{char.ToLower(propertyName[0])}{propertyName[1..]} = value;");
            code.AppendLine("    }");
        }
        else
        {
            // 自动属性
            if (hasGetter && hasSetter && !isReadOnly)
            {
                code.AppendLine("{ get; set; }");
            }
            else if (hasGetter && isReadOnly)
            {
                code.AppendLine("{ get; }");
            }
            else if (hasGetter)
            {
                code.AppendLine("{ get; private set; }");
            }
            else
            {
                code.AppendLine("{ set; }");
            }
        }

        return code.ToString();
    }
}
