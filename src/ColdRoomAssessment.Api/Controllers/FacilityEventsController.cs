using ColdRoomAssessment.Application.Events;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/events")]
public sealed class FacilityEventsController(RoomEventService events) : ControllerBase
{
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<RoomEventImportSummary>> Import(
        Guid facilityId,
        CancellationToken cancellationToken)
    {
        var file = Request.Form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return new BadRequestObjectResult(new ErrorResponse("File is required."));
        }

        await using var content = file.OpenReadStream();
        var result = await events.ImportAsync(facilityId, content, cancellationToken);
        return result.ToActionResult(value => Created($"/api/facilities/{facilityId}/events", value));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoomEventResponse>>> List(
        Guid facilityId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        var result = await events.ListAsync(facilityId, from, to, cancellationToken);
        return result.ToActionResult(value => Ok(value));
    }
}
