using ColdRoomAssessment.Application;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/equipment")]
public sealed class FacilityEquipmentController(EquipmentService equipment) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EquipmentResponse>> Create(
        Guid facilityId,
        CreateEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await equipment.CreateAsync(
            facilityId,
            request.EquipmentTypeId,
            request.Name,
            request.Placement,
            cancellationToken);

        return result.ToActionResult(value => Created($"/api/equipment/{value.Id}", value));
    }
}

public sealed record CreateEquipmentRequest(Guid EquipmentTypeId, string? Name, string? Placement);
