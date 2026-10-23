using ColdRoomAssessment.Application.Imports;
using Microsoft.AspNetCore.Mvc;

namespace ColdRoomAssessment.Api.Controllers;

[ApiController]
[Route("api/equipment/{equipmentId:guid}/imports")]
public sealed class EquipmentImportsController(MeasurementImportService imports) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportSummary>> Create(
        Guid equipmentId,
        CancellationToken cancellationToken)
    {
        var file = Request.Form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return new BadRequestObjectResult(new ErrorResponse("File is required."));
        }

        await using var content = file.OpenReadStream();
        var result = await imports.ImportAsync(equipmentId, file.FileName, content, cancellationToken);
        return result.ToActionResult(value => Created($"/api/equipment/{equipmentId}/imports", value));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ImportSummary>>> List(
        Guid equipmentId,
        CancellationToken cancellationToken)
    {
        var result = await imports.ListAsync(equipmentId, cancellationToken);
        return result.ToActionResult(value => Ok(value));
    }
}
