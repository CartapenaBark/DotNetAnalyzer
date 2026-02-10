using Microsoft.CodeAnalysis;

namespace DotNetAnalyzer.Core.Roslyn.CodeGeneration;

/// <summary>
/// 解构函数生成器
/// </summary>
public class DeconstructorGenerator
{
    /// <summary>
    /// 生成解构函数
    /// </summary>
    public static string GenerateDeconstructor(
        string className,
        List<ParameterInfo> parameters)
    {
        var code = new System.Text.StringBuilder();

        // 生成参数列表
        var paramList = string.Join(", ", parameters.Select(p => $"out {p.Type} {p.Name}"));

        // 生成解构函数
        code.AppendLine($"    public void Deconstruct({paramList})");
        code.AppendLine("    {");

        // 生成赋值语句
        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            code.AppendLine($"        {param.Name} = {param.MemberName};");
        }

        code.AppendLine("    }");

        return code.ToString();
    }

    /// <summary>
    /// 参数信息
    /// </summary>
    public class ParameterInfo
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
    }
}
