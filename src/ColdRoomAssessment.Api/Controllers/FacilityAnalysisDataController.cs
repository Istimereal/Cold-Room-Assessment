using ColdRoomAssessment.Application.Coverage;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/analysis-data")]
public sealed class FacilityAnalysisDataController(AnalysisDataService data) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AnalysisData>> Get(
        Guid facilityId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        var result = await data.GetAsync(facilityId, from, to, cancellationToken);
        return result.ToActionResult(value => Ok(value));
    }
}
