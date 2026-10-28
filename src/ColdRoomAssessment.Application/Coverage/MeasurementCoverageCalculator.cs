namespace ColdRoomAssessment.Application.Coverage;

public static class MeasurementCoverageCalculator
{
    public const int ExpectedSamplingIntervalSeconds = 5;

    private static readonly long TicksPerInterval =
        TimeSpan.FromSeconds(ExpectedSamplingIntervalSeconds).Ticks;

    public static CoverageCalculation Calculate(IReadOnlyList<DateTime> timestamps)
    {
        if (timestamps.Count == 0)
        {
            return new CoverageCalculation(null, null, 0, 0, []);
        }

        var gaps = new List<CoverageGap>();
        var missingTotal = 0;
        for (var index = 1; index < timestamps.Count; index++)
        {
            var deltaTicks = timestamps[index].Ticks - timestamps[index - 1].Ticks;
            if (deltaTicks <= TicksPerInterval)
            {
                continue;
            }

            var missing = (int)((deltaTicks - 1) / TicksPerInterval);
            if (missing <= 0)
            {
                continue;
            }

            missingTotal += missing;
            gaps.Add(new CoverageGap(timestamps[index - 1], timestamps[index], missing));
        }

        return new CoverageCalculation(
            timestamps[0],
            timestamps[^1],
            timestamps.Count,
            missingTotal,
            gaps);
    }
}

public sealed record CoverageCalculation(
    DateTime? FirstMeasurementAt,
    DateTime? LastMeasurementAt,
    int SourceRowCount,
    int MissingSourceSampleCount,
    IReadOnlyList<CoverageGap> Gaps);
