using ColdRoomAssessment.Application;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/equipment")]
public sealed class EquipmentController(EquipmentService equipment) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipmentResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await equipment.GetAsync(id, cancellationToken);
        return result.ToActionResult(value => Ok(value));
    }
}
