using Shared.Errors;

namespace Shared.Results;

public class Result
{
    protected Result(bool isSuccess, AppError error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public AppError Error { get; }

    public static Result Success() => new(true, AppError.None);
    public static Result Failure(AppError error) => new(false, error);
}

public sealed class Result<T> : Result
{
    private Result(T? value, bool isSuccess, AppError error) : base(isSuccess, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value, true, AppError.None);
    public static new Result<T> Failure(AppError error) => new(default, false, error);
}
