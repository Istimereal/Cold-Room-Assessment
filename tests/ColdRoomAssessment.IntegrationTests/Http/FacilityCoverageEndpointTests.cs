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
public sealed class FacilityCoverageEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [Fact]
    public async Task Steady_cadence_reports_bounds_counts_and_no_gaps()
    {
        var sensor01 = Equipment("SENSOR01", "Near entrance, upper shelf");
        sensor01.Imports.Add(new ImportSpec([At(0, 0), At(0, 5)]));
        sensor01.Imports.Add(new ImportSpec([At(0, 10)]));
        sensor01.Imports.Add(new ImportSpec([]));

        var sensor02 = Equipment("SENSOR02", "Far wall");
        sensor02.Imports.Add(new ImportSpec([At(0, 0), At(0, 5), At(0, 10)]));

        var facilityId = await SeedAsync("Cold Room A", sensor01, sensor02);
        await SeedAsync(
            "Other room",
            Equipment("OTHER", "Elsewhere", "Other Logger", [new ImportSpec([At(1, 0), At(1, 30)])]));

        var coverage = await GetCoverageAsync(facilityId);

        Assert.Equal(facilityId, coverage.FacilityId);
        Assert.Equal("Cold Room A", coverage.FacilityName);
        Assert.Equal(At(0, 0), coverage.EarliestMeasurementAt);
        Assert.Equal(At(0, 10), coverage.LatestMeasurementAt);
        Assert.True(coverage.HasCommonOverlap);
        Assert.Equal(At(0, 0), coverage.CommonOverlapStart);
        Assert.Equal(At(0, 10), coverage.CommonOverlapEnd);
        Assert.Equal(2, coverage.Equipment.Count);

        var first = coverage.Equipment[0];
        Assert.Equal(sensor01.Id, first.EquipmentId);
        Assert.Equal("SENSOR01", first.EquipmentName);
        Assert.Equal(sensor01.TypeId, first.EquipmentTypeId);
        Assert.Equal("DHT22 Logger", first.EquipmentTypeName);
        Assert.Equal("Near entrance, upper shelf", first.Placement);
        AssertSteady(first, importCount: 2);

        var second = coverage.Equipment[1];
        Assert.Equal(sensor02.Id, second.EquipmentId);
        Assert.Equal("SENSOR02", second.EquipmentName);
        Assert.Equal("Far wall", second.Placement);
        AssertSteady(second, importCount: 1);
    }

    [Fact]
    public async Task Jump_from_five_seconds_to_twenty_reports_two_missing_samples()
    {
        var sensor = Equipment(
            "SENSOR01",
            "Near entrance",
            imports: [new ImportSpec([At(0, 0), At(0, 5), At(0, 20)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var coverage = await GetCoverageAsync(facilityId);
        var equipment = Assert.Single(coverage.Equipment);

        Assert.Equal(3, equipment.SourceRowCount);
        Assert.Equal(6, equipment.MeasurementCount);
        Assert.Equal(2, equipment.MissingSourceSampleCount);
        var gap = Assert.Single(equipment.Gaps);
        Assert.Equal(At(0, 5), gap.After);
        Assert.Equal(At(0, 20), gap.Before);
        Assert.Equal(2, gap.MissingSourceSampleCount);
    }

    [Fact]
    public async Task Multiple_gaps_add_up_and_an_exact_step_is_not_a_gap()
    {
        var sensor = Equipment(
            "SENSOR01",
            "Near entrance",
            imports: [new ImportSpec([At(0, 0), At(0, 15), At(0, 20), At(0, 40)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var coverage = await GetCoverageAsync(facilityId);
        var equipment = Assert.Single(coverage.Equipment);

        Assert.Equal(4, equipment.SourceRowCount);
        Assert.Equal(5, equipment.MissingSourceSampleCount);
        Assert.Equal(2, equipment.Gaps.Count);
        Assert.Equal(At(0, 0), equipment.Gaps[0].After);
        Assert.Equal(At(0, 15), equipment.Gaps[0].Before);
        Assert.Equal(2, equipment.Gaps[0].MissingSourceSampleCount);
        Assert.Equal(At(0, 20), equipment.Gaps[1].After);
        Assert.Equal(At(0, 40), equipment.Gaps[1].Before);
        Assert.Equal(3, equipment.Gaps[1].MissingSourceSampleCount);
    }

    [Fact]
    public async Task Shared_timestamp_counts_as_one_source_sample_and_overlaps_itself()
    {
        var sensor = Equipment(
            "SENSOR01",
            "Near entrance",
            imports: [new ImportSpec([At(0, 0)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var coverage = await GetCoverageAsync(facilityId);
        var equipment = Assert.Single(coverage.Equipment);

        Assert.Equal(At(0, 0), equipment.FirstMeasurementAt);
        Assert.Equal(At(0, 0), equipment.LastMeasurementAt);
        Assert.Equal(1, equipment.SourceRowCount);
        Assert.Equal(2, equipment.MeasurementCount);
        Assert.Equal(1, equipment.ImportCount);
        Assert.Equal(0, equipment.MissingSourceSampleCount);
        Assert.Empty(equipment.Gaps);
        Assert.Equal(At(0, 0), coverage.EarliestMeasurementAt);
        Assert.Equal(At(0, 0), coverage.LatestMeasurementAt);
        Assert.True(coverage.HasCommonOverlap);
        Assert.Equal(At(0, 0), coverage.CommonOverlapStart);
        Assert.Equal(At(0, 0), coverage.CommonOverlapEnd);
    }

    [Fact]
    public async Task Equipment_without_measurements_stays_listed_with_null_bounds_and_zero_counts()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        var facilityId = await SeedAsync("Cold Room A", sensor);

        var coverage = await GetCoverageAsync(facilityId);
        var equipment = Assert.Single(coverage.Equipment);

        Assert.Equal(sensor.Id, equipment.EquipmentId);
        Assert.Null(equipment.FirstMeasurementAt);
        Assert.Null(equipment.LastMeasurementAt);
        Assert.Equal(0, equipment.SourceRowCount);
        Assert.Equal(0, equipment.MeasurementCount);
        Assert.Equal(0, equipment.ImportCount);
        Assert.Equal(5, equipment.ExpectedSamplingIntervalSeconds);
        Assert.Equal(0, equipment.MissingSourceSampleCount);
        Assert.Empty(equipment.Gaps);
        Assert.Null(coverage.EarliestMeasurementAt);
        Assert.Null(coverage.LatestMeasurementAt);
        Assert.False(coverage.HasCommonOverlap);
        Assert.Null(coverage.CommonOverlapStart);
        Assert.Null(coverage.CommonOverlapEnd);
    }

    [Fact]
    public async Task Overlap_uses_equipment_with_data_and_keeps_outer_facility_bounds()
    {
        var sensor01 = Equipment(
            "SENSOR01",
            "Near entrance",
            imports: [new ImportSpec([At(0, 0), At(0, 40)])]);
        var sensor02 = Equipment(
            "SENSOR02",
            "Far wall",
            imports: [new ImportSpec([At(0, 10), At(0, 30)])]);
        var sensor03 = Equipment("SENSOR03", "Unused shelf");
        var facilityId = await SeedAsync("Cold Room A", sensor01, sensor02, sensor03);

        var coverage = await GetCoverageAsync(facilityId);

        Assert.Equal(At(0, 0), coverage.EarliestMeasurementAt);
        Assert.Equal(At(0, 40), coverage.LatestMeasurementAt);
        Assert.True(coverage.HasCommonOverlap);
        Assert.Equal(At(0, 10), coverage.CommonOverlapStart);
        Assert.Equal(At(0, 30), coverage.CommonOverlapEnd);
        Assert.Equal(["SENSOR01", "SENSOR02", "SENSOR03"], coverage.Equipment.Select(item => item.EquipmentName));

        var empty = coverage.Equipment[2];
        Assert.Equal(sensor03.Id, empty.EquipmentId);
        Assert.Null(empty.FirstMeasurementAt);
        Assert.Null(empty.LastMeasurementAt);
        Assert.Equal(0, empty.SourceRowCount);
        Assert.Equal(0, empty.MeasurementCount);
        Assert.Equal(0, empty.ImportCount);
        Assert.Equal(5, empty.ExpectedSamplingIntervalSeconds);
        Assert.Empty(empty.Gaps);
    }

    [Fact]
    public async Task Disjoint_ranges_return_false_overlap_and_null_bounds()
    {
        var sensor01 = Equipment(
            "SENSOR01",
            "Near entrance",
            imports: [new ImportSpec([At(0, 0), At(0, 10)])]);
        var sensor02 = Equipment(
            "SENSOR02",
            "Far wall",
            imports: [new ImportSpec([At(0, 20), At(0, 30)])]);
        var facilityId = await SeedAsync("Cold Room A", sensor01, sensor02);

        var response = await Client.GetAsync($"/api/facilities/{facilityId}/measurement-coverage");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"hasCommonOverlap\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"commonOverlapStart\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"commonOverlapEnd\":null", json, StringComparison.Ordinal);

        var coverage = JsonSerializer.Deserialize<FacilityCoverage>(json, ColdRoomHttp.Json);
        Assert.NotNull(coverage);
        Assert.Equal(At(0, 0), coverage.EarliestMeasurementAt);
        Assert.Equal(At(0, 30), coverage.LatestMeasurementAt);
        Assert.False(coverage.HasCommonOverlap);
        Assert.Null(coverage.CommonOverlapStart);
        Assert.Null(coverage.CommonOverlapEnd);
    }

    [Fact]
    public async Task Unknown_facility_returns_not_found()
    {
        var response = await Client.GetAsync($"/api/facilities/{Guid.CreateVersion7()}/measurement-coverage");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Facility was not found.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Facility_with_no_equipment_returns_an_empty_list()
    {
        var facilityId = await SeedAsync("Cold Room A");

        var response = await Client.GetAsync($"/api/facilities/{facilityId}/measurement-coverage");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"earliestMeasurementAt\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"latestMeasurementAt\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"hasCommonOverlap\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"commonOverlapStart\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"commonOverlapEnd\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"equipment\":[]", json, StringComparison.Ordinal);

        var coverage = JsonSerializer.Deserialize<FacilityCoverage>(json, ColdRoomHttp.Json);
        Assert.NotNull(coverage);
        Assert.Equal(facilityId, coverage.FacilityId);
        Assert.Equal("Cold Room A", coverage.FacilityName);
        Assert.Empty(coverage.Equipment);
    }

    private static void AssertSteady(EquipmentCoverage equipment, int importCount)
    {
        Assert.Equal(At(0, 0), equipment.FirstMeasurementAt);
        Assert.Equal(DateTimeKind.Unspecified, equipment.FirstMeasurementAt!.Value.Kind);
        Assert.Equal(At(0, 10), equipment.LastMeasurementAt);
        Assert.Equal(3, equipment.SourceRowCount);
        Assert.Equal(6, equipment.MeasurementCount);
        Assert.Equal(importCount, equipment.ImportCount);
        Assert.Equal(5, equipment.ExpectedSamplingIntervalSeconds);
        Assert.Equal(0, equipment.MissingSourceSampleCount);
        Assert.Empty(equipment.Gaps);
    }

    private async Task<FacilityCoverage> GetCoverageAsync(Guid facilityId)
    {
        var response = await Client.GetAsync($"/api/facilities/{facilityId}/measurement-coverage");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadRequiredAsync<FacilityCoverage>();
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
                    db.Add(Measurement(import, timestamp, MeasurementKind.Temperature, "°C", 21.5));
                    if (series.IncludeHumidity)
                    {
                        db.Add(Measurement(import, timestamp, MeasurementKind.RelativeHumidity, "%", 40));
                    }
                }
            }
        }

        await db.SaveChangesAsync();
        return facility.Id;
    }

    private static Measurement Measurement(
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

    private static DateTime At(int minute, int second) =>
        new(2024, 1, 1, 10, minute, second, DateTimeKind.Unspecified);

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
