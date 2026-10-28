using System.Globalization;

namespace ColdRoomAssessment.Application.Coverage;

public sealed class AnalysisDataService(IAnalysisDataStore store)
{
    public static readonly TimeSpan MaximumInterval = TimeSpan.FromHours(24);

    public async Task<OperationResult<AnalysisData>> GetAsync(
        Guid facilityId,
        string? fromText,
        string? toText,
        CancellationToken cancellationToken)
    {
        if (!TryParseTimestamp(fromText, "From", out var from, out var fromError))
        {
            return OperationResult<AnalysisData>.Invalid(fromError!);
        }

        if (!TryParseTimestamp(toText, "To", out var to, out var toError))
        {
            return OperationResult<AnalysisData>.Invalid(toError!);
        }

        if (from >= to)
        {
            return OperationResult<AnalysisData>.Invalid("From must be before to.");
        }

        if (to - from > MaximumInterval)
        {
            return OperationResult<AnalysisData>.Invalid("The requested interval is longer than 24 hours.");
        }

        var facility = await store.FindFacilityAsync(facilityId, cancellationToken);
        if (facility is null)
        {
            return OperationResult<AnalysisData>.NotFound("Facility was not found.");
        }

        var equipment = await store.ListEquipmentAsync(facilityId, cancellationToken);
        var rows = await store.ListMeasurementsAsync(facilityId, from, to, cancellationToken);
        var rowsByEquipment = rows
            .GroupBy(row => row.EquipmentId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<AnalysisMeasurementRow>)group.ToArray());
        var grid = ExpectedSampleGrid.Create(from, to);

        var assembled = new List<AnalysisEquipmentData>(equipment.Count);
        foreach (var item in equipment)
        {
            rowsByEquipment.TryGetValue(item.Id, out var equipmentRows);
            assembled.Add(AnalysisDataAssembler.Assemble(
                item,
                from,
                to,
                grid,
                equipmentRows ?? []));
        }

        return OperationResult<AnalysisData>.Success(new AnalysisData(
            facility.Id,
            facility.Name,
            from,
            to,
            MeasurementCoverageCalculator.ExpectedSamplingIntervalSeconds,
            grid.Count,
            assembled));
    }

    private static bool TryParseTimestamp(
        string? text,
        string fieldName,
        out DateTime timestamp,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            timestamp = default;
            error = $"{fieldName} is required.";
            return false;
        }

        if (!DateTime.TryParseExact(
                text,
                AnalysisPeriodService.TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out timestamp))
        {
            error = $"{fieldName} must be yyyy-MM-ddTHH:mm:ss.";
            return false;
        }

        error = null;
        return true;
    }
}
