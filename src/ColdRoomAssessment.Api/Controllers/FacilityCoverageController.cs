using ColdRoomAssessment.Application.Coverage;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/measurement-coverage")]
public sealed class FacilityCoverageController(FacilityCoverageService coverage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FacilityCoverage>> Get(
        Guid facilityId,
        CancellationToken cancellationToken)
    {
        var result = await coverage.GetAsync(facilityId, cancellationToken);
        return result.ToActionResult(value => Ok(value));
    }
}
