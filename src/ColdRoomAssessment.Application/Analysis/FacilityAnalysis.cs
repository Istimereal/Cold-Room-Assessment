namespace ColdRoomAssessment.Application.Analysis;

public sealed record FacilityAnalysis(
    Guid FacilityId,
    string FacilityName,
    DateTime From,
    DateTime To,
    int EquipmentCount,
    int EquipmentWithTemperatureCount,
    int EquipmentWithRelativeHumidityCount,
    int ValidTemperatureCount,
    int ValidRelativeHumidityCount,
    IReadOnlyList<EquipmentAnalysis> Equipment);

public sealed record EquipmentAnalysis(
    Guid EquipmentId,
    string EquipmentName,
    Guid EquipmentTypeId,
    string EquipmentTypeName,
    string Placement,
    int SourceRowCount,
    int MeasurementCount,
    decimal CoveragePercent,
    bool HasCompleteCoverage,
    int AmbiguousSampleCount,
    int MissingExpectedSampleCount,
    KindAnalysis Temperature,
    KindAnalysis RelativeHumidity);

public sealed record KindAnalysis(
    int Count,
    double? Min,
    DateTime? MinAt,
    double? Max,
    DateTime? MaxAt,
    double? Mean,
    double? Median,
    double? PopulationStdDev,
    double? First,
    DateTime? FirstAt,
    double? Last,
    DateTime? LastAt,
    double? Change,
    double? DurationSeconds,
    IReadOnlyList<SeriesEvent> Events);

public sealed record SeriesEvent(
    DateTime Timestamp,
    double Value,
    SeriesEventKind Kind);

public enum SeriesEventKind
{
    Peak,
    Dip
}
