using ColdRoomAssessment.Application.Coverage;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/analysis-period")]
public sealed class FacilityAnalysisPeriodController(AnalysisPeriodService periods) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AnalysisPeriod>> Get(
        Guid facilityId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        var result = await periods.GetAsync(facilityId, from, to, cancellationToken);
        return result.ToActionResult(value => Ok(value));
    }
}
