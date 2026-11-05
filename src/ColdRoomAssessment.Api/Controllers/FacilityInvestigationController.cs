using ColdRoomAssessment.Api;
using ColdRoomAssessment.Application.Investigation;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/investigations")]
public sealed class FacilityInvestigationController(InvestigationService investigations) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<FacilityInvestigation>> Post(
        Guid facilityId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await investigations.InvestigateAsync(facilityId, from, to, cancellationToken);
            return result.ToActionResult(value => Ok(value));
        }
        catch (InvestigatorNotConfiguredException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("Investigator is not configured."));
        }
        catch (InvestigatorUnavailableException)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new ErrorResponse("The investigator did not return a result."));
        }
        catch (InvestigatorInvalidResultException)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new ErrorResponse("The investigator returned an invalid result."));
        }
    }
}
