using System.Diagnostics;
using System.Globalization;
using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Application.Investigation;
using ColdRoomAssessment.Infrastructure;

namespace ColdRoomAssessment.IntegrationTests.Analysis;

public sealed class TemperatureSeriesTests
{
    private static readonly DateTime Start = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Gradual_rise_is_one_rising_event()
    {
        var equipment = Analyze(Series(60, 3.0, 3.05, 3.1, 3.15, 3.2, 3.25, 3.3, 3.35, 3.4, 3.45, 3.5));

        var rising = Assert.Single(equipment.TemperatureEvents);
        Assert.Equal(TemperatureEventKind.Rising, rising.Kind);
        Assert.Equal(Start, rising.From);
        Assert.Equal(Start.AddMinutes(10), rising.To);
        Assert.Equal(3, rising.StartValue);
        Assert.Equal(3.5, rising.EndValue);
        Assert.Equal(0.5, rising.Change);
        Assert.Equal(600, rising.DurationSeconds);
        Assert.Empty(equipment.Temperature.Events);
        Assert.Equal(11, equipment.Temperature.Count);
        Assert.Equal(3, equipment.Temperature.First);
        Assert.Equal(3.5, equipment.Temperature.Last);
        Assert.Equal(0.5, equipment.Temperature.Change);
        Assert.Equal(3.25, equipment.Temperature.Mean);
        Assert.Equal(3.25, equipment.Temperature.Median);
    }

    [Fact]
    public void Gradual_fall_is_one_falling_event()
    {
        var equipment = Analyze(Series(60, 3.5, 3.45, 3.4, 3.35, 3.3, 3.25, 3.2, 3.15, 3.1, 3.05, 3.0));

        var falling = Assert.Single(equipment.TemperatureEvents);
        Assert.Equal(TemperatureEventKind.Falling, falling.Kind);
        Assert.Equal(3.5, falling.StartValue);
        Assert.Equal(3, falling.EndValue);
        Assert.Equal(-0.5, falling.Change);
        Assert.Equal(Start, falling.From);
        Assert.Equal(Start.AddMinutes(10), falling.To);
        Assert.Equal(600, falling.DurationSeconds);
    }

    [Fact]
    public void Oscillation_of_a_tenth_is_not_significant()
    {
        var values = new double[25];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = index % 2 == 0 ? 3.0 : 3.1;
        }

        var equipment = Analyze(Series(5, values));

        Assert.Empty(equipment.TemperatureEvents);
        Assert.Equal(25, equipment.Temperature.Count);
    }

    [Fact]
    public void Same_direction_steps_within_five_minutes_accumulate()
    {
        var equipment = Analyze(Series(60, 3.0, 3.2, 3.3, 3.5));

        var rising = Assert.Single(equipment.TemperatureEvents);
        Assert.Equal(TemperatureEventKind.Rising, rising.Kind);
        Assert.Equal(3, rising.StartValue);
        Assert.Equal(3.5, rising.EndValue);
        Assert.Equal(0.5, rising.Change);
        Assert.Equal(180, rising.DurationSeconds);
    }

    [Fact]
    public void Same_direction_steps_separated_by_more_than_five_minutes_do_not_accumulate()
    {
        var equipment = Analyze(Points(
            (0, 3.0),
            (60, 3.3),
            (400, 3.3),
            (460, 3.6)));

        Assert.DoesNotContain(equipment.TemperatureEvents, item => item.Kind != TemperatureEventKind.Stable);
        var stable = Assert.Single(equipment.TemperatureEvents);
        Assert.Equal(TemperatureEventKind.Stable, stable.Kind);
        Assert.Equal(400, stable.DurationSeconds);
    }

    [Fact]
    public void Forty_minute_stable_stretch_is_one_period()
    {
        var equipment = Analyze(Constant(TimeSpan.FromMinutes(40), 3.2));

        var stable = Assert.Single(equipment.TemperatureEvents);
        Assert.Equal(TemperatureEventKind.Stable, stable.Kind);
        Assert.Equal(Start, stable.From);
        Assert.Equal(Start.AddMinutes(40), stable.To);
        Assert.Equal(2400, stable.DurationSeconds);
        Assert.Equal(3.2, stable.Min);
        Assert.Equal(3.2, stable.Max);
        Assert.Equal(3.2, stable.Mean);
        Assert.Equal(481, equipment.Temperature.Count);
    }

    [Fact]
    public void Stable_shorter_than_five_minutes_is_omitted()
    {
        var equipment = Analyze(Constant(TimeSpan.FromSeconds(295), 3.2));

        Assert.Empty(equipment.TemperatureEvents);
        Assert.Equal(60, equipment.Temperature.Count);
    }

    [Fact]
    public void Stable_of_exactly_five_minutes_is_kept()
    {
        var equipment = Analyze(Constant(TimeSpan.FromMinutes(5), 3.2));

        var stable = Assert.Single(equipment.TemperatureEvents);
        Assert.Equal(300, stable.DurationSeconds);
    }

    [Fact]
    public void Significant_excursion_splits_a_stable_period()
    {
        var samples = new List<(int Seconds, double Value)>();
        for (var second = 0; second <= 600; second += 5)
        {
            samples.Add((second, 3));
        }

        for (var step = 1; step <= 12; step++)
        {
            samples.Add((600 + step * 5, 3 + step / 12d));
        }

        for (var second = 665; second <= 1260; second += 5)
        {
            samples.Add((second, 4));
        }

        var equipment = Analyze(Points(samples.ToArray()));
        var rising = Assert.Single(equipment.TemperatureEvents, item => item.Kind == TemperatureEventKind.Rising);
        var stables = equipment.TemperatureEvents.Where(item => item.Kind == TemperatureEventKind.Stable).ToList();

        Assert.Equal(2, stables.Count);
        Assert.Equal(3, rising.StartValue);
        Assert.Equal(4, rising.EndValue);
        Assert.Equal(1, rising.Change);
        Assert.Equal(Start.AddMinutes(10), rising.From);
        Assert.Equal(Start.AddMinutes(11), rising.To);
        Assert.Equal(600, stables[0].DurationSeconds);
        Assert.Equal(600, stables[1].DurationSeconds);
        Assert.Equal(rising.From, stables[0].To);
        Assert.Equal(rising.To, stables[1].From);
    }

    [Fact]
    public void Summary_statistics_ignore_missing_and_ambiguous_samples()
    {
        var equipment = Analyze(new[]
        {
            Sample(0, 1, true, false),
            Sample(5, null, false, false),
            Sample(10, null, true, true),
            Sample(15, 2, true, false),
            Sample(20, 4, true, false)
        });

        Assert.Equal(3, equipment.Temperature.Count);
        Assert.Equal(1, equipment.MissingExpectedSampleCount);
        Assert.Equal(1, equipment.AmbiguousSampleCount);
        Assert.Equal(1, equipment.Temperature.Min);
        Assert.Equal(Start, equipment.Temperature.MinAt);
        Assert.Equal(4, equipment.Temperature.Max);
        Assert.Equal(Start.AddSeconds(20), equipment.Temperature.MaxAt);
        Assert.Equal(2.333333, equipment.Temperature.Mean);
        Assert.Equal(2, equipment.Temperature.Median);
        Assert.Equal(1.247219, equipment.Temperature.PopulationStdDev);
        Assert.Equal(1, equipment.Temperature.First);
        Assert.Equal(4, equipment.Temperature.Last);
        Assert.Equal(3, equipment.Temperature.Change);
        Assert.Equal(20, equipment.Temperature.DurationSeconds);
        Assert.DoesNotContain(equipment.TemperatureEvents, item => item.From == Start.AddSeconds(5) || item.From == Start.AddSeconds(10));
        var rising = Assert.Single(equipment.TemperatureEvents);
        Assert.Equal(Start, rising.From);
        Assert.Equal(Start.AddSeconds(20), rising.To);
        Assert.Equal(1, rising.StartValue);
        Assert.Equal(4, rising.EndValue);
    }

    [Fact]
    public void Humidity_keeps_neighbouring_peaks_and_dips()
    {
        var samples = new (int Second, double Value)[]
        {
            (0, 100), (5, 1), (10, 80), (20, 2), (25, 50), (30, 50), (35, 0), (40, 10)
        };
        var equipment = Analyze(samples.Select(item => Humidity(item.Second, item.Value)).ToArray());

        Assert.Equal(
            [
                (Start.AddSeconds(5), 1d, SeriesEventKind.Dip),
                (Start.AddSeconds(10), 80d, SeriesEventKind.Peak),
                (Start.AddSeconds(20), 2d, SeriesEventKind.Dip),
                (Start.AddSeconds(35), 0d, SeriesEventKind.Dip)
            ],
            equipment.RelativeHumidity.Events.Select(item => (item.Timestamp, item.Value, item.Kind)));
        Assert.Empty(equipment.TemperatureEvents);
    }

    [Fact]
    public void Evidence_ids_are_unique_and_the_validator_accepts_a_real_rise()
    {
        var equipment = Analyze(Series(60, 3.0, 3.05, 3.1, 3.15, 3.2, 3.25, 3.3, 3.35, 3.4, 3.45, 3.5));
        var analysis = Analysis(equipment);
        var catalog = EvidenceCatalog.FromAnalysis(analysis);
        var rise = Assert.Single(catalog.Items, item => item.Fact == "rise:1");

        Assert.Equal($"eq:{equipment.EquipmentId:D}:temperature:rise:1", rise.Id);
        Assert.Contains("start=3", rise.Value, StringComparison.Ordinal);
        Assert.Contains("end=3.5", rise.Value, StringComparison.Ordinal);
        Assert.Contains("change=+0.5", rise.Value, StringComparison.Ordinal);
        Assert.Equal(catalog.Items.Count, catalog.Items.Select(item => item.Id).Distinct().Count());
        Assert.True(catalog.Contains(rise.Id));
        Assert.False(catalog.Contains($"eq:{equipment.EquipmentId:D}:temperature:peak:1"));
        Assert.True(InvestigationResultValidator.TryCreate(Result(equipment.EquipmentId, rise.Id, rise.Timestamp!), analysis, catalog, out _));
        Assert.False(InvestigationResultValidator.TryCreate(
            Result(equipment.EquipmentId, $"eq:{equipment.EquipmentId:D}:temperature:peak:1", rise.Timestamp!),
            analysis,
            catalog,
            out _));
    }

    [Fact]
    public void Eighteen_hour_five_second_series_stays_compact()
    {
        const int sampleCount = 18 * 3600 / 5 + 1;
        var samples = new AnalysisSample[sampleCount];
        for (var index = 0; index < sampleCount; index++)
        {
            samples[index] = Sample(index * 5, TemperatureAt(index * 5), true, false);
        }

        var stopwatch = Stopwatch.StartNew();
        var equipment = Analyze(samples);
        stopwatch.Stop();

        Assert.Equal(sampleCount, equipment.Temperature.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), stopwatch.Elapsed.ToString());
        Assert.Equal(1, equipment.TemperatureEvents.Count(item => item.Kind == TemperatureEventKind.Rising));
        Assert.Equal(1, equipment.TemperatureEvents.Count(item => item.Kind == TemperatureEventKind.Falling));
        Assert.Equal(3, equipment.TemperatureEvents.Count(item => item.Kind == TemperatureEventKind.Stable));

        var analysis = Analysis(equipment);
        var catalog = EvidenceCatalog.FromAnalysis(analysis);
        Assert.Equal(31, catalog.Items.Count);
        var temperatureFacts = catalog.Items.Where(item => item.MeasurementKind == "Temperature").ToList();
        Assert.Equal(18, temperatureFacts.Count);
        Assert.Equal(2, temperatureFacts.Count(item => item.Fact.StartsWith("rise:", StringComparison.Ordinal) || item.Fact.StartsWith("fall:", StringComparison.Ordinal)));
        Assert.Equal(3, temperatureFacts.Count(item => item.Fact.StartsWith("stable:", StringComparison.Ordinal)));
        Assert.DoesNotContain(catalog.Items, item => item.Fact.StartsWith("peak:", StringComparison.Ordinal) || item.Fact.StartsWith("dip:", StringComparison.Ordinal));
        Assert.Equal(catalog.Items.Count, catalog.Items.Select(item => item.Id).Distinct().Count());
        Assert.Contains(catalog.Items, item => item.Id.EndsWith(":temperature:rise:1", StringComparison.Ordinal));
        Assert.Contains(catalog.Items, item => item.Id.EndsWith(":temperature:fall:1", StringComparison.Ordinal));
        Assert.Contains(catalog.Items, item => item.Id.EndsWith(":temperature:stable:1", StringComparison.Ordinal));

        var legacy = LegacyExtrema(samples);
        Assert.True(legacy > 1000, legacy.ToString(CultureInfo.InvariantCulture));
        Assert.True(equipment.TemperatureEvents.Count < legacy / 100);

        var payloadBytes = OpenAIInvestigatorClient.InvestigationPayloadBytes(catalog);
        Assert.True(payloadBytes < 250_000, payloadBytes.ToString(CultureInfo.InvariantCulture));
    }

    private static double TemperatureAt(int seconds)
    {
        const int riseStart = 6 * 3600;
        const int riseEnd = riseStart + 20 * 60;
        const int fallStart = 12 * 3600;
        const int fallEnd = fallStart + 15 * 60;
        if (seconds <= riseStart)
        {
            return seconds / 5 % 2 == 0 ? 3.25 : 3.15;
        }

        if (seconds <= riseEnd)
        {
            return 3.2 + (seconds - riseStart) / (double)(riseEnd - riseStart);
        }

        if (seconds <= fallStart)
        {
            return seconds / 5 % 2 == 0 ? 4.25 : 4.15;
        }

        if (seconds <= fallEnd)
        {
            return 4.2 - 0.8 * (seconds - fallStart) / (double)(fallEnd - fallStart);
        }

        return seconds / 5 % 2 == 0 ? 3.45 : 3.35;
    }

    private static int LegacyExtrema(IReadOnlyList<AnalysisSample> samples)
    {
        var values = samples.Where(sample => sample.HasSourceSample && sample.Temperature is not null).Select(sample => sample.Temperature!.Value).ToList();
        var count = 0;
        for (var index = 1; index < values.Count - 1; index++)
        {
            if (values[index] > values[index - 1] && values[index] > values[index + 1])
            {
                count++;
            }
            else if (values[index] < values[index - 1] && values[index] < values[index + 1])
            {
                count++;
            }
        }

        return count;
    }

    private static string Result(Guid equipmentId, string evidenceId, string timestamp) =>
        $$"""
        {
          "overallSummary": "Temperature rose.",
          "observations": [
            {
              "id": "obs-1",
              "title": "Rise",
              "description": "The temperature rose.",
              "equipmentIds": ["{{equipmentId:D}}"],
              "measurementKind": "Temperature",
              "from": "{{timestamp}}",
              "to": "{{timestamp}}",
              "evidenceReferenceIds": ["{{evidenceId}}"],
              "confidence": "Medium"
            }
          ],
          "hypotheses": [],
          "limitations": []
        }
        """;

    private static EquipmentAnalysis Analyze(params AnalysisSample[] samples)
    {
        var equipment = new AnalysisEquipmentData(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "SENSOR01",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Sensor",
            "Near entrance",
            samples.Length,
            samples.Count(sample => sample.HasSourceSample),
            100,
            true,
            samples);
        return Analysis(equipment).Equipment[0];
    }

    private static FacilityAnalysis Analysis(EquipmentAnalysis equipment) =>
        new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Cold Room A",
            equipment.Temperature.FirstAt ?? Start,
            equipment.Temperature.LastAt ?? Start,
            1,
            equipment.Temperature.Count > 0 ? 1 : 0,
            equipment.RelativeHumidity.Count > 0 ? 1 : 0,
            equipment.Temperature.Count,
            equipment.RelativeHumidity.Count,
            [equipment]);

    private static FacilityAnalysis Analysis(AnalysisEquipmentData equipment) =>
        MeasurementSeriesAnalyzer.Analyze(new AnalysisData(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Cold Room A",
        equipment.Samples.Count == 0 ? Start : equipment.Samples[0].Timestamp,
        equipment.Samples.Count == 0 ? Start : equipment.Samples[^1].Timestamp,
            5,
            equipment.Samples.Count,
            [equipment]));

    private static AnalysisSample[] Series(int stepSeconds, params double[] values) =>
        values.Select((value, index) => Sample(index * stepSeconds, value, true, false)).ToArray();

    private static AnalysisSample[] Points(params (int Seconds, double Value)[] points) =>
        points.Select(point => Sample(point.Seconds, point.Value, true, false)).ToArray();

    private static AnalysisSample[] Constant(TimeSpan duration, double value)
    {
        var count = (int)duration.TotalSeconds / 5 + 1;
        var samples = new AnalysisSample[count];
        for (var index = 0; index < count; index++)
        {
            samples[index] = Sample(index * 5, value, true, false);
        }

        return samples;
    }

    private static AnalysisSample Sample(int seconds, double? temperature, bool hasSource, bool ambiguous) =>
        new(Start.AddSeconds(seconds), temperature, null, hasSource, ambiguous);

    private static AnalysisSample Humidity(int seconds, double value) =>
        new(Start.AddSeconds(seconds), null, value, true, false);
}
