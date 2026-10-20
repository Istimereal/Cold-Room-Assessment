using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application;

public sealed record EquipmentSummary(Guid Id, Guid EquipmentTypeId, string Name, string Placement);

public sealed record FacilityResponse(Guid Id, string Name, IReadOnlyList<EquipmentSummary> Equipment)
{
    public static FacilityResponse From(Facility facility) =>
        new(
            facility.Id,
            facility.Name,
            facility.Equipment
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .Select(item => new EquipmentSummary(item.Id, item.EquipmentTypeId, item.Name, item.Placement))
                .ToArray());
}
