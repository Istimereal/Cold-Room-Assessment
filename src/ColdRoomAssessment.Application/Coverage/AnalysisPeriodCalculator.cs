namespace ColdRoomAssessment.Application.Coverage;

public static class AnalysisPeriodCalculator
{
    private static readonly long TicksPerInterval =
        TimeSpan.FromSeconds(MeasurementCoverageCalculator.ExpectedSamplingIntervalSeconds).Ticks;

    public static AnalysisCalculation Calculate(
        DateTime from,
        DateTime to,
        IReadOnlyList<DateTime> timestamps)
    {
        var storedTimes = timestamps.Distinct().OrderBy(time => time).ToArray();
        var grid = BuildGrid(from, to);
        var stored = new HashSet<DateTime>(storedTimes);
        var coveredIndexes = new List<int>();
        for (var index = 0; index < grid.Count; index++)
        {
            if (stored.Contains(grid[index]))
            {
                coveredIndexes.Add(index);
            }
        }

        var expected = grid.Count;
        var coveredCount = coveredIndexes.Count;
        var missing = expected - coveredCount;
        var percent = expected == 0
            ? 0m
            : decimal.Round(coveredCount * 100m / expected, 1, MidpointRounding.AwayFromZero);
        var hasData = storedTimes.Length > 0;
        var complete = expected > 0 && coveredCount == expected;

        if (!hasData)
        {
            return new AnalysisCalculation(
                null,
                null,
                0,
                expected,
                0,
                missing,
                percent,
                false,
                false,
                [AnalysisWarningCode.NoData],
                []);
        }

        var first = storedTimes[0];
        var last = storedTimes[^1];
        if (coveredCount == 0)
        {
            return new AnalysisCalculation(
                first,
                last,
                storedTimes.Length,
                expected,
                0,
                missing,
                percent,
                true,
                false,
                [],
                [new AnalysisGap(from, to, missing, AnalysisGapKind.FullInterval)]);
        }

        var warnings = new List<AnalysisWarningCode>();
        var gaps = new List<AnalysisGap>();
        var firstCoveredIndex = coveredIndexes[0];
        if (firstCoveredIndex > 0)
        {
            warnings.Add(AnalysisWarningCode.StartsAfterFrom);
            gaps.Add(new AnalysisGap(
                from,
                grid[firstCoveredIndex],
                firstCoveredIndex,
                AnalysisGapKind.Leading));
        }

        var internalMissing = 0;
        for (var index = 1; index < coveredIndexes.Count; index++)
        {
            var previous = coveredIndexes[index - 1];
            var current = coveredIndexes[index];
            var between = current - previous - 1;
            if (between <= 0)
            {
                continue;
            }

            internalMissing += between;
            gaps.Add(new AnalysisGap(grid[previous], grid[current], between, AnalysisGapKind.Internal));
        }

        if (internalMissing > 0)
        {
            warnings.Add(AnalysisWarningCode.InternalGap);
        }

        var lastCoveredIndex = coveredIndexes[^1];
        var trailing = expected - lastCoveredIndex - 1;
        if (trailing > 0)
        {
            warnings.Add(AnalysisWarningCode.EndsBeforeTo);
            gaps.Add(new AnalysisGap(grid[lastCoveredIndex], to, trailing, AnalysisGapKind.Trailing));
        }

        return new AnalysisCalculation(
            first,
            last,
            storedTimes.Length,
            expected,
            coveredCount,
            missing,
            percent,
            true,
            complete,
            warnings,
            gaps);
    }

    private static List<DateTime> BuildGrid(DateTime from, DateTime to)
    {
        var grid = new List<DateTime>();
        for (var offset = 0L; ; offset++)
        {
            var time = from.AddTicks(offset * TicksPerInterval);
            if (time >= to)
            {
                break;
            }

            grid.Add(time);
        }

        return grid;
    }
}

public sealed record AnalysisCalculation(
    DateTime? FirstMeasurementAt,
    DateTime? LastMeasurementAt,
    int SourceRowCount,
    int ExpectedSourceSampleCount,
    int CoveredSourceSampleCount,
    int MissingSourceSampleCount,
    decimal CoveragePercent,
    bool HasData,
    bool HasCompleteCoverage,
    IReadOnlyList<AnalysisWarningCode> Warnings,
    IReadOnlyList<AnalysisGap> Gaps);
