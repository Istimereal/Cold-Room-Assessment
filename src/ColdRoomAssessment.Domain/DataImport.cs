namespace ColdRoomAssessment.Domain;

public sealed class DataImport
{
    public Guid Id { get; set; }

    public Guid EquipmentId { get; set; }

    public string? OriginalFileName { get; set; }

    public DateTimeOffset? ImportedAt { get; set; }

    public DateTime? FirstMeasurementAt { get; set; }

    public DateTime? LastMeasurementAt { get; set; }

    public int ImportedRowCount { get; set; }

    public int InvalidRowCount { get; set; }

    public ImportStatus Status { get; set; }

    public Equipment? Equipment { get; set; }

    public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
}
