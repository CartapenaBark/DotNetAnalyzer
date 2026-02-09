namespace DotNetAnalyzer.Core.Refactoring.Models;

/// <summary>
/// 重构预览
/// </summary>
public sealed class RefactoringPreview
{
    /// <summary>
    /// 获取或设置所有文件变更
    /// </summary>
    public required IReadOnlyList<FileChange> FileChanges { get; set; }

    /// <summary>
    /// 获取或设置受影响的文件列表
    /// </summary>
    public required IReadOnlyList<AffectedFile> AffectedFiles { get; set; }

    /// <summary>
    /// 获取或设置统一的 diff 字符串
    /// </summary>
    public string? Diff { get; set; }

    /// <summary>
    /// 获取或设置验证结果
    /// </summary>
    public RefactoringValidation Validation { get; set; } = RefactoringValidation.Valid();

    /// <summary>
    /// 获取或设置重构描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 获取或设置额外的元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 重构验证结果
/// </summary>
public sealed class RefactoringValidation
{
    /// <summary>
    /// 获取或设置验证是否通过
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 获取或设置验证错误列表
    /// </summary>
    public IReadOnlyList<RefactoringError> Errors { get; set; } = Array.Empty<RefactoringError>();

    /// <summary>
    /// 获取或设置验证警告列表
    /// </summary>
    public IReadOnlyList<RefactoringError> Warnings { get; set; } = Array.Empty<RefactoringError>();

    /// <summary>
    /// 创建有效的验证结果
    /// </summary>
    public static RefactoringValidation Valid()
    {
        return new RefactoringValidation { IsValid = true };
    }

    /// <summary>
    /// 创建无效的验证结果
    /// </summary>
    public static RefactoringValidation Invalid(params RefactoringError[] errors)
    {
        return new RefactoringValidation
        {
            IsValid = false,
            Errors = errors
        };
    }

    /// <summary>
    /// 添加警告
    /// </summary>
    public RefactoringValidation WithWarning(RefactoringError warning)
    {
        var warnings = new List<RefactoringError>(Warnings) { warning };
        return new RefactoringValidation
        {
            IsValid = IsValid,
            Errors = Errors,
            Warnings = warnings
        };
    }
}
