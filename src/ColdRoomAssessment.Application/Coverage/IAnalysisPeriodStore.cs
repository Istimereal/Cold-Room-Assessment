namespace ColdRoomAssessment.Application.Coverage;

public interface IAnalysisPeriodStore
{
    Task<CoverageFacility?> FindFacilityAsync(Guid facilityId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CoverageEquipmentRecord>> ListEquipmentAsync(
        Guid facilityId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EquipmentMeasurementFacts>> ListMeasurementFactsAsync(
        Guid facilityId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken);
}
