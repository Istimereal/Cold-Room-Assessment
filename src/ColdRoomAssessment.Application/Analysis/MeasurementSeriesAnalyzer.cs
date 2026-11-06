using ColdRoomAssessment.Application.Coverage;

namespace ColdRoomAssessment.Application.Analysis;

public static class MeasurementSeriesAnalyzer
{
    public static FacilityAnalysis Analyze(AnalysisData data)
    {
        var equipment = new List<EquipmentAnalysis>(data.Equipment.Count);
        foreach (var item in data.Equipment)
        {
            equipment.Add(AnalyzeEquipment(item));
        }

        return new FacilityAnalysis(
            data.FacilityId,
            data.FacilityName,
            data.From,
            data.To,
            equipment.Count,
            equipment.Count(item => item.Temperature.Count > 0),
            equipment.Count(item => item.RelativeHumidity.Count > 0),
            equipment.Sum(item => item.Temperature.Count),
            equipment.Sum(item => item.RelativeHumidity.Count),
            equipment);
    }

    private static EquipmentAnalysis AnalyzeEquipment(AnalysisEquipmentData equipment)
    {
        var temperature = Valid(equipment.Samples, sample => sample.Temperature);
        var humidity = Valid(equipment.Samples, sample => sample.RelativeHumidity);
        return new EquipmentAnalysis(
            equipment.EquipmentId,
            equipment.EquipmentName,
            equipment.EquipmentTypeId,
            equipment.EquipmentTypeName,
            equipment.Placement,
            equipment.SourceRowCount,
            equipment.MeasurementCount,
            equipment.CoveragePercent,
            equipment.HasCompleteCoverage,
            equipment.Samples.Count(sample => sample.Ambiguous),
            equipment.Samples.Count(sample => !sample.HasSourceSample),
            AnalyzeKind(temperature, []),
            AnalyzeKind(humidity, Events(humidity)),
            TemperatureEvents(temperature));
    }

    private static List<Reading> Valid(
        IReadOnlyList<AnalysisSample> samples,
        Func<AnalysisSample, double?> value)
    {
        var readings = new List<Reading>();
        foreach (var sample in samples)
        {
            if (!sample.HasSourceSample)
            {
                continue;
            }

            var reading = value(sample);
            if (reading is null)
            {
                continue;
            }

            readings.Add(new Reading(sample.Timestamp, reading.Value));
        }

        return readings;
    }

    private static KindAnalysis AnalyzeKind(IReadOnlyList<Reading> readings, IReadOnlyList<SeriesEvent> events)
    {
        if (readings.Count == 0)
        {
            return new KindAnalysis(
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                events);
        }

        var count = readings.Count;
        var sum = 0d;
        var min = readings[0].Value;
        var minAt = readings[0].Timestamp;
        var max = readings[0].Value;
        var maxAt = readings[0].Timestamp;
        foreach (var reading in readings)
        {
            sum += reading.Value;
            if (reading.Value < min)
            {
                min = reading.Value;
                minAt = reading.Timestamp;
            }

            if (reading.Value > max)
            {
                max = reading.Value;
                maxAt = reading.Timestamp;
            }
        }

        var mean = sum / count;
        var sorted = new double[count];
        for (var index = 0; index < count; index++)
        {
            sorted[index] = readings[index].Value;
        }

        Array.Sort(sorted);
        var median = count % 2 == 1
            ? sorted[count / 2]
            : (sorted[count / 2 - 1] + sorted[count / 2]) / 2d;

        var squared = 0d;
        foreach (var reading in readings)
        {
            var delta = reading.Value - mean;
            squared += delta * delta;
        }

        var first = readings[0];
        var last = readings[^1];
        return new KindAnalysis(
            count,
            min,
            minAt,
            max,
            maxAt,
            Round(mean),
            Round(median),
            Round(Math.Sqrt(squared / count)),
            first.Value,
            first.Timestamp,
            last.Value,
            last.Timestamp,
            Round(last.Value - first.Value),
            (last.Timestamp - first.Timestamp).TotalSeconds,
            events);
    }

    private static IReadOnlyList<SeriesEvent> Events(IReadOnlyList<Reading> readings)
    {
        var events = new List<SeriesEvent>();
        for (var index = 1; index < readings.Count - 1; index++)
        {
            var previous = readings[index - 1].Value;
            var current = readings[index];
            var next = readings[index + 1].Value;
            if (current.Value > previous && current.Value > next)
            {
                events.Add(new SeriesEvent(current.Timestamp, current.Value, SeriesEventKind.Peak));
            }
            else if (current.Value < previous && current.Value < next)
            {
                events.Add(new SeriesEvent(current.Timestamp, current.Value, SeriesEventKind.Dip));
            }
        }

        return events;
    }

    // A temperature move is one fact once it reaches 0.5 °C. Same-direction steps keep accumulating
    // while a new extreme arrives within five minutes. A five-minute pause, or a 0.5 °C reversal, ends it.
    private const double TemperatureSignificance = 0.5;
    private static readonly TimeSpan TemperatureTrendWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinimumStableDuration = TimeSpan.FromMinutes(5);

    private enum TemperatureTrend
    {
        Search,
        Rising,
        Falling
    }

    private readonly record struct MoveSpan(int Start, int End);

    private static IReadOnlyList<TemperatureEvent> TemperatureEvents(IReadOnlyList<Reading> readings)
    {
        if (readings.Count == 0)
        {
            return [];
        }

        var moves = SignificantMoves(readings);
        var stable = StablePeriods(readings, moves);
        var combined = new List<TemperatureEvent>(moves.Count + stable.Count);
        foreach (var move in moves)
        {
            combined.Add(move.Event);
        }

        combined.AddRange(stable);
        combined.Sort(static (left, right) =>
        {
            var from = left.From.CompareTo(right.From);
            return from != 0 ? from : left.To.CompareTo(right.To);
        });
        return combined;
    }

    private readonly record struct IndexedMove(MoveSpan Span, TemperatureEvent Event);

    private static List<IndexedMove> SignificantMoves(IReadOnlyList<Reading> readings)
    {
        var moves = new List<IndexedMove>();
        var pivot = 0;
        var high = 0;
        var low = 0;
        var extreme = 0;
        var trend = TemperatureTrend.Search;
        var index = 1;
        while (index < readings.Count)
        {
            var value = readings[index].Value;
            if (trend == TemperatureTrend.Search)
            {
                if (value > readings[high].Value)
                {
                    high = index;
                }

                if (value < readings[low].Value)
                {
                    low = index;
                }

                if (Reached(readings[high].Value - readings[pivot].Value) && high > pivot)
                {
                    trend = TemperatureTrend.Rising;
                    extreme = high;
                }
                else if (Reached(readings[pivot].Value - readings[low].Value) && low > pivot)
                {
                    trend = TemperatureTrend.Falling;
                    extreme = low;
                }
                else if (Stalled(readings, index, high) && Stalled(readings, index, low))
                {
                    pivot = index;
                    high = index;
                    low = index;
                }

                index++;
                continue;
            }

            if (trend == TemperatureTrend.Rising)
            {
                if (value > readings[extreme].Value)
                {
                    extreme = index;
                    index++;
                    continue;
                }

                if (Reached(readings[extreme].Value - value) || Stalled(readings, index, extreme))
                {
                    moves.Add(CreateMove(readings, pivot, extreme, TemperatureEventKind.Rising));
                    pivot = extreme;
                    high = extreme;
                    low = extreme;
                    trend = TemperatureTrend.Search;
                    index = extreme + 1;
                    continue;
                }

                index++;
                continue;
            }

            if (value < readings[extreme].Value)
            {
                extreme = index;
                index++;
                continue;
            }

            if (Reached(value - readings[extreme].Value) || Stalled(readings, index, extreme))
            {
                moves.Add(CreateMove(readings, pivot, extreme, TemperatureEventKind.Falling));
                pivot = extreme;
                high = extreme;
                low = extreme;
                trend = TemperatureTrend.Search;
                index = extreme + 1;
                continue;
            }

            index++;
        }

        if (trend == TemperatureTrend.Rising && Reached(readings[extreme].Value - readings[pivot].Value))
        {
            moves.Add(CreateMove(readings, pivot, extreme, TemperatureEventKind.Rising));
        }
        else if (trend == TemperatureTrend.Falling && Reached(readings[pivot].Value - readings[extreme].Value))
        {
            moves.Add(CreateMove(readings, pivot, extreme, TemperatureEventKind.Falling));
        }

        return moves;
    }

    private static List<TemperatureEvent> StablePeriods(IReadOnlyList<Reading> readings, List<IndexedMove> moves)
    {
        var periods = new List<TemperatureEvent>();
        var cursor = 0;
        foreach (var move in moves)
        {
            if (move.Span.Start >= cursor)
            {
                AppendStable(readings, cursor, move.Span.Start, periods);
            }

            cursor = move.Span.End;
        }

        if (cursor <= readings.Count - 1)
        {
            AppendStable(readings, cursor, readings.Count - 1, periods);
        }

        return periods;
    }

    private static void AppendStable(
        IReadOnlyList<Reading> readings,
        int start,
        int end,
        List<TemperatureEvent> periods)
    {
        if (end < start)
        {
            return;
        }

        var segmentStart = start;
        var min = readings[start].Value;
        var max = readings[start].Value;
        var sum = readings[start].Value;
        var count = 1;
        for (var index = start + 1; index <= end; index++)
        {
            var value = readings[index].Value;
            var nextMin = Math.Min(min, value);
            var nextMax = Math.Max(max, value);
            if (nextMax - nextMin < TemperatureSignificance)
            {
                min = nextMin;
                max = nextMax;
                sum += value;
                count++;
                continue;
            }

            AddStable(readings, segmentStart, index - 1, min, max, sum, count, periods);
            segmentStart = index;
            min = value;
            max = value;
            sum = value;
            count = 1;
        }

        AddStable(readings, segmentStart, end, min, max, sum, count, periods);
    }

    private static void AddStable(
        IReadOnlyList<Reading> readings,
        int start,
        int end,
        double min,
        double max,
        double sum,
        int count,
        List<TemperatureEvent> periods)
    {
        if (count < 2 || end <= start)
        {
            return;
        }

        var duration = (readings[end].Timestamp - readings[start].Timestamp).TotalSeconds;
        if (duration < MinimumStableDuration.TotalSeconds)
        {
            return;
        }

        periods.Add(new TemperatureEvent(
            readings[start].Timestamp,
            readings[end].Timestamp,
            readings[start].Value,
            readings[end].Value,
            Round(readings[end].Value - readings[start].Value),
            duration,
            TemperatureEventKind.Stable,
            min,
            max,
            Round(sum / count)));
    }

    private static IndexedMove CreateMove(
        IReadOnlyList<Reading> readings,
        int start,
        int end,
        TemperatureEventKind kind)
    {
        var origin = readings[start].Value;
        while (start < end && Math.Abs(readings[start + 1].Value - origin) <= 1e-9)
        {
            start++;
        }

        var min = readings[start].Value;
        var max = min;
        var sum = 0d;
        var count = 0;
        for (var index = start; index <= end; index++)
        {
            var value = readings[index].Value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
            sum += value;
            count++;
        }

        var startValue = readings[start].Value;
        var endValue = readings[end].Value;
        var temperatureEvent = new TemperatureEvent(
            readings[start].Timestamp,
            readings[end].Timestamp,
            startValue,
            endValue,
            Round(endValue - startValue),
            (readings[end].Timestamp - readings[start].Timestamp).TotalSeconds,
            kind,
            min,
            max,
            Round(sum / count));
        return new IndexedMove(new MoveSpan(start, end), temperatureEvent);
    }

    private static bool Reached(double delta) => delta >= TemperatureSignificance - 1e-9;

    private static bool Stalled(IReadOnlyList<Reading> readings, int index, int since) =>
        readings[index].Timestamp - readings[since].Timestamp > TemperatureTrendWindow;

    private static double Round(double value) =>
        (double)decimal.Round((decimal)value, 6, MidpointRounding.AwayFromZero);

    private readonly record struct Reading(DateTime Timestamp, double Value);
}
