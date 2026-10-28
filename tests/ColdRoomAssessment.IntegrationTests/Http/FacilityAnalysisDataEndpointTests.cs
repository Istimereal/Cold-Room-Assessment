using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Domain;
using ColdRoomAssessment.Infrastructure;
using ColdRoomAssessment.IntegrationTests.Setup;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.IntegrationTests.Http;

[Collection(IntegrationCollection.Name)]
public sealed class FacilityAnalysisDataEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PostgresFixture _postgres = postgres;

    [Fact]
    public async Task Complete_grid_aligns_every_expected_timestamp()
    {
        var sensor = Equipment("SENSOR01", "Near entrance, upper shelf");
        AddPair(sensor, At(0, 0), 4.5, 80);
        AddPair(sensor, At(0, 5), 4.6, 81);
        AddPair(sensor, At(0, 10), 4.7, 82);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var data = await GetDataAsync(facilityId, At(0, 0), At(0, 15));
        var equipment = Assert.Single(data.Equipment);

        Assert.Equal(5, data.ExpectedSamplingIntervalSeconds);
        Assert.Equal(3, data.ExpectedGridPointCount);
        Assert.Equal(3, equipment.SourceRowCount);
        Assert.Equal(6, equipment.MeasurementCount);
        Assert.Equal(100.0m, equipment.CoveragePercent);
        Assert.True(equipment.HasCompleteCoverage);
        Assert.Equal(
            [At(0, 0), At(0, 5), At(0, 10)],
            equipment.Samples.Select(sample => sample.Timestamp));
        Assert.All(equipment.Samples, sample =>
        {
            Assert.True(sample.HasSourceSample);
            Assert.False(sample.Ambiguous);
            Assert.NotNull(sample.Temperature);
            Assert.NotNull(sample.RelativeHumidity);
        });
        Assert.Equal(4.5, equipment.Samples[0].Temperature);
        Assert.Equal(80, equipment.Samples[0].RelativeHumidity);
        Assert.Equal(4.6, equipment.Samples[1].Temperature);
        Assert.Equal(81, equipment.Samples[1].RelativeHumidity);
        Assert.Equal(4.7, equipment.Samples[2].Temperature);
        Assert.Equal(82, equipment.Samples[2].RelativeHumidity);
    }

    [Fact]
    public async Task Missing_grid_time_is_a_null_sample_and_partial_kinds_stay_on_their_timestamp()
    {
        var sensor = Equipment("SENSOR01", "Near entrance, upper shelf");
        Add(sensor, Clock(9, 59, 55), MeasurementKind.Temperature, 1);
        AddPair(sensor, At(0, 0), 4.5, 80);
        Add(sensor, At(0, 10), MeasurementKind.Temperature, 4.7);
        Add(sensor, At(0, 15), MeasurementKind.Temperature, 9);
        var facilityId = await SeedAsync("Cold Room A", sensor);
        await SeedAsync(
            "Other room",
            Equipment("OTHER", "Elsewhere", "Other Logger", At(0, 5), MeasurementKind.Temperature, 99));

        var data = await GetDataAsync(facilityId, At(0, 0), At(0, 15));
        var equipment = Assert.Single(data.Equipment);

        Assert.Equal(facilityId, data.FacilityId);
        Assert.Equal("Cold Room A", data.FacilityName);
        Assert.Equal(At(0, 0), data.From);
        Assert.Equal(DateTimeKind.Unspecified, data.From.Kind);
        Assert.Equal(At(0, 15), data.To);
        Assert.Equal(3, data.ExpectedGridPointCount);
        Assert.Equal(sensor.Id, equipment.EquipmentId);
        Assert.Equal("SENSOR01", equipment.EquipmentName);
        Assert.Equal(sensor.TypeId, equipment.EquipmentTypeId);
        Assert.Equal("DHT22 Logger", equipment.EquipmentTypeName);
        Assert.Equal("Near entrance, upper shelf", equipment.Placement);
        Assert.Equal(2, equipment.SourceRowCount);
        Assert.Equal(3, equipment.MeasurementCount);
        Assert.Equal(66.7m, equipment.CoveragePercent);
        Assert.False(equipment.HasCompleteCoverage);

        AssertSample(equipment.Samples[0], At(0, 0), 4.5, 80, true, false);
        AssertSample(equipment.Samples[1], At(0, 5), null, null, false, false);
        AssertSample(equipment.Samples[2], At(0, 10), 4.7, null, true, false);
    }

    [Fact]
    public async Task Several_missing_grid_times_stay_null()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        AddPair(sensor, At(0, 20), 5, 50);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var data = await GetDataAsync(facilityId, At(0, 0), At(0, 30));
        var equipment = Assert.Single(data.Equipment);

        Assert.Equal(6, data.ExpectedGridPointCount);
        Assert.Equal(1, equipment.SourceRowCount);
        Assert.Equal(2, equipment.MeasurementCount);
        Assert.Equal(16.7m, equipment.CoveragePercent);
        Assert.Equal(6, equipment.Samples.Count);
        Assert.Equal(5, equipment.Samples.Count(sample => !sample.HasSourceSample));
        Assert.All(
            equipment.Samples.Where(sample => sample.Timestamp != At(0, 20)),
            sample => AssertSample(sample, sample.Timestamp, null, null, false, false));
        AssertSample(equipment.Samples[4], At(0, 20), 5, 50, true, false);
    }

    [Fact]
    public async Task Humidity_only_leaves_temperature_null()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        Add(sensor, At(0, 0), MeasurementKind.RelativeHumidity, 40);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var data = await GetDataAsync(facilityId, At(0, 0), At(0, 5));
        var sample = Assert.Single(Assert.Single(data.Equipment).Samples);

        AssertSample(sample, At(0, 0), null, 40, true, false);
        Assert.Equal(1, data.Equipment[0].SourceRowCount);
        Assert.Equal(1, data.Equipment[0].MeasurementCount);
        Assert.True(data.Equipment[0].HasCompleteCoverage);
    }

    [Fact]
    public async Task Equipment_with_no_rows_still_gets_the_full_null_grid()
    {
        var present = Equipment("SENSOR01", "Near entrance");
        AddPair(present, At(0, 0), 4.5, 80);
        var missing = Equipment("SENSOR02", "Unused shelf");
        var facilityId = await SeedAsync("Cold Room A", present, missing);

        var data = await GetDataAsync(facilityId, At(0, 0), At(0, 15));

        Assert.Equal(["SENSOR01", "SENSOR02"], data.Equipment.Select(item => item.EquipmentName));
        var empty = data.Equipment[1];
        Assert.Equal(missing.Id, empty.EquipmentId);
        Assert.Equal(0, empty.SourceRowCount);
        Assert.Equal(0, empty.MeasurementCount);
        Assert.Equal(0.0m, empty.CoveragePercent);
        Assert.False(empty.HasCompleteCoverage);
        Assert.Equal(3, empty.Samples.Count);
        Assert.All(empty.Samples, sample => Assert.False(sample.HasSourceSample));
        Assert.All(empty.Samples, sample => Assert.False(sample.Ambiguous));
        Assert.All(empty.Samples, sample => Assert.Null(sample.Temperature));
        Assert.All(empty.Samples, sample => Assert.Null(sample.RelativeHumidity));
    }

    [Fact]
    public async Task Two_equipment_share_the_same_timestamps()
    {
        var first = Equipment("SENSOR01", "Near entrance");
        var second = Equipment("SENSOR02", "Far wall");
        AddPair(first, At(0, 0), 4.5, 80);
        AddPair(first, At(0, 5), 4.6, 81);
        AddPair(second, At(0, 0), 4.5, 80);
        AddPair(second, At(0, 5), 4.6, 81);
        var facilityId = await SeedAsync("Cold Room A", first, second);

        var data = await GetDataAsync(facilityId, At(0, 0), At(0, 10));

        Assert.Equal(2, data.Equipment.Count);
        Assert.Equal(data.Equipment[0].Samples.Select(SampleShape), data.Equipment[1].Samples.Select(SampleShape));
        Assert.Equal(100.0m, data.Equipment[0].CoveragePercent);
        Assert.Equal(100.0m, data.Equipment[1].CoveragePercent);
    }

    [Fact]
    public async Task Off_grid_time_is_not_moved_onto_a_grid_point()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        AddPair(sensor, At(0, 0), 4.5, 80);
        Add(sensor, At(0, 7), MeasurementKind.Temperature, 9.9);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var data = await GetDataAsync(facilityId, At(0, 0), At(0, 15));
        var equipment = Assert.Single(data.Equipment);

        Assert.Equal(2, equipment.SourceRowCount);
        Assert.Equal(3, equipment.MeasurementCount);
        Assert.Equal(33.3m, equipment.CoveragePercent);
        Assert.False(equipment.HasCompleteCoverage);
        AssertSample(equipment.Samples[0], At(0, 0), 4.5, 80, true, false);
        AssertSample(equipment.Samples[1], At(0, 5), null, null, false, false);
        AssertSample(equipment.Samples[2], At(0, 10), null, null, false, false);
    }

    [Fact]
    public async Task Duplicate_kind_is_null_and_ambiguous_while_the_other_kind_is_kept()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        Add(sensor, At(0, 0), MeasurementKind.Temperature, 4.5);
        Add(sensor, At(0, 0), MeasurementKind.Temperature, 5.5);
        Add(sensor, At(0, 0), MeasurementKind.RelativeHumidity, 80);
        Add(sensor, At(0, 5), MeasurementKind.Temperature, 4.5);
        Add(sensor, At(0, 5), MeasurementKind.Temperature, 4.5);
        Add(sensor, At(0, 5), MeasurementKind.RelativeHumidity, 40);
        Add(sensor, At(0, 10), MeasurementKind.Temperature, 1);
        Add(sensor, At(0, 10), MeasurementKind.Temperature, 2);
        Add(sensor, At(0, 10), MeasurementKind.RelativeHumidity, 10);
        Add(sensor, At(0, 10), MeasurementKind.RelativeHumidity, 12);
        Add(sensor, At(0, 15), MeasurementKind.RelativeHumidity, 10);
        Add(sensor, At(0, 15), MeasurementKind.RelativeHumidity, 12);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var data = await GetDataAsync(facilityId, At(0, 0), At(0, 20));
        var equipment = Assert.Single(data.Equipment);

        Assert.Equal(4, equipment.SourceRowCount);
        Assert.Equal(12, equipment.MeasurementCount);
        Assert.Equal(100.0m, equipment.CoveragePercent);
        Assert.True(equipment.HasCompleteCoverage);
        AssertSample(equipment.Samples[0], At(0, 0), null, 80, true, true);
        AssertSample(equipment.Samples[1], At(0, 5), null, 40, true, true);
        AssertSample(equipment.Samples[2], At(0, 10), null, null, true, true);
        AssertSample(equipment.Samples[3], At(0, 15), null, null, true, true);
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
        var cases = new (string? From, string? To, string Error)[]
        {
            (null, "2024-01-01T10:00:30", "From is required."),
            ("2024-01-01T10:00:00", null, "To is required."),
            ("", "2024-01-01T10:00:30", "From is required."),
            ("2024-01-01T10:00:00Z", "2024-01-01T10:00:30", "From must be yyyy-MM-ddTHH:mm:ss."),
            ("2024-01-01T10:00:00", "2024-01-01T10:00:30+00:00", "To must be yyyy-MM-ddTHH:mm:ss."),
            ("2024-01-01 10:00:00", "2024-01-01T10:00:30", "From must be yyyy-MM-ddTHH:mm:ss.")
        };

        foreach (var (from, to, error) in cases)
        {
            var response = await GetRawAsync(facilityId, from, to);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(error, await response.ReadErrorAsync());
        }
    }

    [Fact]
    public async Task Interval_longer_than_24_hours_is_rejected_and_exactly_24_hours_is_allowed()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        var facilityId = await SeedAsync("Cold Room A", sensor);
        var from = "2024-01-01T10:00:00";

        var tooLong = await GetRawAsync(Guid.CreateVersion7(), from, "2024-01-02T10:00:01");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("The requested interval is longer than 24 hours.", await tooLong.ReadErrorAsync());

        var data = await GetDataAsync(facilityId, At(0, 0), Clock(10, 0, 0, day: 2));
        var equipment = Assert.Single(data.Equipment);
        Assert.Equal(17280, data.ExpectedGridPointCount);
        Assert.Equal(17280, equipment.Samples.Count);
        Assert.Equal(At(0, 0), equipment.Samples[0].Timestamp);
        Assert.Equal(new DateTime(2024, 1, 2, 9, 59, 55, DateTimeKind.Unspecified), equipment.Samples[^1].Timestamp);
        Assert.Equal(0, equipment.SourceRowCount);
        Assert.Equal(0, equipment.MeasurementCount);
        Assert.All(equipment.Samples, sample => Assert.False(sample.HasSourceSample));
    }

    [Fact]
    public async Task Unknown_facility_returns_not_found()
    {
        var response = await GetRawAsync(Guid.CreateVersion7(), "2024-01-01T10:00:00", "2024-01-01T10:00:30");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Facility was not found.", await response.ReadErrorAsync());
    }

    private static void AssertSample(
        AnalysisSample sample,
        DateTime timestamp,
        double? temperature,
        double? humidity,
        bool hasSourceSample,
        bool ambiguous)
    {
        Assert.Equal(timestamp, sample.Timestamp);
        Assert.Equal(temperature, sample.Temperature);
        Assert.Equal(humidity, sample.RelativeHumidity);
        Assert.Equal(hasSourceSample, sample.HasSourceSample);
        Assert.Equal(ambiguous, sample.Ambiguous);
    }

    private static (DateTime Timestamp, double? Temperature, double? Humidity, bool HasSource, bool Ambiguous) SampleShape(
        AnalysisSample sample) =>
        (sample.Timestamp, sample.Temperature, sample.RelativeHumidity, sample.HasSourceSample, sample.Ambiguous);

    private async Task<AnalysisData> GetDataAsync(Guid facilityId, DateTime from, DateTime to)
    {
        var response = await GetRawAsync(facilityId, Text(from), Text(to));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<AnalysisData>(Json);
        Assert.NotNull(data);
        return data;
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
        return Client.GetAsync($"/api/facilities/{facilityId}/analysis-data{suffix}");
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
            spec.TypeId = equipmentType.Id;
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

    private static EquipmentSpec Equipment(
        string name,
        string placement,
        string typeName = "DHT22 Logger")
    {
        return new EquipmentSpec
        {
            Name = name,
            Placement = placement,
            TypeName = typeName
        };
    }

    private static EquipmentSpec Equipment(
        string name,
        string placement,
        string typeName,
        DateTime measuredAt,
        MeasurementKind kind,
        double value)
    {
        var spec = Equipment(name, placement, typeName);
        Add(spec, measuredAt, kind, value);
        return spec;
    }

    private static void AddPair(EquipmentSpec spec, DateTime measuredAt, double temperature, double humidity)
    {
        Add(spec, measuredAt, MeasurementKind.Temperature, temperature);
        Add(spec, measuredAt, MeasurementKind.RelativeHumidity, humidity);
    }

    private static void Add(EquipmentSpec spec, DateTime measuredAt, MeasurementKind kind, double value)
    {
        spec.Rows.Add(new StoredRow(measuredAt, kind, value));
    }

    private static string Text(DateTime time) =>
        time.ToString(AnalysisPeriodService.TimestampFormat, CultureInfo.InvariantCulture);

    private static DateTime At(int minute, int second) =>
        new(2024, 1, 1, 10, minute, second, DateTimeKind.Unspecified);

    private static DateTime Clock(int hour, int minute, int second, int day = 1) =>
        new(2024, 1, day, hour, minute, second, DateTimeKind.Unspecified);

    private sealed class EquipmentSpec
    {
        public required string Name { get; init; }

        public required string Placement { get; init; }

        public string TypeName { get; init; } = "DHT22 Logger";

        public Guid Id { get; set; }

        public Guid TypeId { get; set; }

        public List<StoredRow> Rows { get; } = [];
    }

    private sealed record StoredRow(DateTime MeasuredAt, MeasurementKind Kind, double Value);
}
