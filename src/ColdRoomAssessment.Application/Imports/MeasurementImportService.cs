using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application.Imports;

public sealed class MeasurementImportService(IMeasurementImportStore store)
{
    public const string TemperatureUnit = "°C";

    public const string RelativeHumidityUnit = "%";

    public async Task<OperationResult<ImportSummary>> ImportAsync(
        Guid equipmentId,
        string? fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        var equipment = await store.FindEquipmentAsync(equipmentId, cancellationToken);
        if (equipment is null)
        {
            return OperationResult<ImportSummary>.NotFound("Equipment was not found.");
        }

        using var reader = new StreamReader(content, leaveOpen: true);
        var parsed = Dht22LogParser.Parse(await reader.ReadToEndAsync(cancellationToken));
        if (!parsed.FormatRecognized)
        {
            return OperationResult<ImportSummary>.Invalid("The file format was not recognized.");
        }

        if (parsed.Rows.Count == 0)
        {
            return OperationResult<ImportSummary>.Invalid("The file does not contain a valid data row.");
        }

        var import = new DataImport
        {
            Id = Guid.CreateVersion7(),
            EquipmentId = equipment.Id,
            OriginalFileName = string.IsNullOrWhiteSpace(fileName) ? null : Path.GetFileName(fileName),
            ImportedAt = DateTimeOffset.UtcNow,
            FirstMeasurementAt = parsed.Rows.Min(row => row.MeasuredAt),
            LastMeasurementAt = parsed.Rows.Max(row => row.MeasuredAt),
            ImportedRowCount = parsed.Rows.Count,
            InvalidRowCount = parsed.InvalidRowCount,
            Status = ImportStatus.Completed
        };

        var measurements = new List<Measurement>(parsed.Rows.Count * 2);
        foreach (var row in parsed.Rows)
        {
            measurements.Add(CreateMeasurement(import.Id, row.MeasuredAt, row.Temperature, MeasurementKind.Temperature, TemperatureUnit));
            measurements.Add(CreateMeasurement(import.Id, row.MeasuredAt, row.Humidity, MeasurementKind.RelativeHumidity, RelativeHumidityUnit));
        }

        await store.AddImportAsync(import, measurements, cancellationToken);
        return OperationResult<ImportSummary>.Success(ToSummary(import, equipment.Name, measurements.Count));
    }

    public async Task<OperationResult<IReadOnlyList<ImportSummary>>> ListAsync(
        Guid equipmentId,
        CancellationToken cancellationToken)
    {
        var equipment = await store.FindEquipmentAsync(equipmentId, cancellationToken);
        if (equipment is null)
        {
            return OperationResult<IReadOnlyList<ImportSummary>>.NotFound("Equipment was not found.");
        }

        var imports = await store.ListImportsAsync(equipmentId, cancellationToken);
        return OperationResult<IReadOnlyList<ImportSummary>>.Success(imports);
    }

    private static Measurement CreateMeasurement(
        Guid importId,
        DateTime measuredAt,
        double value,
        MeasurementKind kind,
        string unit) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            DataImportId = importId,
            MeasuredAt = measuredAt,
            Value = value,
            Kind = kind,
            Unit = unit
        };

    private static ImportSummary ToSummary(DataImport import, string equipmentName, int measurementCount) =>
        new(
            import.Id,
            import.EquipmentId,
            equipmentName,
            import.OriginalFileName,
            import.ImportedAt,
            import.FirstMeasurementAt,
            import.LastMeasurementAt,
            import.ImportedRowCount,
            import.InvalidRowCount,
            measurementCount,
            import.Status);
}
