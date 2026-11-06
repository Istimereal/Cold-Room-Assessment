using System.Text.Json;
using ColdRoomAssessment.Api;
using ColdRoomAssessment.Application.Verification;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/verifications")]
public sealed class FacilityVerificationController(VerifierService verifications) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<FacilityVerification>> Post(
        Guid facilityId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] bool? includeContext,
        [FromBody] JsonElement investigation,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = investigation.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? null
                : investigation.GetRawText();
            var result = await verifications.VerifyAsync(
                facilityId,
                from,
                to,
                json,
                includeContext.GetValueOrDefault(),
                cancellationToken);
            return result.ToActionResult(value => Ok(value));
        }
        catch (VerifierNotConfiguredException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("Verifier is not configured."));
        }
        catch (VerifierUnavailableException)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new ErrorResponse("The verifier did not return a result."));
        }
        catch (VerifierInvalidResultException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new ErrorResponse(VerifierInvalidResultException.MessageFor(exception.Reason)));
        }
    }
}
