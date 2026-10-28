namespace ColdRoomAssessment.Application.Coverage;

public sealed record FacilityCoverage(
    Guid FacilityId,
    string FacilityName,
    DateTime? EarliestMeasurementAt,
    DateTime? LatestMeasurementAt,
    bool HasCommonOverlap,
    DateTime? CommonOverlapStart,
    DateTime? CommonOverlapEnd,
    IReadOnlyList<EquipmentCoverage> Equipment);

public sealed record EquipmentCoverage(
    Guid EquipmentId,
    string EquipmentName,
    Guid EquipmentTypeId,
    string EquipmentTypeName,
    string Placement,
    DateTime? FirstMeasurementAt,
    DateTime? LastMeasurementAt,
    int SourceRowCount,
    int MeasurementCount,
    int ImportCount,
    int ExpectedSamplingIntervalSeconds,
    int MissingSourceSampleCount,
    IReadOnlyList<CoverageGap> Gaps);

public sealed record CoverageGap(
    DateTime After,
    DateTime Before,
    int MissingSourceSampleCount);
