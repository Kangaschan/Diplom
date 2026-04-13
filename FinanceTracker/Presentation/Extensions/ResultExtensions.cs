using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.Results;

namespace Presentation.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this ControllerBase controller, Result result)
    {
        if (result.IsSuccess) return controller.NoContent();

        return result.Error.Code switch
        {
            ErrorCodes.NotFound => controller.NotFound(new Contracts.ApiErrorResponse(result.Error.Code, result.Error.Message)),
            ErrorCodes.Validation => controller.BadRequest(new Contracts.ApiErrorResponse(result.Error.Code, result.Error.Message)),
            ErrorCodes.Conflict => controller.Conflict(new Contracts.ApiErrorResponse(result.Error.Code, result.Error.Message)),
            ErrorCodes.Unauthorized => controller.Unauthorized(new Contracts.ApiErrorResponse(result.Error.Code, result.Error.Message)),
            ErrorCodes.Forbidden or ErrorCodes.PremiumRequired => controller.StatusCode(403, new Contracts.ApiErrorResponse(result.Error.Code, result.Error.Message)),
            _ => controller.StatusCode(500, new Contracts.ApiErrorResponse(result.Error.Code, result.Error.Message))
        };
    }

    public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess) return controller.Ok(result.Value);

        return controller.ToActionResult(Result.Failure(result.Error));
    }
}
