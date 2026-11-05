using ColdRoomAssessment.Application.Analysis;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/analysis")]
public sealed class FacilityAnalysisController(AnalysisService analysis) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FacilityAnalysis>> Get(
        Guid facilityId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        var result = await analysis.GetAsync(facilityId, from, to, cancellationToken);
        return result.ToActionResult(value => Ok(value));
    }
}
