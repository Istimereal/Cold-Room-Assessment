using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application.Coverage;

public interface IAnalysisDataStore
{
    Task<CoverageFacility?> FindFacilityAsync(Guid facilityId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CoverageEquipmentRecord>> ListEquipmentAsync(
        Guid facilityId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AnalysisMeasurementRow>> ListMeasurementsAsync(
        Guid facilityId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken);
}

public sealed record AnalysisMeasurementRow(
    Guid EquipmentId,
    DateTime MeasuredAt,
    MeasurementKind Kind,
    double Value,
    Guid ImportId);
