using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Domain;
using ColdRoomAssessment.Infrastructure;
using ColdRoomAssessment.IntegrationTests.Setup;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.IntegrationTests.Http;

[Collection(IntegrationCollection.Name)]
public sealed class FacilityAnalysisPeriodEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private static readonly JsonSerializerOptions Json = CreateJson();

    private readonly PostgresFixture _postgres = postgres;

    [Fact]
    public async Task Complete_grid_is_fully_covered_without_an_ends_before_to_warning()
    {
        var from = At(0, 0);
        var to = At(0, 30);
        var sensor = Equipment("SENSOR01", "Near entrance, upper shelf");
        sensor.Imports.Add(new ImportSpec(
        [
            At(0, 0),
            At(0, 1),
            At(0, 5),
            At(0, 10),
            At(0, 15),
            At(0, 20),
            At(0, 25)
        ]));
        sensor.Imports.Add(new ImportSpec([Clock(9, 59, 55), to]));

        var facilityId = await SeedAsync("Cold Room A", sensor);
        var period = await GetPeriodAsync(facilityId, from, to);
        var equipment = Assert.Single(period.Equipment);

        Assert.Equal(facilityId, period.FacilityId);
        Assert.Equal("Cold Room A", period.FacilityName);
        Assert.Equal(from, period.From);
        Assert.Equal(DateTimeKind.Unspecified, period.From.Kind);
        Assert.Equal(to, period.To);
        Assert.Equal(1, period.EquipmentCount);
        Assert.Equal(1, period.EquipmentWithDataCount);
        Assert.Equal(1, period.EquipmentWithCompleteCoverageCount);
        Assert.True(period.AllEquipmentHaveData);
        Assert.True(period.AllEquipmentHaveCompleteCoverage);
        Assert.Empty(period.Warnings);

        Assert.Equal(sensor.Id, equipment.EquipmentId);
        Assert.Equal("SENSOR01", equipment.EquipmentName);
        Assert.Equal(sensor.TypeId, equipment.EquipmentTypeId);
        Assert.Equal("DHT22 Logger", equipment.EquipmentTypeName);
        Assert.Equal("Near entrance, upper shelf", equipment.Placement);
        Assert.Equal(from, equipment.From);
        Assert.Equal(to, equipment.To);
        Assert.Equal(At(0, 0), equipment.FirstMeasurementAt);
        Assert.Equal(At(0, 25), equipment.LastMeasurementAt);
        Assert.Equal(7, equipment.SourceRowCount);
        Assert.Equal(14, equipment.MeasurementCount);
        Assert.Equal(1, equipment.ImportCount);
        Assert.Equal(5, equipment.ExpectedSamplingIntervalSeconds);
        Assert.Equal(6, equipment.ExpectedSourceSampleCount);
        Assert.Equal(6, equipment.CoveredSourceSampleCount);
        Assert.Equal(0, equipment.MissingSourceSampleCount);
        Assert.Equal(100.0m, equipment.CoveragePercent);
        Assert.True(equipment.HasData);
        Assert.True(equipment.HasCompleteCoverage);
        Assert.Empty(equipment.Gaps);
    }

    [Fact]
    public async Task Missing_samples_before_the_first_covered_time_are_a_leading_gap()
    {
        var sensor = Equipment("SENSOR01", "Near entrance", imports: [new ImportSpec([At(0, 10), At(0, 15)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var period = await GetPeriodAsync(facilityId, At(0, 0), At(0, 20));
        var equipment = Assert.Single(period.Equipment);

        Assert.Equal(4, equipment.ExpectedSourceSampleCount);
        Assert.Equal(2, equipment.CoveredSourceSampleCount);
        Assert.Equal(2, equipment.MissingSourceSampleCount);
        Assert.Equal(50.0m, equipment.CoveragePercent);
        Assert.False(equipment.HasCompleteCoverage);
        var gap = Assert.Single(equipment.Gaps);
        Assert.Equal(AnalysisGapKind.Leading, gap.Kind);
        Assert.Equal(At(0, 0), gap.From);
        Assert.Equal(At(0, 10), gap.To);
        Assert.Equal(2, gap.MissingSourceSampleCount);
        var warning = Assert.Single(period.Warnings);
        Assert.Equal(sensor.Id, warning.EquipmentId);
        Assert.Equal(AnalysisWarningCode.StartsAfterFrom, warning.Code);
    }

    [Fact]
    public async Task Missing_samples_after_the_last_covered_time_are_a_trailing_gap()
    {
        var sensor = Equipment(
            "SENSOR01",
            "Near entrance",
            imports: [new ImportSpec([At(0, 0), At(0, 5), At(0, 10)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var period = await GetPeriodAsync(facilityId, At(0, 0), At(0, 30));
        var equipment = Assert.Single(period.Equipment);

        Assert.Equal(6, equipment.ExpectedSourceSampleCount);
        Assert.Equal(3, equipment.CoveredSourceSampleCount);
        Assert.Equal(3, equipment.MissingSourceSampleCount);
        Assert.Equal(At(0, 10), equipment.LastMeasurementAt);
        var gap = Assert.Single(equipment.Gaps);
        Assert.Equal(AnalysisGapKind.Trailing, gap.Kind);
        Assert.Equal(At(0, 10), gap.From);
        Assert.Equal(At(0, 30), gap.To);
        Assert.Equal(3, gap.MissingSourceSampleCount);
        var warning = Assert.Single(period.Warnings);
        Assert.Equal(AnalysisWarningCode.EndsBeforeTo, warning.Code);
    }

    [Fact]
    public async Task Internal_jump_of_15_seconds_reports_two_missing_grid_samples()
    {
        var sensor = Equipment("SENSOR01", "Near entrance", imports: [new ImportSpec([At(0, 0), At(0, 15)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var period = await GetPeriodAsync(facilityId, At(0, 0), At(0, 20));
        var equipment = Assert.Single(period.Equipment);

        Assert.Equal(2, equipment.MissingSourceSampleCount);
        var gap = Assert.Single(equipment.Gaps);
        Assert.Equal(AnalysisGapKind.Internal, gap.Kind);
        Assert.Equal(At(0, 0), gap.From);
        Assert.Equal(At(0, 15), gap.To);
        Assert.Equal(2, gap.MissingSourceSampleCount);
        var warning = Assert.Single(period.Warnings);
        Assert.Equal(AnalysisWarningCode.InternalGap, warning.Code);
    }

    [Fact]
    public async Task Leading_internal_and_trailing_gaps_add_up()
    {
        var sensor = Equipment(
            "SENSOR01",
            "Near entrance",
            imports: [new ImportSpec([At(0, 10), At(0, 15), At(0, 30)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var period = await GetPeriodAsync(facilityId, At(0, 0), At(0, 40));
        var equipment = Assert.Single(period.Equipment);

        Assert.Equal(8, equipment.ExpectedSourceSampleCount);
        Assert.Equal(3, equipment.CoveredSourceSampleCount);
        Assert.Equal(5, equipment.MissingSourceSampleCount);
        Assert.Equal(37.5m, equipment.CoveragePercent);
        Assert.Equal(3, equipment.Gaps.Count);
        Assert.Equal(5, equipment.Gaps.Sum(gap => gap.MissingSourceSampleCount));
        Assert.Equal(AnalysisGapKind.Leading, equipment.Gaps[0].Kind);
        Assert.Equal(At(0, 0), equipment.Gaps[0].From);
        Assert.Equal(At(0, 10), equipment.Gaps[0].To);
        Assert.Equal(2, equipment.Gaps[0].MissingSourceSampleCount);
        Assert.Equal(AnalysisGapKind.Internal, equipment.Gaps[1].Kind);
        Assert.Equal(At(0, 15), equipment.Gaps[1].From);
        Assert.Equal(At(0, 30), equipment.Gaps[1].To);
        Assert.Equal(2, equipment.Gaps[1].MissingSourceSampleCount);
        Assert.Equal(AnalysisGapKind.Trailing, equipment.Gaps[2].Kind);
        Assert.Equal(At(0, 30), equipment.Gaps[2].From);
        Assert.Equal(At(0, 40), equipment.Gaps[2].To);
        Assert.Equal(1, equipment.Gaps[2].MissingSourceSampleCount);
        Assert.Equal(
            [
                AnalysisWarningCode.StartsAfterFrom,
                AnalysisWarningCode.InternalGap,
                AnalysisWarningCode.EndsBeforeTo
            ],
            period.Warnings.Select(warning => warning.Code));
    }

    [Fact]
    public async Task Temperature_and_humidity_at_one_time_are_one_source_sample()
    {
        var sensor = Equipment("SENSOR01", "Near entrance", imports: [new ImportSpec([At(0, 10)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var period = await GetPeriodAsync(facilityId, At(0, 0), At(0, 30));
        var equipment = Assert.Single(period.Equipment);

        Assert.Equal(1, equipment.SourceRowCount);
        Assert.Equal(2, equipment.MeasurementCount);
        Assert.Equal(1, equipment.ImportCount);
        Assert.Equal(6, equipment.ExpectedSourceSampleCount);
        Assert.Equal(1, equipment.CoveredSourceSampleCount);
        Assert.Equal(5, equipment.MissingSourceSampleCount);
        Assert.Equal(16.7m, equipment.CoveragePercent);
        Assert.Equal(2, equipment.Gaps[0].MissingSourceSampleCount);
        Assert.Equal(AnalysisGapKind.Leading, equipment.Gaps[0].Kind);
        Assert.Equal(3, equipment.Gaps[1].MissingSourceSampleCount);
        Assert.Equal(AnalysisGapKind.Trailing, equipment.Gaps[1].Kind);
    }

    [Fact]
    public async Task Equipment_coverage_rolls_up_and_no_data_is_the_only_warning()
    {
        var complete = Equipment(
            "SENSOR01",
            "Near entrance",
            imports: [new ImportSpec([At(0, 0), At(0, 5), At(0, 10), At(0, 15), At(0, 20), At(0, 25)])]);
        var partial = Equipment(
            "SENSOR02",
            "Far wall",
            imports: [new ImportSpec([At(0, 10), At(0, 15), At(0, 20)])]);
        var empty = Equipment("SENSOR03", "Unused shelf");
        var facilityId = await SeedAsync("Cold Room A", complete, partial, empty);
        await SeedAsync(
            "Other room",
            Equipment("OTHER", "Elsewhere", "Other Logger", [new ImportSpec([At(0, 0), At(0, 30)])]));

        var period = await GetPeriodAsync(facilityId, At(0, 0), At(0, 30));

        Assert.Equal(["SENSOR01", "SENSOR02", "SENSOR03"], period.Equipment.Select(item => item.EquipmentName));
        Assert.Equal(3, period.EquipmentCount);
        Assert.Equal(2, period.EquipmentWithDataCount);
        Assert.Equal(1, period.EquipmentWithCompleteCoverageCount);
        Assert.False(period.AllEquipmentHaveData);
        Assert.False(period.AllEquipmentHaveCompleteCoverage);
        Assert.True(period.Equipment[0].HasCompleteCoverage);
        Assert.Empty(period.Equipment[0].Gaps);

        var partialCoverage = period.Equipment[1];
        Assert.Equal(3, partialCoverage.SourceRowCount);
        Assert.Equal(6, partialCoverage.MeasurementCount);
        Assert.Equal(3, partialCoverage.CoveredSourceSampleCount);
        Assert.Equal(50.0m, partialCoverage.CoveragePercent);
        Assert.False(partialCoverage.HasCompleteCoverage);
        Assert.Equal(At(0, 0), partialCoverage.Gaps[0].From);
        Assert.Equal(At(0, 10), partialCoverage.Gaps[0].To);
        Assert.Equal(2, partialCoverage.Gaps[0].MissingSourceSampleCount);
        Assert.Equal(AnalysisGapKind.Leading, partialCoverage.Gaps[0].Kind);
        Assert.Equal(At(0, 20), partialCoverage.Gaps[1].From);
        Assert.Equal(At(0, 30), partialCoverage.Gaps[1].To);
        Assert.Equal(1, partialCoverage.Gaps[1].MissingSourceSampleCount);
        Assert.Equal(AnalysisGapKind.Trailing, partialCoverage.Gaps[1].Kind);

        var missing = period.Equipment[2];
        Assert.Equal(empty.Id, missing.EquipmentId);
        Assert.Null(missing.FirstMeasurementAt);
        Assert.Null(missing.LastMeasurementAt);
        Assert.Equal(0, missing.SourceRowCount);
        Assert.Equal(0, missing.MeasurementCount);
        Assert.Equal(0, missing.ImportCount);
        Assert.Equal(5, missing.ExpectedSamplingIntervalSeconds);
        Assert.Equal(6, missing.ExpectedSourceSampleCount);
        Assert.Equal(0, missing.CoveredSourceSampleCount);
        Assert.Equal(6, missing.MissingSourceSampleCount);
        Assert.Equal(0.0m, missing.CoveragePercent);
        Assert.False(missing.HasData);
        Assert.False(missing.HasCompleteCoverage);
        Assert.Empty(missing.Gaps);

        Assert.Equal(
            [
                (partial.Id, AnalysisWarningCode.StartsAfterFrom),
                (partial.Id, AnalysisWarningCode.EndsBeforeTo),
                (empty.Id, AnalysisWarningCode.NoData)
            ],
            period.Warnings.Select(warning => (warning.EquipmentId, warning.Code)));
    }

    [Fact]
    public async Task Off_grid_time_counts_as_a_source_row_and_does_not_cover_a_grid_time()
    {
        var sensor = Equipment(
            "SENSOR01",
            "Near entrance",
            imports: [new ImportSpec([At(0, 0), At(0, 7), At(0, 10)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var period = await GetPeriodAsync(facilityId, At(0, 0), At(0, 15));
        var equipment = Assert.Single(period.Equipment);

        Assert.Equal(3, equipment.SourceRowCount);
        Assert.Equal(6, equipment.MeasurementCount);
        Assert.Equal(3, equipment.ExpectedSourceSampleCount);
        Assert.Equal(2, equipment.CoveredSourceSampleCount);
        Assert.Equal(1, equipment.MissingSourceSampleCount);
        Assert.Equal(66.7m, equipment.CoveragePercent);
        Assert.Equal(At(0, 0), equipment.FirstMeasurementAt);
        Assert.Equal(At(0, 10), equipment.LastMeasurementAt);
        var gap = Assert.Single(equipment.Gaps);
        Assert.Equal(AnalysisGapKind.Internal, gap.Kind);
        Assert.Equal(At(0, 0), gap.From);
        Assert.Equal(At(0, 10), gap.To);
        Assert.Equal(1, gap.MissingSourceSampleCount);
        var warning = Assert.Single(period.Warnings);
        Assert.Equal(AnalysisWarningCode.InternalGap, warning.Code);
    }

    [Fact]
    public async Task Coverage_percent_rounds_half_away_from_zero()
    {
        var sensor = Equipment("SENSOR01", "Near entrance", imports: [new ImportSpec([At(0, 0)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var period = await GetPeriodAsync(facilityId, At(0, 0), Clock(10, 1, 20));
        var equipment = Assert.Single(period.Equipment);

        Assert.Equal(16, equipment.ExpectedSourceSampleCount);
        Assert.Equal(1, equipment.CoveredSourceSampleCount);
        Assert.Equal(6.3m, equipment.CoveragePercent);
    }

    [Fact]
    public async Task Stored_rows_that_miss_every_grid_time_are_a_full_interval_gap()
    {
        var sensor = Equipment("SENSOR01", "Near entrance", imports: [new ImportSpec([At(0, 7)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor);
        var from = At(0, 0);
        var to = At(0, 15);

        var response = await GetRawAsync(facilityId, Text(from), Text(to));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"kind\":\"FullInterval\"", json, StringComparison.Ordinal);
        Assert.Contains("\"warnings\":[]", json, StringComparison.Ordinal);
        Assert.DoesNotContain("NoData", json, StringComparison.Ordinal);
        Assert.DoesNotContain("StartsAfterFrom", json, StringComparison.Ordinal);
        Assert.DoesNotContain("EndsBeforeTo", json, StringComparison.Ordinal);
        Assert.DoesNotContain("InternalGap", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Leading", json, StringComparison.Ordinal);

        var period = JsonSerializer.Deserialize<AnalysisPeriod>(json, Json);
        Assert.NotNull(period);
        Assert.Equal(facilityId, period.FacilityId);
        Assert.Equal("Cold Room A", period.FacilityName);
        Assert.Equal(from, period.From);
        Assert.Equal(to, period.To);
        Assert.Equal(1, period.EquipmentCount);
        Assert.Equal(1, period.EquipmentWithDataCount);
        Assert.Equal(0, period.EquipmentWithCompleteCoverageCount);
        Assert.True(period.AllEquipmentHaveData);
        Assert.False(period.AllEquipmentHaveCompleteCoverage);
        Assert.Empty(period.Warnings);

        var equipment = Assert.Single(period.Equipment);
        Assert.Equal(sensor.Id, equipment.EquipmentId);
        Assert.Equal("SENSOR01", equipment.EquipmentName);
        Assert.Equal(sensor.TypeId, equipment.EquipmentTypeId);
        Assert.Equal("DHT22 Logger", equipment.EquipmentTypeName);
        Assert.Equal("Near entrance", equipment.Placement);
        Assert.Equal(from, equipment.From);
        Assert.Equal(to, equipment.To);
        Assert.Equal(At(0, 7), equipment.FirstMeasurementAt);
        Assert.Equal(At(0, 7), equipment.LastMeasurementAt);
        Assert.Equal(1, equipment.SourceRowCount);
        Assert.Equal(2, equipment.MeasurementCount);
        Assert.Equal(1, equipment.ImportCount);
        Assert.Equal(5, equipment.ExpectedSamplingIntervalSeconds);
        Assert.Equal(3, equipment.ExpectedSourceSampleCount);
        Assert.True(equipment.HasData);
        Assert.Equal(0, equipment.CoveredSourceSampleCount);
        Assert.Equal(3, equipment.MissingSourceSampleCount);
        Assert.Equal(0.0m, equipment.CoveragePercent);
        Assert.False(equipment.HasCompleteCoverage);
        var gap = Assert.Single(equipment.Gaps);
        Assert.Equal(AnalysisGapKind.FullInterval, gap.Kind);
        Assert.Equal(from, gap.From);
        Assert.Equal(to, gap.To);
        Assert.Equal(3, gap.MissingSourceSampleCount);
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
            ("2024-01-01 10:00:00", "2024-01-01T10:00:30", "From must be yyyy-MM-ddTHH:mm:ss."),
            ("01.01.2024T10:00:00", "2024-01-01T10:00:30", "From must be yyyy-MM-ddTHH:mm:ss.")
        };

        foreach (var (from, to, error) in cases)
        {
            var response = await GetRawAsync(facilityId, from, to);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(error, await response.ReadErrorAsync());
        }
    }

    [Fact]
    public async Task Unknown_facility_returns_not_found()
    {
        var response = await GetRawAsync(Guid.CreateVersion7(), "2024-01-01T10:00:00", "2024-01-01T10:00:30");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Facility was not found.", await response.ReadErrorAsync());
    }

    private async Task<AnalysisPeriod> GetPeriodAsync(Guid facilityId, DateTime from, DateTime to)
    {
        var response = await GetRawAsync(facilityId, Text(from), Text(to));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var period = await response.Content.ReadFromJsonAsync<AnalysisPeriod>(Json);
        Assert.NotNull(period);
        return period;
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
        return Client.GetAsync($"/api/facilities/{facilityId}/analysis-period{suffix}");
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

            foreach (var series in spec.Imports)
            {
                var import = new DataImport
                {
                    Id = Guid.CreateVersion7(),
                    Equipment = item,
                    OriginalFileName = "sensor.csv",
                    ImportedAt = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
                    Status = ImportStatus.Completed,
                    ImportedRowCount = series.Timestamps.Length,
                    FirstMeasurementAt = series.Timestamps.Length == 0 ? null : series.Timestamps[0],
                    LastMeasurementAt = series.Timestamps.Length == 0 ? null : series.Timestamps[^1]
                };
                db.Add(import);

                foreach (var timestamp in series.Timestamps)
                {
                    db.Add(Sample(import, timestamp, MeasurementKind.Temperature, "°C", 21.5));
                    if (series.IncludeHumidity)
                    {
                        db.Add(Sample(import, timestamp, MeasurementKind.RelativeHumidity, "%", 40));
                    }
                }
            }
        }

        await db.SaveChangesAsync();
        return facility.Id;
    }

    private static Measurement Sample(
        DataImport import,
        DateTime measuredAt,
        MeasurementKind kind,
        string unit,
        double value) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            DataImport = import,
            MeasuredAt = measuredAt,
            Value = value,
            Kind = kind,
            Unit = unit
        };

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
        string typeName = "DHT22 Logger",
        IReadOnlyList<ImportSpec>? imports = null)
    {
        var spec = new EquipmentSpec
        {
            Name = name,
            Placement = placement,
            TypeName = typeName
        };
        if (imports is not null)
        {
            spec.Imports.AddRange(imports);
        }

        return spec;
    }

    private static string Text(DateTime time) =>
        time.ToString(AnalysisPeriodService.TimestampFormat, CultureInfo.InvariantCulture);

    private static DateTime At(int minute, int second) =>
        new(2024, 1, 1, 10, minute, second, DateTimeKind.Unspecified);

    private static DateTime Clock(int hour, int minute, int second) =>
        new(2024, 1, 1, hour, minute, second, DateTimeKind.Unspecified);

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

        public Guid TypeId { get; set; }

        public List<ImportSpec> Imports { get; } = [];
    }

    private sealed record ImportSpec(DateTime[] Timestamps, bool IncludeHumidity = true);
}
