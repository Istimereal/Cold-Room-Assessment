using ColdRoomAssessment.Application;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/facilities")]
public sealed class FacilitiesController(FacilityService facilities) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<FacilityResponse>> Create(
        CreateFacilityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await facilities.CreateAsync(request.Name, cancellationToken);
        return result.ToActionResult(value => Created($"/api/facilities/{value.Id}", value));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FacilityResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await facilities.GetAsync(id, cancellationToken);
        return result.ToActionResult(value => Ok(value));
    }
}

public sealed record CreateFacilityRequest(string? Name);
