using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application;

public sealed class EquipmentTypeService(IColdRoomStore store)
{
    public async Task<OperationResult<EquipmentTypeResponse>> CreateAsync(
        string? name,
        CancellationToken cancellationToken)
    {
        if (!TextField.TryTrim(name, "Name", TextField.NameMaxLength, out var trimmed, out var error))
        {
            return OperationResult<EquipmentTypeResponse>.Invalid(error!);
        }

        var equipmentType = new EquipmentType
        {
            Id = Guid.CreateVersion7(),
            Name = trimmed
        };

        try
        {
            await store.AddEquipmentTypeAsync(equipmentType, cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            return OperationResult<EquipmentTypeResponse>.Conflict(
                $"An equipment type named '{trimmed}' already exists.");
        }

        return OperationResult<EquipmentTypeResponse>.Success(
            new EquipmentTypeResponse(equipmentType.Id, equipmentType.Name));
    }

    public async Task<OperationResult<EquipmentTypeResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var equipmentType = await store.GetEquipmentTypeAsync(id, cancellationToken);
        if (equipmentType is null)
        {
            return OperationResult<EquipmentTypeResponse>.NotFound("Equipment type was not found.");
        }

        return OperationResult<EquipmentTypeResponse>.Success(
            new EquipmentTypeResponse(equipmentType.Id, equipmentType.Name));
    }
}
