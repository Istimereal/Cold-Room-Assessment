using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application;

public sealed record EquipmentResponse(
    Guid Id,
    Guid FacilityId,
    string FacilityName,
    Guid EquipmentTypeId,
    string EquipmentTypeName,
    string Name,
    string Placement)
{
    public static EquipmentResponse From(Equipment equipment, string facilityName, string equipmentTypeName) =>
        new(
            equipment.Id,
            equipment.FacilityId,
            facilityName,
            equipment.EquipmentTypeId,
            equipmentTypeName,
            equipment.Name,
            equipment.Placement);
}
