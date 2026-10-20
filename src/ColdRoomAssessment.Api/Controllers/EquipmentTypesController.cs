using ColdRoomAssessment.Application;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/equipment-types")]
public sealed class EquipmentTypesController(EquipmentTypeService equipmentTypes) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EquipmentTypeResponse>> Create(
        CreateEquipmentTypeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await equipmentTypes.CreateAsync(request.Name, cancellationToken);
        return result.ToActionResult(value => Created($"/api/equipment-types/{value.Id}", value));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipmentTypeResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await equipmentTypes.GetAsync(id, cancellationToken);
        return result.ToActionResult(value => Ok(value));
    }
}

public sealed record CreateEquipmentTypeRequest(string? Name);
