using ColdRoomAssessment.Application;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api;

public static class OperationResultHttpExtensions
{
    public static ActionResult<T> ToActionResult<T>(
        this OperationResult<T> result,
        Func<T, ActionResult<T>> onSuccess)
    {
        switch (result.Status)
        {
            case OperationStatus.Success:
                return onSuccess(result.Value!);
            case OperationStatus.Invalid:
                return new BadRequestObjectResult(new ErrorResponse(result.Error!));
            case OperationStatus.NotFound:
                return new NotFoundObjectResult(new ErrorResponse(result.Error!));
            case OperationStatus.Conflict:
                return new ConflictObjectResult(new ErrorResponse(result.Error!));
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown operation status.");
        }
    }
}

public sealed record ErrorResponse(string Error);
