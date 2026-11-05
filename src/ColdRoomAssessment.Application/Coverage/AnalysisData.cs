namespace ColdRoomAssessment.Application.Coverage;

public sealed record AnalysisData(
    Guid FacilityId,
    string FacilityName,
    DateTime From,
    DateTime To,
    int ExpectedSamplingIntervalSeconds,
    int ExpectedGridPointCount,
    IReadOnlyList<AnalysisEquipmentData> Equipment);

public sealed record AnalysisEquipmentData(
    Guid EquipmentId,
    string EquipmentName,
    Guid EquipmentTypeId,
    string EquipmentTypeName,
    string Placement,
    int SourceRowCount,
    int MeasurementCount,
    decimal CoveragePercent,
    bool HasCompleteCoverage,
    IReadOnlyList<AnalysisSample> Samples);

public sealed record AnalysisSample(
    DateTime Timestamp,
    double? Temperature,
    double? RelativeHumidity,
    bool HasSourceSample,
    bool Ambiguous);
