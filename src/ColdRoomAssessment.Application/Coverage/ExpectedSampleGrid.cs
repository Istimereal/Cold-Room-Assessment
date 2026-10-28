namespace ColdRoomAssessment.Application.Coverage;

public static class ExpectedSampleGrid
{
    public static IReadOnlyList<DateTime> Create(DateTime from, DateTime to)
    {
        var ticksPerInterval = TimeSpan.FromSeconds(
            MeasurementCoverageCalculator.ExpectedSamplingIntervalSeconds).Ticks;
        var grid = new List<DateTime>();
        for (var offset = 0L; ; offset++)
        {
            var time = from.AddTicks(offset * ticksPerInterval);
            if (time >= to)
            {
                break;
            }

            grid.Add(time);
        }

        return grid;
    }
}
