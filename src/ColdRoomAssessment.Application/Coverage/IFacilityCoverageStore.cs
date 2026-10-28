namespace ColdRoomAssessment.Application.Coverage;

public interface IFacilityCoverageStore
{
    Task<CoverageFacility?> FindFacilityAsync(Guid facilityId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CoverageEquipmentRecord>> ListEquipmentAsync(
        Guid facilityId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EquipmentMeasurementFacts>> ListMeasurementFactsAsync(
        Guid facilityId,
        CancellationToken cancellationToken);
}

public sealed record CoverageFacility(Guid Id, string Name);

public sealed record CoverageEquipmentRecord(
    Guid Id,
    string Name,
    string Placement,
    Guid EquipmentTypeId,
    string EquipmentTypeName);

public sealed record EquipmentMeasurementFacts(
    Guid EquipmentId,
    IReadOnlyList<DateTime> Timestamps,
    int MeasurementCount,
    int ImportCount);
