namespace Shared.Errors;

public sealed record AppError(string Code, string Message)
{
    public static readonly AppError None = new(string.Empty, string.Empty);
}
