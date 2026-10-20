namespace ColdRoomAssessment.Domain;

public sealed class Measurement
{
    public Guid Id { get; set; }

    public Guid DataImportId { get; set; }

    public DateTime MeasuredAt { get; set; }

    public double Value { get; set; }

    public MeasurementKind Kind { get; set; }

    public string Unit { get; set; } = string.Empty;

    public DataImport? DataImport { get; set; }
}
