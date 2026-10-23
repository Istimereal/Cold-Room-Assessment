using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColdRoomAssessment.Application;
using ColdRoomAssessment.Application.Imports;
using ColdRoomAssessment.Domain;
using ColdRoomAssessment.Infrastructure;
using ColdRoomAssessment.IntegrationTests.Setup;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.IntegrationTests.Http;

[Collection(IntegrationCollection.Name)]
public sealed class MeasurementImportEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private readonly PostgresFixture _postgres = postgres;

    [Fact]
    public async Task Import_returns_a_summary_and_stores_both_measurements_for_each_valid_row()
    {
        var equipment = await CreateEquipmentAsync();

        var response = await ImportFixtureAsync(equipment.Id, "valid-dht22.csv", "logs/sensor.csv");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/equipment/{equipment.Id}/imports", Location(response));
        var summary = await ReadSummaryAsync(response);
        ColdRoomHttp.AssertVersion7(summary.Id);
        Assert.Equal(equipment.Id, summary.EquipmentId);
        Assert.Equal("SENSOR01", summary.EquipmentName);
        Assert.Equal("sensor.csv", summary.OriginalFileName);
        Assert.Equal(new DateTime(2026, 3, 15, 8, 30, 0), summary.FirstMeasurementAt);
        Assert.Equal(new DateTime(2026, 3, 16, 9, 15, 0), summary.LastMeasurementAt);
        Assert.Equal(DateTimeKind.Unspecified, summary.FirstMeasurementAt!.Value.Kind);
        Assert.Equal(2, summary.ValidRowCount);
        Assert.Equal(0, summary.InvalidRowCount);
        Assert.Equal(4, summary.MeasurementCount);
        Assert.Equal(ImportStatus.Completed, summary.Status);
        Assert.NotNull(summary.ImportedAt);
        Assert.True((DateTimeOffset.UtcNow - summary.ImportedAt.Value).Duration() < TimeSpan.FromMinutes(1));

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("2026-03-15T08:30:00", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"measurements\"", json, StringComparison.Ordinal);

        await using var db = CreateDbContext();
        var import = await db.DataImports.SingleAsync();
        Assert.Equal(summary.Id, import.Id);
        Assert.Equal(ImportStatus.Completed, import.Status);
        Assert.Equal(2, import.ImportedRowCount);
        Assert.Equal(0, import.InvalidRowCount);
        Assert.Equal(DateTimeKind.Unspecified, import.FirstMeasurementAt!.Value.Kind);

        var measurements = await db.Measurements.Where(item => item.DataImportId == import.Id).ToListAsync();
        Assert.Equal(4, measurements.Count);
        Assert.All(measurements, item => Assert.Equal(DateTimeKind.Unspecified, item.MeasuredAt.Kind));
        AssertMeasurement(measurements, new DateTime(2026, 3, 15, 8, 30, 0), MeasurementKind.Temperature, 4.5, "°C");
        AssertMeasurement(measurements, new DateTime(2026, 3, 15, 8, 30, 0), MeasurementKind.RelativeHumidity, 40, "%");
        AssertMeasurement(measurements, new DateTime(2026, 3, 16, 9, 15, 0), MeasurementKind.Temperature, 5.25, "°C");
        AssertMeasurement(measurements, new DateTime(2026, 3, 16, 9, 15, 0), MeasurementKind.RelativeHumidity, 41.5, "%");
    }

    [Fact]
    public async Task Import_accepts_comma_and_dot_as_decimal_separators()
    {
        var equipment = await CreateEquipmentAsync();

        var response = await ImportFixtureAsync(equipment.Id, "mixed-decimals.csv", "mixed.csv");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var summary = await ReadSummaryAsync(response);
        Assert.Equal(2, summary.ValidRowCount);
        Assert.Equal(0, summary.InvalidRowCount);
        Assert.Equal(4, summary.MeasurementCount);
        Assert.Equal(new DateTime(2024, 1, 2, 10, 0, 0), summary.FirstMeasurementAt);
        Assert.Equal(new DateTime(2024, 1, 2, 11, 30, 0), summary.LastMeasurementAt);

        await using var db = CreateDbContext();
        var measurements = await db.Measurements.ToListAsync();
        AssertMeasurement(measurements, new DateTime(2024, 1, 2, 10, 0, 0), MeasurementKind.Temperature, 1.234, "°C");
        AssertMeasurement(measurements, new DateTime(2024, 1, 2, 10, 0, 0), MeasurementKind.RelativeHumidity, 55.5, "%");
        AssertMeasurement(measurements, new DateTime(2024, 1, 2, 11, 30, 0), MeasurementKind.Temperature, -3.25, "°C");
        AssertMeasurement(measurements, new DateTime(2024, 1, 2, 11, 30, 0), MeasurementKind.RelativeHumidity, 60, "%");
    }

    [Fact]
    public async Task Import_counts_unusable_lines_and_keeps_the_valid_row()
    {
        var equipment = await CreateEquipmentAsync();

        var response = await ImportFixtureAsync(equipment.Id, "invalid-and-preamble.csv", "partial.csv");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var summary = await ReadSummaryAsync(response);
        Assert.Equal(1, summary.ValidRowCount);
        Assert.Equal(4, summary.InvalidRowCount);
        Assert.Equal(2, summary.MeasurementCount);
        Assert.Equal(new DateTime(2026, 3, 15, 8, 30, 0), summary.FirstMeasurementAt);

        await using var db = CreateDbContext();
        Assert.Equal(2, await db.Measurements.CountAsync());
        var measurements = await db.Measurements.ToListAsync();
        AssertMeasurement(measurements, new DateTime(2026, 3, 15, 8, 30, 0), MeasurementKind.Temperature, 19.5, "°C");
        AssertMeasurement(measurements, new DateTime(2026, 3, 15, 8, 30, 0), MeasurementKind.RelativeHumidity, 48, "%");
    }

    [Fact]
    public async Task Import_when_equipment_does_not_exist_returns_404()
    {
        using var content = FileContent("valid-dht22.csv", "sensor.csv");

        var response = await Client.PostAsync($"/api/equipment/{Guid.CreateVersion7()}/imports", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Equipment was not found.", await response.ReadErrorAsync());
        await AssertNothingStoredAsync();
    }

    [Fact]
    public async Task Import_when_the_file_is_missing_returns_400()
    {
        var equipment = await CreateEquipmentAsync();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("missing"), "note");

        var response = await Client.PostAsync($"/api/equipment/{equipment.Id}/imports", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("File is required.", await response.ReadErrorAsync());

        var emptyFile = new ByteArrayContent([]);
        emptyFile.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var emptyContent = new MultipartFormDataContent();
        emptyContent.Add(emptyFile, "file", "empty.csv");
        var emptyResponse = await Client.PostAsync($"/api/equipment/{equipment.Id}/imports", emptyContent);

        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
        Assert.Equal("File is required.", await emptyResponse.ReadErrorAsync());
        await AssertNothingStoredAsync();
    }

    [Fact]
    public async Task Import_when_the_format_is_not_recognized_returns_400_and_stores_nothing()
    {
        var equipment = await CreateEquipmentAsync();

        var response = await ImportFixtureAsync(equipment.Id, "no-header.csv", "notes.csv");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The file format was not recognized.", await response.ReadErrorAsync());
        await AssertNothingStoredAsync();
    }

    [Fact]
    public async Task Import_when_no_valid_row_remains_returns_400_and_stores_nothing()
    {
        var equipment = await CreateEquipmentAsync();

        var response = await ImportFixtureAsync(equipment.Id, "header-only.csv", "empty.csv");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The file does not contain a valid data row.", await response.ReadErrorAsync());
        await AssertNothingStoredAsync();
    }

    [Fact]
    public async Task List_returns_summaries_ordered_by_first_measurement_time()
    {
        var equipment = await CreateEquipmentAsync();
        var later = await ImportFixtureAsync(equipment.Id, "later.csv", "later.csv");
        var earlier = await ImportFixtureAsync(equipment.Id, "earlier.csv", "earlier.csv");
        Assert.Equal(HttpStatusCode.Created, later.StatusCode);
        Assert.Equal(HttpStatusCode.Created, earlier.StatusCode);
        var laterSummary = await ReadSummaryAsync(later);
        var earlierSummary = await ReadSummaryAsync(earlier);

        var response = await Client.GetAsync($"/api/equipment/{equipment.Id}/imports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"measurements\"", json, StringComparison.Ordinal);
        var summaries = JsonSerializer.Deserialize<List<ImportSummary>>(json, Json);
        Assert.NotNull(summaries);
        Assert.Equal(2, summaries.Count);
        Assert.Equal(earlierSummary.Id, summaries[0].Id);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0), summaries[0].FirstMeasurementAt);
        Assert.Equal(2, summaries[0].MeasurementCount);
        Assert.Equal(laterSummary.Id, summaries[1].Id);
        Assert.Equal(new DateTime(2024, 6, 1, 0, 0, 0), summaries[1].FirstMeasurementAt);
    }

    [Fact]
    public async Task List_when_equipment_has_no_imports_returns_an_empty_list()
    {
        var equipment = await CreateEquipmentAsync();

        var response = await Client.GetAsync($"/api/equipment/{equipment.Id}/imports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summaries = await response.Content.ReadFromJsonAsync<List<ImportSummary>>(Json);
        Assert.NotNull(summaries);
        Assert.Empty(summaries);
    }

    [Fact]
    public async Task List_when_equipment_does_not_exist_returns_404()
    {
        var response = await Client.GetAsync($"/api/equipment/{Guid.CreateVersion7()}/imports");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Equipment was not found.", await response.ReadErrorAsync());
    }

    private async Task<EquipmentResponse> CreateEquipmentAsync()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");
        var response = await Client.PostEquipmentAsync(
            facility.Id,
            equipmentType.Id,
            "SENSOR01",
            "Near entrance, upper shelf");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadRequiredAsync<EquipmentResponse>();
    }

    private Task<HttpResponseMessage> ImportFixtureAsync(Guid equipmentId, string fixtureName, string fileName)
    {
        var content = FileContent(fixtureName, fileName);
        return Client.PostAsync($"/api/equipment/{equipmentId}/imports", content);
    }

    private static MultipartFormDataContent FileContent(string fixtureName, string fileName)
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Http", "fixtures", fixtureName));
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);
        return content;
    }

    private static async Task<ImportSummary> ReadSummaryAsync(HttpResponseMessage response)
    {
        var summary = await response.Content.ReadFromJsonAsync<ImportSummary>(Json);
        Assert.NotNull(summary);
        return summary;
    }

    private ColdRoomDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ColdRoomDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;
        return new ColdRoomDbContext(options);
    }

    private async Task AssertNothingStoredAsync()
    {
        await using var db = CreateDbContext();
        Assert.Equal(0, await db.DataImports.CountAsync());
        Assert.Equal(0, await db.Measurements.CountAsync());
    }

    private static void AssertMeasurement(
        IEnumerable<Measurement> measurements,
        DateTime measuredAt,
        MeasurementKind kind,
        double value,
        string unit)
    {
        var measurement = Assert.Single(measurements, item => item.MeasuredAt == measuredAt && item.Kind == kind);
        Assert.Equal(value, measurement.Value);
        Assert.Equal(unit, measurement.Unit);
    }

    private static string Location(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Location", out var locations));
        return Assert.Single(locations);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class Dht22LogParserTests
{
    [Fact]
    public void Parse_uses_explicit_formats_when_the_current_culture_differs()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;

            var parsed = Dht22LogParser.Parse(
                """
                Date;Time;Temperature;Humidity
                15.03.2026;08:30:00;21,5;40.0
                03/15/2026;08:30:00;21.5;40
                """);

            Assert.True(parsed.FormatRecognized);
            var row = Assert.Single(parsed.Rows);
            Assert.Equal(new DateTime(2026, 3, 15, 8, 30, 0, DateTimeKind.Unspecified), row.MeasuredAt);
            Assert.Equal(21.5, row.Temperature);
            Assert.Equal(40, row.Humidity);
            Assert.Equal(1, parsed.InvalidRowCount);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Parse_rejects_a_field_that_contains_both_decimal_separators()
    {
        var parsed = Dht22LogParser.Parse(
            """
            Date;Time;Temperature;Humidity
            15.03.2026;08:30:00;1.2,3;4
            15.03.2026;08:31:00;4;1,2.3
            """);

        Assert.True(parsed.FormatRecognized);
        Assert.Empty(parsed.Rows);
        Assert.Equal(2, parsed.InvalidRowCount);
    }

    [Fact]
    public void Parse_skips_blank_lines_and_repeated_headers()
    {
        var parsed = Dht22LogParser.Parse(
            """
            before
            Date;Time;Temperature;Humidity

            Date;Time;Temperature;Humidity
            01.01.2024;00:00:00;1,234;2
            """);

        Assert.True(parsed.FormatRecognized);
        Assert.Equal(1, parsed.InvalidRowCount);
        var row = Assert.Single(parsed.Rows);
        Assert.Equal(1.234, row.Temperature);
        Assert.Equal(2, row.Humidity);
    }
}
