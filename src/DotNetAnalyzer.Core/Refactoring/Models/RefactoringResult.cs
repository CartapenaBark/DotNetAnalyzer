namespace DotNetAnalyzer.Core.Refactoring.Models;

/// <summary>
/// 重构结果
/// </summary>
public sealed class RefactoringResult
{
    /// <summary>
    /// 获取或设置是否为预览模式
    /// </summary>
    public bool IsPreview { get; set; }

    /// <summary>
    /// 获取或设置重构预览（预览模式或应用后都包含）
    /// </summary>
    public RefactoringPreview? Preview { get; set; }

    /// <summary>
    /// 获取或设置已应用的文件变更（应用模式下有值）
    /// </summary>
    public IReadOnlyList<FileChange>? AppliedChanges { get; set; }

    /// <summary>
    /// 获取或设置重构状态
    /// </summary>
    public RefactoringStatus Status { get; set; } = RefactoringStatus.Success;

    /// <summary>
    /// 获取或设置错误信息（失败时有值）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 创建成功的预览结果
    /// </summary>
    public static RefactoringResult PreviewSuccess(RefactoringPreview preview)
    {
        return new RefactoringResult
        {
            IsPreview = true,
            Preview = preview,
            Status = RefactoringStatus.Success
        };
    }

    /// <summary>
    /// 创建成功的应用结果
    /// </summary>
    public static RefactoringResult ApplySuccess(RefactoringPreview preview, IReadOnlyList<FileChange> appliedChanges)
    {
        return new RefactoringResult
        {
            IsPreview = false,
            Preview = preview,
            AppliedChanges = appliedChanges,
            Status = RefactoringStatus.Success
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static RefactoringResult Failure(string errorMessage)
    {
        return new RefactoringResult
        {
            Status = RefactoringStatus.Failed,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// 重构状态
/// </summary>
public enum RefactoringStatus
{
    /// <summary>
    /// 成功
    /// </summary>
    Success = 0,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 1,

    /// <summary>
    /// 取消
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// 部分成功
    /// </summary>
    PartialSuccess = 3
}
