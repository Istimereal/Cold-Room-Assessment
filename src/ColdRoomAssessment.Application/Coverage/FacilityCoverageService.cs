namespace ColdRoomAssessment.Application.Coverage;

public sealed class FacilityCoverageService(IFacilityCoverageStore store)
{
    public async Task<OperationResult<FacilityCoverage>> GetAsync(
        Guid facilityId,
        CancellationToken cancellationToken)
    {
        var facility = await store.FindFacilityAsync(facilityId, cancellationToken);
        if (facility is null)
        {
            return OperationResult<FacilityCoverage>.NotFound("Facility was not found.");
        }

        var equipment = await store.ListEquipmentAsync(facilityId, cancellationToken);
        var facts = await store.ListMeasurementFactsAsync(facilityId, cancellationToken);
        var factsByEquipment = facts.ToDictionary(item => item.EquipmentId);

        var coverage = new List<EquipmentCoverage>(equipment.Count);
        foreach (var item in equipment)
        {
            factsByEquipment.TryGetValue(item.Id, out var fact);
            var calculated = MeasurementCoverageCalculator.Calculate(fact?.Timestamps ?? []);
            coverage.Add(new EquipmentCoverage(
                item.Id,
                item.Name,
                item.EquipmentTypeId,
                item.EquipmentTypeName,
                item.Placement,
                calculated.FirstMeasurementAt,
                calculated.LastMeasurementAt,
                calculated.SourceRowCount,
                fact?.MeasurementCount ?? 0,
                fact?.ImportCount ?? 0,
                MeasurementCoverageCalculator.ExpectedSamplingIntervalSeconds,
                calculated.MissingSourceSampleCount,
                calculated.Gaps));
        }

        var withData = coverage.Where(item => item.SourceRowCount > 0).ToArray();
        var (hasCommonOverlap, overlapStart, overlapEnd) = CommonOverlap(withData);

        return OperationResult<FacilityCoverage>.Success(new FacilityCoverage(
            facility.Id,
            facility.Name,
            withData.Length == 0 ? null : withData.Min(item => item.FirstMeasurementAt),
            withData.Length == 0 ? null : withData.Max(item => item.LastMeasurementAt),
            hasCommonOverlap,
            overlapStart,
            overlapEnd,
            coverage));
    }

    private static (bool HasCommonOverlap, DateTime? Start, DateTime? End) CommonOverlap(
        IReadOnlyList<EquipmentCoverage> withData)
    {
        if (withData.Count == 0)
        {
            return (false, null, null);
        }

        var start = withData.Max(item => item.FirstMeasurementAt!.Value);
        var end = withData.Min(item => item.LastMeasurementAt!.Value);
        if (start <= end)
        {
            return (true, start, end);
        }

        return (false, null, null);
    }
}
