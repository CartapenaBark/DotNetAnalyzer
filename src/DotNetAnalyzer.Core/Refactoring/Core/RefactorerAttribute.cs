namespace DotNetAnalyzer.Core.Refactoring.Core;

/// <summary>
/// 重构器特性，用于标记和注册重构器
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RefactorerAttribute : Attribute
{
    /// <summary>
    /// 获取重构器名称（唯一标识）
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 创建重构器特性
    /// </summary>
    /// <param name="name">重构器名称（唯一标识，如 "extract_method"）</param>
    /// <param name="displayName">重构器显示名称（如 "提取方法"）</param>
    /// <param name="category">重构器分类（如 "Extraction"）</param>
    /// <param name="description">重构器描述</param>
    public RefactorerAttribute(
        string name,
        string displayName,
        string category,
        string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("重构器名称不能为空", nameof(name));

        Name = name;
        DisplayName = displayName;
        Category = category;
        Description = description;
    }
}
