using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application;

public sealed class EquipmentService(IColdRoomStore store)
{
    public async Task<OperationResult<EquipmentResponse>> CreateAsync(
        Guid facilityId,
        Guid equipmentTypeId,
        string? name,
        string? placement,
        CancellationToken cancellationToken)
    {
        if (!TextField.TryTrim(name, "Name", TextField.NameMaxLength, out var trimmedName, out var nameError))
        {
            return OperationResult<EquipmentResponse>.Invalid(nameError!);
        }

        if (!TextField.TryTrim(
                placement,
                "Placement",
                TextField.PlacementMaxLength,
                out var trimmedPlacement,
                out var placementError))
        {
            return OperationResult<EquipmentResponse>.Invalid(placementError!);
        }

        if (equipmentTypeId == Guid.Empty)
        {
            return OperationResult<EquipmentResponse>.Invalid("Equipment type id is required.");
        }

        var facility = await store.GetFacilityAsync(facilityId, cancellationToken);
        if (facility is null)
        {
            return OperationResult<EquipmentResponse>.NotFound("Facility was not found.");
        }

        var equipmentType = await store.GetEquipmentTypeAsync(equipmentTypeId, cancellationToken);
        if (equipmentType is null)
        {
            return OperationResult<EquipmentResponse>.NotFound("Equipment type was not found.");
        }

        var equipment = new Equipment
        {
            Id = Guid.CreateVersion7(),
            FacilityId = facility.Id,
            EquipmentTypeId = equipmentType.Id,
            Name = trimmedName,
            Placement = trimmedPlacement
        };

        try
        {
            await store.AddEquipmentAsync(equipment, cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            return OperationResult<EquipmentResponse>.Conflict(
                $"Equipment named '{trimmedName}' already exists in this facility.");
        }

        return OperationResult<EquipmentResponse>.Success(
            EquipmentResponse.From(equipment, facility.Name, equipmentType.Name));
    }

    public async Task<OperationResult<EquipmentResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var equipment = await store.GetEquipmentAsync(id, cancellationToken);
        if (equipment is null)
        {
            return OperationResult<EquipmentResponse>.NotFound("Equipment was not found.");
        }

        if (equipment.Facility is null || equipment.EquipmentType is null)
        {
            throw new InvalidOperationException("Equipment is missing its facility or equipment type.");
        }

        return OperationResult<EquipmentResponse>.Success(
            EquipmentResponse.From(equipment, equipment.Facility.Name, equipment.EquipmentType.Name));
    }
}
