using System.Collections.Immutable;

namespace DotNetAnalyzer.Core.Refactoring.Models;

/// <summary>
/// 表示可能失败的操作结果
/// </summary>
/// <typeparam name="T">成功值的类型</typeparam>
public sealed class Result<T>
{
    /// <summary>
    /// 获取操作是否成功
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 获取操作是否失败
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// 获取成功时的值
    /// </summary>
    /// <exception cref="InvalidOperationException">操作失败时访问值</exception>
    public T Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException("不能访问失败结果的值");
            return _value!;
        }
    }

    /// <summary>
    /// 获取失败时的错误列表
    /// </summary>
    public IImmutableList<RefactoringError> Errors { get; }

    private readonly T? _value;

    private Result(bool isSuccess, T? value, IImmutableList<RefactoringError> errors)
    {
        IsSuccess = isSuccess;
        _value = value;
        Errors = errors;
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="value">成功值</param>
    /// <returns>成功结果</returns>
    public static Result<T> Success(T value)
    {
        return new Result<T>(true, value, ImmutableList<RefactoringError>.Empty);
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errors">错误列表</param>
    /// <returns>失败结果</returns>
    public static Result<T> Failure(params RefactoringError[] errors)
    {
        if (errors.Length == 0)
        {
            throw new ArgumentException("至少需要一个错误", nameof(errors));
        }
        return new Result<T>(false, default, errors.ToImmutableList());
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="error">单个错误</param>
    /// <returns>失败结果</returns>
    public static Result<T> Failure(RefactoringError error)
    {
        return new Result<T>(false, default, ImmutableList.Create(error));
    }

    /// <summary>
    /// 创建失败结果（从错误代码和消息）
    /// </summary>
    /// <param name="code">错误代码</param>
    /// <param name="message">错误消息</param>
    /// <returns>失败结果</returns>
    public static Result<T> Failure(string code, string message)
    {
        var error = new RefactoringError
        {
            Code = code,
            Message = message,
            Severity = ErrorSeverity.Error
        };
        return Failure(error);
    }
}

/// <summary>
/// 表示没有返回值的操作结果
/// </summary>
public sealed class Result
{
    /// <summary>
    /// 获取操作是否成功
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 获取操作是否失败
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// 获取失败时的错误列表
    /// </summary>
    public IImmutableList<RefactoringError> Errors { get; }

    private Result(bool isSuccess, IImmutableList<RefactoringError> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <returns>成功结果</returns>
    public static Result Success()
    {
        return new Result(true, ImmutableList<RefactoringError>.Empty);
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errors">错误列表</param>
    /// <returns>失败结果</returns>
    public static Result Failure(params RefactoringError[] errors)
    {
        if (errors.Length == 0)
        {
            throw new ArgumentException("至少需要一个错误", nameof(errors));
        }
        return new Result(false, errors.ToImmutableList());
    }

    /// <summary>
    /// 创建失败结果（从错误代码和消息）
    /// </summary>
    /// <param name="code">错误代码</param>
    /// <param name="message">错误消息</param>
    /// <returns>失败结果</returns>
    public static Result Failure(string code, string message)
    {
        var error = new RefactoringError
        {
            Code = code,
            Message = message,
            Severity = ErrorSeverity.Error
        };
        return Failure(error);
    }
}
