using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application.Coverage;

public static class AnalysisDataAssembler
{
    public static AnalysisEquipmentData Assemble(
        CoverageEquipmentRecord equipment,
        DateTime from,
        DateTime to,
        IReadOnlyList<DateTime> grid,
        IReadOnlyList<AnalysisMeasurementRow> rows)
    {
        var coverage = AnalysisPeriodCalculator.Calculate(
            from,
            to,
            rows.Select(row => row.MeasuredAt).ToArray());
        var rowsByTimestamp = rows
            .GroupBy(row => row.MeasuredAt)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var samples = new List<AnalysisSample>(grid.Count);
        foreach (var timestamp in grid)
        {
            if (!rowsByTimestamp.TryGetValue(timestamp, out var atTimestamp))
            {
                samples.Add(new AnalysisSample(timestamp, null, null, false, false));
                continue;
            }

            var temperature = Values(atTimestamp, MeasurementKind.Temperature);
            var humidity = Values(atTimestamp, MeasurementKind.RelativeHumidity);
            var temperatureAmbiguous = temperature.Count > 1;
            var humidityAmbiguous = humidity.Count > 1;
            var ambiguous = atTimestamp
                .GroupBy(row => row.Kind)
                .Any(group => group.Count() > 1);
            samples.Add(new AnalysisSample(
                timestamp,
                temperatureAmbiguous || temperature.Count != 1 ? null : temperature[0],
                humidityAmbiguous || humidity.Count != 1 ? null : humidity[0],
                true,
                ambiguous));
        }

        return new AnalysisEquipmentData(
            equipment.Id,
            equipment.Name,
            equipment.EquipmentTypeId,
            equipment.EquipmentTypeName,
            equipment.Placement,
            coverage.SourceRowCount,
            rows.Count,
            coverage.CoveragePercent,
            coverage.HasCompleteCoverage,
            samples);
    }

    private static List<double> Values(IReadOnlyList<AnalysisMeasurementRow> rows, MeasurementKind kind)
    {
        var values = new List<double>();
        foreach (var row in rows)
        {
            if (row.Kind == kind)
            {
                values.Add(row.Value);
            }
        }

        return values;
    }
}
