namespace ColdRoomAssessment.Application.Coverage;

public sealed record AnalysisPeriod(
    Guid FacilityId,
    string FacilityName,
    DateTime From,
    DateTime To,
    int EquipmentCount,
    int EquipmentWithDataCount,
    int EquipmentWithCompleteCoverageCount,
    bool AllEquipmentHaveData,
    bool AllEquipmentHaveCompleteCoverage,
    IReadOnlyList<AnalysisWarning> Warnings,
    IReadOnlyList<AnalysisEquipment> Equipment);

public sealed record AnalysisWarning(Guid EquipmentId, AnalysisWarningCode Code);

public sealed record AnalysisEquipment(
    Guid EquipmentId,
    string EquipmentName,
    Guid EquipmentTypeId,
    string EquipmentTypeName,
    string Placement,
    DateTime From,
    DateTime To,
    DateTime? FirstMeasurementAt,
    DateTime? LastMeasurementAt,
    int SourceRowCount,
    int MeasurementCount,
    int ImportCount,
    int ExpectedSamplingIntervalSeconds,
    int ExpectedSourceSampleCount,
    int CoveredSourceSampleCount,
    int MissingSourceSampleCount,
    decimal CoveragePercent,
    bool HasData,
    bool HasCompleteCoverage,
    IReadOnlyList<AnalysisGap> Gaps);

public sealed record AnalysisGap(
    DateTime From,
    DateTime To,
    int MissingSourceSampleCount,
    AnalysisGapKind Kind);

public enum AnalysisWarningCode
{
    NoData,
    StartsAfterFrom,
    InternalGap,
    EndsBeforeTo
}

public enum AnalysisGapKind
{
    Leading,
    Internal,
    Trailing,
    FullInterval
}
