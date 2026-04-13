using Shared.Constants;
using Shared.Errors;

namespace Application.Abstractions;

public static class AppErrors
{
    public static AppError NotFound(string message) => new(ErrorCodes.NotFound, message);
    public static AppError Validation(string message) => new(ErrorCodes.Validation, message);
    public static AppError PremiumRequired(string message) => new(ErrorCodes.PremiumRequired, message);
    public static AppError Conflict(string message) => new(ErrorCodes.Conflict, message);
}
