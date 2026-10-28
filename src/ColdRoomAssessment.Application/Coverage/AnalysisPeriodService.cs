using System.Globalization;

namespace ColdRoomAssessment.Application.Coverage;

public sealed class AnalysisPeriodService(IAnalysisPeriodStore store)
{
    public const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss";

    public async Task<OperationResult<AnalysisPeriod>> GetAsync(
        Guid facilityId,
        string? fromText,
        string? toText,
        CancellationToken cancellationToken)
    {
        if (!TryParseTimestamp(fromText, "From", out var from, out var fromError))
        {
            return OperationResult<AnalysisPeriod>.Invalid(fromError!);
        }

        if (!TryParseTimestamp(toText, "To", out var to, out var toError))
        {
            return OperationResult<AnalysisPeriod>.Invalid(toError!);
        }

        if (from >= to)
        {
            return OperationResult<AnalysisPeriod>.Invalid("From must be before to.");
        }

        var facility = await store.FindFacilityAsync(facilityId, cancellationToken);
        if (facility is null)
        {
            return OperationResult<AnalysisPeriod>.NotFound("Facility was not found.");
        }

        var equipment = await store.ListEquipmentAsync(facilityId, cancellationToken);
        var facts = await store.ListMeasurementFactsAsync(facilityId, from, to, cancellationToken);
        var factsByEquipment = facts.ToDictionary(item => item.EquipmentId);

        var coverage = new List<AnalysisEquipment>(equipment.Count);
        var warnings = new List<AnalysisWarning>();
        foreach (var item in equipment)
        {
            factsByEquipment.TryGetValue(item.Id, out var fact);
            var calculated = AnalysisPeriodCalculator.Calculate(from, to, fact?.Timestamps ?? []);
            coverage.Add(new AnalysisEquipment(
                item.Id,
                item.Name,
                item.EquipmentTypeId,
                item.EquipmentTypeName,
                item.Placement,
                from,
                to,
                calculated.FirstMeasurementAt,
                calculated.LastMeasurementAt,
                calculated.SourceRowCount,
                fact?.MeasurementCount ?? 0,
                fact?.ImportCount ?? 0,
                MeasurementCoverageCalculator.ExpectedSamplingIntervalSeconds,
                calculated.ExpectedSourceSampleCount,
                calculated.CoveredSourceSampleCount,
                calculated.MissingSourceSampleCount,
                calculated.CoveragePercent,
                calculated.HasData,
                calculated.HasCompleteCoverage,
                calculated.Gaps));

            foreach (var code in calculated.Warnings)
            {
                warnings.Add(new AnalysisWarning(item.Id, code));
            }
        }

        return OperationResult<AnalysisPeriod>.Success(new AnalysisPeriod(
            facility.Id,
            facility.Name,
            from,
            to,
            coverage.Count,
            coverage.Count(item => item.HasData),
            coverage.Count(item => item.HasCompleteCoverage),
            coverage.Count > 0 && coverage.All(item => item.HasData),
            coverage.Count > 0 && coverage.All(item => item.HasCompleteCoverage),
            warnings,
            coverage));
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
                TimestampFormat,
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
