using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application.Imports;

public sealed record ImportSummary(
    Guid Id,
    Guid EquipmentId,
    string EquipmentName,
    string? OriginalFileName,
    DateTimeOffset? ImportedAt,
    DateTime? FirstMeasurementAt,
    DateTime? LastMeasurementAt,
    int ValidRowCount,
    int InvalidRowCount,
    int MeasurementCount,
    ImportStatus Status);
