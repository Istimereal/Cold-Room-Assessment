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
            AnalyzeKind(Valid(equipment.Samples, sample => sample.Temperature)),
            AnalyzeKind(Valid(equipment.Samples, sample => sample.RelativeHumidity)));
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

    private static KindAnalysis AnalyzeKind(IReadOnlyList<Reading> readings)
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
                []);
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
            Events(readings));
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

    private static double Round(double value) =>
        (double)decimal.Round((decimal)value, 6, MidpointRounding.AwayFromZero);

    private readonly record struct Reading(DateTime Timestamp, double Value);
}
