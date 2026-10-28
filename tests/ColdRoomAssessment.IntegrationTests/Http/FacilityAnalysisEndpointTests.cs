using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Domain;
using ColdRoomAssessment.Infrastructure;
using ColdRoomAssessment.IntegrationTests.Setup;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.IntegrationTests.Http;

[Collection(IntegrationCollection.Name)]
public sealed class FacilityAnalysisEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private static readonly JsonSerializerOptions Json = CreateJson();

    private readonly PostgresFixture _postgres = postgres;

    [Fact]
    public async Task Known_temperature_and_humidity_series_keep_their_own_statistics()
    {
        var sensor = Equipment("SENSOR01", "Near entrance, upper shelf");
        Add(sensor, At(0, 0), MeasurementKind.Temperature, 1);
        Add(sensor, At(0, 0), MeasurementKind.RelativeHumidity, 10);
        Add(sensor, At(0, 5), MeasurementKind.Temperature, 2);
        Add(sensor, At(0, 5), MeasurementKind.RelativeHumidity, 20);
        Add(sensor, At(0, 10), MeasurementKind.Temperature, 3);
        Add(sensor, At(0, 10), MeasurementKind.RelativeHumidity, 30);
        Add(sensor, At(0, 15), MeasurementKind.Temperature, 4);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var analysis = await GetAnalysisAsync(facilityId, At(0, 0), At(0, 20));
        var equipment = Assert.Single(analysis.Equipment);

        Assert.Equal(facilityId, analysis.FacilityId);
        Assert.Equal("Cold Room A", analysis.FacilityName);
        Assert.Equal(At(0, 0), analysis.From);
        Assert.Equal(DateTimeKind.Unspecified, analysis.From.Kind);
        Assert.Equal(At(0, 20), analysis.To);
        Assert.Equal(1, analysis.EquipmentCount);
        Assert.Equal(1, analysis.EquipmentWithTemperatureCount);
        Assert.Equal(1, analysis.EquipmentWithRelativeHumidityCount);
        Assert.Equal(4, analysis.ValidTemperatureCount);
        Assert.Equal(3, analysis.ValidRelativeHumidityCount);
        Assert.Equal(sensor.Id, equipment.EquipmentId);
        Assert.Equal("SENSOR01", equipment.EquipmentName);
        Assert.Equal("DHT22 Logger", equipment.EquipmentTypeName);
        Assert.Equal("Near entrance, upper shelf", equipment.Placement);
        Assert.Equal(4, equipment.SourceRowCount);
        Assert.Equal(7, equipment.MeasurementCount);
        Assert.Equal(100.0m, equipment.CoveragePercent);
        Assert.True(equipment.HasCompleteCoverage);
        Assert.Equal(0, equipment.AmbiguousSampleCount);
        Assert.Equal(0, equipment.MissingExpectedSampleCount);

        AssertKind(equipment.Temperature, 4, 1, At(0, 0), 4, At(0, 15), 2.5, 2.5, 1.118034, 1, At(0, 0), 4, At(0, 15), 3, 15);
        Assert.Empty(equipment.Temperature.Events);
        AssertKind(equipment.RelativeHumidity, 3, 10, At(0, 0), 30, At(0, 10), 20, 20, 8.164966, 10, At(0, 0), 30, At(0, 10), 20, 10);
        Assert.Empty(equipment.RelativeHumidity.Events);
    }

    [Fact]
    public async Task Equal_extremes_use_the_earliest_timestamp()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        AddTemps(sensor, (At(0, 0), 5), (At(0, 5), 1), (At(0, 10), 3), (At(0, 15), 5), (At(0, 20), 1));
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var temperature = (await GetAnalysisAsync(facilityId, At(0, 0), At(0, 25))).Equipment[0].Temperature;

        Assert.Equal(1, temperature.Min);
        Assert.Equal(At(0, 5), temperature.MinAt);
        Assert.Equal(5, temperature.Max);
        Assert.Equal(At(0, 0), temperature.MaxAt);
    }

    [Fact]
    public async Task One_valid_value_has_zero_spread_and_no_events()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        Add(sensor, At(0, 0), MeasurementKind.Temperature, 4.25);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var temperature = (await GetAnalysisAsync(facilityId, At(0, 0), At(0, 5))).Equipment[0].Temperature;

        AssertKind(temperature, 1, 4.25, At(0, 0), 4.25, At(0, 0), 4.25, 4.25, 0, 4.25, At(0, 0), 4.25, At(0, 0), 0, 0);
        Assert.Empty(temperature.Events);
    }

    [Fact]
    public async Task No_valid_values_leave_every_statistic_null()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        Add(sensor, At(0, 0), MeasurementKind.Temperature, 4);
        Add(sensor, At(0, 0), MeasurementKind.Temperature, 5);
        Add(sensor, At(0, 5), MeasurementKind.Temperature, 1);
        Add(sensor, At(0, 5), MeasurementKind.Temperature, 2);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var analysis = await GetAnalysisAsync(facilityId, At(0, 0), At(0, 10));
        var equipment = Assert.Single(analysis.Equipment);

        Assert.Equal(0, analysis.EquipmentWithTemperatureCount);
        Assert.Equal(0, analysis.ValidTemperatureCount);
        Assert.Equal(2, equipment.AmbiguousSampleCount);
        Assert.Equal(0, equipment.MissingExpectedSampleCount);
        Assert.Equal(100.0m, equipment.CoveragePercent);
        AssertEmpty(equipment.Temperature);
        AssertEmpty(equipment.RelativeHumidity);
    }

    [Fact]
    public async Task Missing_grid_samples_are_excluded_from_the_series()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        AddTemps(sensor, (At(0, 0), 1), (At(0, 10), 3), (At(0, 20), 5));
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var analysis = await GetAnalysisAsync(facilityId, At(0, 0), At(0, 25));
        var equipment = Assert.Single(analysis.Equipment);

        Assert.Equal(3, equipment.SourceRowCount);
        Assert.Equal(3, equipment.MeasurementCount);
        Assert.Equal(60.0m, equipment.CoveragePercent);
        Assert.False(equipment.HasCompleteCoverage);
        Assert.Equal(2, equipment.MissingExpectedSampleCount);
        Assert.Equal(0, equipment.AmbiguousSampleCount);
        AssertKind(equipment.Temperature, 3, 1, At(0, 0), 5, At(0, 20), 3, 3, 1.632993, 1, At(0, 0), 5, At(0, 20), 4, 20);
        Assert.Empty(equipment.Temperature.Events);
        AssertEmpty(equipment.RelativeHumidity);
    }

    [Fact]
    public async Task Ambiguous_values_are_excluded_and_the_other_kind_is_kept()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        Add(sensor, At(0, 0), MeasurementKind.Temperature, 1);
        Add(sensor, At(0, 0), MeasurementKind.Temperature, 2);
        Add(sensor, At(0, 0), MeasurementKind.RelativeHumidity, 40);
        Add(sensor, At(0, 5), MeasurementKind.Temperature, 3);
        Add(sensor, At(0, 5), MeasurementKind.RelativeHumidity, 50);
        Add(sensor, At(0, 5), MeasurementKind.RelativeHumidity, 60);
        Add(sensor, At(0, 10), MeasurementKind.Temperature, 7);
        Add(sensor, At(0, 10), MeasurementKind.RelativeHumidity, 70);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var equipment = (await GetAnalysisAsync(facilityId, At(0, 0), At(0, 15))).Equipment[0];

        Assert.Equal(2, equipment.AmbiguousSampleCount);
        Assert.Equal(0, equipment.MissingExpectedSampleCount);
        Assert.Equal(100.0m, equipment.CoveragePercent);
        AssertKind(equipment.Temperature, 2, 3, At(0, 5), 7, At(0, 10), 5, 5, 2, 3, At(0, 5), 7, At(0, 10), 4, 5);
        AssertKind(equipment.RelativeHumidity, 2, 40, At(0, 0), 70, At(0, 10), 55, 55, 15, 40, At(0, 0), 70, At(0, 10), 30, 10);
    }

    [Fact]
    public async Task Peaks_and_dips_use_neighbouring_valid_values_and_ignore_a_flat_top()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        AddTemps(
            sensor,
            (At(0, 0), 100),
            (At(0, 5), 1),
            (At(0, 10), 80),
            (At(0, 20), 2),
            (At(0, 25), 50),
            (At(0, 30), 50),
            (At(0, 35), 0),
            (At(0, 40), 10));
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var temperature = (await GetAnalysisAsync(facilityId, At(0, 0), At(0, 45))).Equipment[0].Temperature;

        Assert.Equal(8, temperature.Count);
        Assert.Equal(100, temperature.First);
        Assert.Equal(10, temperature.Last);
        Assert.Equal(-90, temperature.Change);
        Assert.Equal(40, temperature.DurationSeconds);
        Assert.Equal(
            [
                (At(0, 5), 1d, SeriesEventKind.Dip),
                (At(0, 10), 80d, SeriesEventKind.Peak),
                (At(0, 20), 2d, SeriesEventKind.Dip),
                (At(0, 35), 0d, SeriesEventKind.Dip)
            ],
            temperature.Events.Select(item => (item.Timestamp, item.Value, item.Kind)));
    }

    [Fact]
    public async Task Two_equipment_stay_separate_and_facility_counts_are_sums()
    {
        var first = Equipment("SENSOR01", "Near entrance");
        AddTemps(first, (At(0, 0), 1), (At(0, 5), 2), (At(0, 10), 3));
        var second = Equipment("SENSOR02", "Far wall");
        Add(second, At(0, 0), MeasurementKind.Temperature, 10);
        Add(second, At(0, 0), MeasurementKind.RelativeHumidity, 20);
        var third = Equipment("SENSOR03", "Unused shelf");
        var facilityId = await SeedAsync("Cold Room A", first, second, third);

        var analysis = await GetAnalysisAsync(facilityId, At(0, 0), At(0, 15));

        Assert.Equal(["SENSOR01", "SENSOR02", "SENSOR03"], analysis.Equipment.Select(item => item.EquipmentName));
        Assert.Equal(3, analysis.EquipmentCount);
        Assert.Equal(2, analysis.EquipmentWithTemperatureCount);
        Assert.Equal(1, analysis.EquipmentWithRelativeHumidityCount);
        Assert.Equal(4, analysis.ValidTemperatureCount);
        Assert.Equal(1, analysis.ValidRelativeHumidityCount);
        Assert.Equal(2, analysis.Equipment[0].Temperature.Mean);
        Assert.Equal(10, analysis.Equipment[1].Temperature.Mean);
        Assert.Equal(20, analysis.Equipment[1].RelativeHumidity.Mean);
        AssertEmpty(analysis.Equipment[0].RelativeHumidity);
        AssertEmpty(analysis.Equipment[2].Temperature);
        Assert.Equal(3, analysis.Equipment[2].MissingExpectedSampleCount);
        Assert.Equal(0, analysis.Equipment[2].SourceRowCount);
    }

    [Fact]
    public async Task From_must_be_before_to()
    {
        var facilityId = await SeedAsync("Cold Room A");

        foreach (var (from, to) in new[]
        {
            ("2024-01-01T10:00:00", "2024-01-01T10:00:00"),
            ("2024-01-01T10:00:30", "2024-01-01T10:00:00")
        })
        {
            var response = await GetRawAsync(facilityId, from, to);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("From must be before to.", await response.ReadErrorAsync());
        }
    }

    [Fact]
    public async Task Missing_or_malformed_bounds_are_rejected()
    {
        var facilityId = Guid.CreateVersion7();
        var response = await GetRawAsync(facilityId, "2024-01-01T10:00:00Z", "2024-01-01T10:00:30");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("From must be yyyy-MM-ddTHH:mm:ss.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Interval_longer_than_24_hours_is_rejected_and_exactly_24_hours_is_allowed()
    {
        var facilityId = await SeedAsync("Cold Room A", Equipment("SENSOR01", "Near entrance"));

        var tooLong = await GetRawAsync(Guid.CreateVersion7(), "2024-01-01T10:00:00", "2024-01-02T10:00:01");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("The requested interval is longer than 24 hours.", await tooLong.ReadErrorAsync());

        var analysis = await GetAnalysisAsync(facilityId, At(0, 0), new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Unspecified));
        var equipment = Assert.Single(analysis.Equipment);
        Assert.Equal(17280, equipment.MissingExpectedSampleCount);
        Assert.Equal(0, equipment.SourceRowCount);
        Assert.Equal(0.0m, equipment.CoveragePercent);
        Assert.False(equipment.HasCompleteCoverage);
        AssertEmpty(equipment.Temperature);
        AssertEmpty(equipment.RelativeHumidity);
    }

    [Fact]
    public async Task Unknown_facility_returns_not_found()
    {
        var response = await GetRawAsync(Guid.CreateVersion7(), "2024-01-01T10:00:00", "2024-01-01T10:00:30");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Facility was not found.", await response.ReadErrorAsync());
    }

    private static void AssertKind(
        KindAnalysis kind,
        int count,
        double min,
        DateTime minAt,
        double max,
        DateTime maxAt,
        double mean,
        double median,
        double stdDev,
        double first,
        DateTime firstAt,
        double last,
        DateTime lastAt,
        double change,
        double durationSeconds)
    {
        Assert.Equal(count, kind.Count);
        Assert.Equal(min, kind.Min);
        Assert.Equal(minAt, kind.MinAt);
        Assert.Equal(max, kind.Max);
        Assert.Equal(maxAt, kind.MaxAt);
        Assert.Equal(mean, kind.Mean);
        Assert.Equal(median, kind.Median);
        Assert.Equal(stdDev, kind.PopulationStdDev);
        Assert.Equal(first, kind.First);
        Assert.Equal(firstAt, kind.FirstAt);
        Assert.Equal(last, kind.Last);
        Assert.Equal(lastAt, kind.LastAt);
        Assert.Equal(change, kind.Change);
        Assert.Equal(durationSeconds, kind.DurationSeconds);
    }

    private static void AssertEmpty(KindAnalysis kind)
    {
        Assert.Equal(0, kind.Count);
        Assert.Null(kind.Min);
        Assert.Null(kind.MinAt);
        Assert.Null(kind.Max);
        Assert.Null(kind.MaxAt);
        Assert.Null(kind.Mean);
        Assert.Null(kind.Median);
        Assert.Null(kind.PopulationStdDev);
        Assert.Null(kind.First);
        Assert.Null(kind.FirstAt);
        Assert.Null(kind.Last);
        Assert.Null(kind.LastAt);
        Assert.Null(kind.Change);
        Assert.Null(kind.DurationSeconds);
        Assert.Empty(kind.Events);
    }

    private async Task<FacilityAnalysis> GetAnalysisAsync(Guid facilityId, DateTime from, DateTime to)
    {
        var response = await GetRawAsync(facilityId, Text(from), Text(to));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var analysis = await response.Content.ReadFromJsonAsync<FacilityAnalysis>(Json);
        Assert.NotNull(analysis);
        return analysis;
    }

    private Task<HttpResponseMessage> GetRawAsync(Guid facilityId, string? from, string? to)
    {
        var query = new List<string>();
        if (from is not null)
        {
            query.Add("from=" + Uri.EscapeDataString(from));
        }

        if (to is not null)
        {
            query.Add("to=" + Uri.EscapeDataString(to));
        }

        var suffix = query.Count == 0 ? string.Empty : "?" + string.Join('&', query);
        return Client.GetAsync($"/api/facilities/{facilityId}/analysis{suffix}");
    }

    private async Task<Guid> SeedAsync(string facilityName, params EquipmentSpec[] equipment)
    {
        await using var db = CreateDbContext();
        var facility = new Facility
        {
            Id = Guid.CreateVersion7(),
            Name = facilityName
        };
        db.Add(facility);

        var types = new Dictionary<string, EquipmentType>();
        foreach (var spec in equipment)
        {
            if (!types.TryGetValue(spec.TypeName, out var equipmentType))
            {
                equipmentType = new EquipmentType
                {
                    Id = Guid.CreateVersion7(),
                    Name = spec.TypeName
                };
                types.Add(spec.TypeName, equipmentType);
                db.Add(equipmentType);
            }

            var item = new Equipment
            {
                Id = Guid.CreateVersion7(),
                Facility = facility,
                EquipmentType = equipmentType,
                Name = spec.Name,
                Placement = spec.Placement
            };
            spec.Id = item.Id;
            db.Add(item);
            if (spec.Rows.Count == 0)
            {
                continue;
            }

            var import = new DataImport
            {
                Id = Guid.CreateVersion7(),
                Equipment = item,
                OriginalFileName = "sensor.csv",
                ImportedAt = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
                Status = ImportStatus.Completed,
                ImportedRowCount = spec.Rows.Count,
                FirstMeasurementAt = spec.Rows.Min(row => row.MeasuredAt),
                LastMeasurementAt = spec.Rows.Max(row => row.MeasuredAt)
            };
            db.Add(import);
            foreach (var row in spec.Rows)
            {
                db.Add(new Measurement
                {
                    Id = Guid.CreateVersion7(),
                    DataImport = import,
                    MeasuredAt = row.MeasuredAt,
                    Value = row.Value,
                    Kind = row.Kind,
                    Unit = row.Kind == MeasurementKind.Temperature ? "°C" : "%"
                });
            }
        }

        await db.SaveChangesAsync();
        return facility.Id;
    }

    private ColdRoomDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ColdRoomDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;
        return new ColdRoomDbContext(options);
    }

    private static EquipmentSpec Equipment(string name, string placement) =>
        new()
        {
            Name = name,
            Placement = placement
        };

    private static void AddTemps(EquipmentSpec spec, params (DateTime At, double Value)[] values)
    {
        foreach (var (at, value) in values)
        {
            Add(spec, at, MeasurementKind.Temperature, value);
        }
    }

    private static void Add(EquipmentSpec spec, DateTime measuredAt, MeasurementKind kind, double value)
    {
        spec.Rows.Add(new StoredRow(measuredAt, kind, value));
    }

    private static string Text(DateTime time) =>
        time.ToString(AnalysisPeriodService.TimestampFormat, CultureInfo.InvariantCulture);

    private static DateTime At(int minute, int second) =>
        new(2024, 1, 1, 10, minute, second, DateTimeKind.Unspecified);

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class EquipmentSpec
    {
        public required string Name { get; init; }

        public required string Placement { get; init; }

        public string TypeName { get; init; } = "DHT22 Logger";

        public Guid Id { get; set; }

        public List<StoredRow> Rows { get; } = [];
    }

    private sealed record StoredRow(DateTime MeasuredAt, MeasurementKind Kind, double Value);
}
