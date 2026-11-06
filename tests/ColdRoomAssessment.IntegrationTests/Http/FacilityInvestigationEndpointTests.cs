using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Application.Investigation;
using ColdRoomAssessment.Domain;
using ColdRoomAssessment.Infrastructure;
using ColdRoomAssessment.IntegrationTests.Setup;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ColdRoomAssessment.IntegrationTests.Http;

[Collection(IntegrationCollection.Name)]
public sealed class FacilityInvestigationEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private static readonly JsonSerializerOptions Json = CreateJson();

    private readonly PostgresFixture _postgres = postgres;

    [Fact]
    public async Task Evidence_includes_coverage_ambiguity_and_separate_equipment()
    {
        var first = Equipment("SENSOR01", "Near entrance, upper shelf");
        Add(first, At(0, 0), MeasurementKind.Temperature, 1);
        Add(first, At(0, 0), MeasurementKind.RelativeHumidity, 40);
        Add(first, At(0, 0), MeasurementKind.RelativeHumidity, 41);
        Add(first, At(0, 5), MeasurementKind.Temperature, 3);
        Add(first, At(0, 5), MeasurementKind.RelativeHumidity, 50);
        Add(first, At(0, 10), MeasurementKind.Temperature, 2);
        var second = Equipment("SENSOR02", "Far wall");
        Add(second, At(0, 0), MeasurementKind.Temperature, 10);
        var facilityId = await SeedAsync("Cold Room A", first, second);
        var fake = new FakeInvestigator
        {
            Responder = catalog =>
            {
                var minimum = TemperatureMinimum(catalog);
                return InvestigationJson(minimum.EquipmentId, minimum.Id, to: "2024-01-01T10:00:20");
            }
        };

        var analysis = await GetAnalysisAsync(facilityId, At(0, 0), At(0, 20));
        var response = await PostAsync(fake, facilityId, Text(At(0, 0)), Text(At(0, 20)));

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.Equal(1, fake.Calls);
        Assert.NotNull(fake.Catalog);
        Assert.Equal(InvestigatorInstructions.Text, fake.Instructions);
        Assert.Contains("Use only the supplied evidence.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Separate observations from hypotheses.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not claim a cause the evidence does not show.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("coverage, ambiguity, and missing data", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("supplied id and name", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Cite catalog ids.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not construct new evidence ids.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("even if it matches the id pattern", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("No operational advice.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("No experiment actions or room events.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Say when the evidence is insufficient.", fake.Instructions, StringComparison.Ordinal);

        Assert.Equal(facilityId, fake.Catalog.FacilityId);
        Assert.Equal("Cold Room A", fake.Catalog.FacilityName);
        Assert.Equal(At(0, 0), fake.Catalog.From);
        Assert.Equal(At(0, 20), fake.Catalog.To);
        Assert.Equal(["SENSOR01", "SENSOR02"], analysis.Equipment.Select(item => item.EquipmentName));
        Assert.NotEqual(analysis.Equipment[0].EquipmentId, analysis.Equipment[1].EquipmentId);
        Assert.Equal(75.0m, analysis.Equipment[0].CoveragePercent);
        Assert.Equal(1, analysis.Equipment[0].AmbiguousSampleCount);
        Assert.Equal(1, analysis.Equipment[0].MissingExpectedSampleCount);
        Assert.Equal(25.0m, analysis.Equipment[1].CoveragePercent);
        Assert.Equal(0, analysis.Equipment[1].AmbiguousSampleCount);
        Assert.Equal(3, analysis.Equipment[1].MissingExpectedSampleCount);
        Assert.Equal(54, fake.Catalog.Items.Count);

        AssertKindCopied(fake.Catalog, analysis.Equipment[0], "Temperature", "temperature", analysis.Equipment[0].Temperature);
        AssertKindCopied(fake.Catalog, analysis.Equipment[0], "RelativeHumidity", "relative-humidity", analysis.Equipment[0].RelativeHumidity);
        AssertKindCopied(fake.Catalog, analysis.Equipment[1], "Temperature", "temperature", analysis.Equipment[1].Temperature);
        AssertKindCopied(fake.Catalog, analysis.Equipment[1], "RelativeHumidity", "relative-humidity", analysis.Equipment[1].RelativeHumidity);
        Assert.Contains(fake.Catalog.Items, item => item.EquipmentId == analysis.Equipment[0].EquipmentId && item.Fact == "rise:1");
        Assert.Contains(fake.Catalog.Items, item => item.EquipmentId == analysis.Equipment[0].EquipmentId && item.Fact == "fall:1");
        Assert.DoesNotContain(fake.Catalog.Items, item => item.Fact.StartsWith("peak:", StringComparison.Ordinal));
        Assert.DoesNotContain(fake.Catalog.Items, item => item.Fact.StartsWith("dip:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Valid_fake_result_is_returned()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        AddTemps(sensor, (At(0, 0), 1), (At(0, 5), 2), (At(0, 10), 3));
        var facilityId = await SeedAsync("Cold Room A", sensor);
        var fake = new FakeInvestigator
        {
            Responder = catalog =>
            {
                var minimum = TemperatureMinimum(catalog);
                return InvestigationJson(minimum.EquipmentId, minimum.Id, includeSecondObservation: true);
            }
        };

        var response = await PostAsync(fake, facilityId, Text(At(0, 0)), Text(At(0, 15)));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var investigation = JsonSerializer.Deserialize<FacilityInvestigation>(body, Json);
        Assert.NotNull(investigation);
        Assert.Equal(facilityId, investigation.FacilityId);
        Assert.Equal(At(0, 0), investigation.From);
        Assert.Equal(DateTimeKind.Unspecified, investigation.From.Kind);
        Assert.Equal(At(0, 15), investigation.To);
        Assert.Equal("The cited minimum is the start of the series.", investigation.OverallSummary);
        Assert.Equal(2, investigation.Observations.Count);
        var observation = investigation.Observations[0];
        Assert.Equal("obs-1", observation.Id);
        Assert.Equal("Starting temperature", observation.Title);
        Assert.Equal(sensor.Id, Assert.Single(observation.EquipmentIds));
        Assert.Equal(MeasurementKind.Temperature, observation.MeasurementKind);
        Assert.Equal(At(0, 0), observation.From);
        Assert.Equal(At(0, 15), observation.To);
        Assert.Equal($"eq:{sensor.Id:D}:temperature:minimum", Assert.Single(observation.EvidenceReferenceIds));
        Assert.Equal(InvestigationConfidence.Medium, observation.Confidence);
        Assert.Null(investigation.Observations[1].MeasurementKind);
        var hypothesis = Assert.Single(investigation.Hypotheses);
        Assert.Equal("hyp-1", hypothesis.Id);
        Assert.Equal(["obs-1"], hypothesis.SupportingObservationIds);
        Assert.Equal(InvestigationConfidence.Low, hypothesis.Confidence);
        Assert.Equal(2, investigation.Limitations.Count);
        Assert.Equal(InvestigationLimitationCode.InsufficientEvidence, investigation.Limitations[0].Code);
        Assert.Null(investigation.Limitations[0].EquipmentId);
        Assert.Equal(InvestigationLimitationCode.IncompleteCoverage, investigation.Limitations[1].Code);
        Assert.Equal(sensor.Id, investigation.Limitations[1].EquipmentId);
    }

    [Fact]
    public async Task Unknown_equipment_ids_are_rejected()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var unknown = Guid.CreateVersion7();
        var observation = new FakeInvestigator
        {
            Responder = catalog =>
            {
                var minimum = TemperatureMinimum(catalog);
                return InvestigationJson(minimum.EquipmentId, minimum.Id, observationEquipmentId: unknown);
            }
        };
        var limitation = new FakeInvestigator
        {
            Responder = catalog =>
            {
                var minimum = TemperatureMinimum(catalog);
                return InvestigationJson(minimum.EquipmentId, minimum.Id, limitationEquipmentId: unknown);
            }
        };

        var observationResponse = await PostAsync(observation, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)));
        var limitationResponse = await PostAsync(limitation, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)));

        Assert.Equal(HttpStatusCode.BadGateway, observationResponse.StatusCode);
        Assert.Equal("The investigator returned an invalid result.", await observationResponse.ReadErrorAsync());
        Assert.Equal(HttpStatusCode.BadGateway, limitationResponse.StatusCode);
        Assert.Equal("The investigator returned an invalid result.", await limitationResponse.ReadErrorAsync());
    }

    [Fact]
    public async Task Evidence_ids_outside_the_catalog_are_rejected()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        string? rejected = null;
        var fake = new FakeInvestigator
        {
            Responder = catalog =>
            {
                var minimum = TemperatureMinimum(catalog);
                rejected = $"eq:{minimum.EquipmentId:D}:temperature:peak:1";
                Assert.True(catalog.Contains(minimum.Id));
                Assert.False(catalog.Contains(rejected));
                return InvestigationJson(minimum.EquipmentId, rejected);
            }
        };

        var response = await PostAsync(fake, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("\"error\":\"The investigator returned an invalid result.\"", body, StringComparison.Ordinal);
        Assert.NotNull(rejected);
        Assert.DoesNotContain(rejected, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_supporting_observation_ids_are_rejected()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var fake = new FakeInvestigator
        {
            Responder = catalog =>
            {
                var minimum = TemperatureMinimum(catalog);
                return InvestigationJson(minimum.EquipmentId, minimum.Id, supportingObservationId: "obs-missing");
            }
        };

        var response = await PostAsync(fake, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("The investigator returned an invalid result.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Confidence_outside_the_set_is_rejected()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var observation = new FakeInvestigator
        {
            Responder = catalog =>
            {
                var minimum = TemperatureMinimum(catalog);
                return InvestigationJson(minimum.EquipmentId, minimum.Id, confidence: "Certain");
            }
        };
        var hypothesis = new FakeInvestigator
        {
            Responder = catalog =>
            {
                var minimum = TemperatureMinimum(catalog);
                return InvestigationJson(minimum.EquipmentId, minimum.Id, hypothesisConfidence: "Certain");
            }
        };

        var observationResponse = await PostAsync(observation, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)));
        var hypothesisResponse = await PostAsync(hypothesis, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)));

        Assert.Equal(HttpStatusCode.BadGateway, observationResponse.StatusCode);
        Assert.Equal("The investigator returned an invalid result.", await observationResponse.ReadErrorAsync());
        Assert.Equal(HttpStatusCode.BadGateway, hypothesisResponse.StatusCode);
        Assert.Equal("The investigator returned an invalid result.", await hypothesisResponse.ReadErrorAsync());
    }

    [Fact]
    public async Task Limitation_code_outside_the_set_is_rejected()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var fake = new FakeInvestigator
        {
            Responder = catalog =>
            {
                var minimum = TemperatureMinimum(catalog);
                return InvestigationJson(minimum.EquipmentId, minimum.Id, limitationCode: "CauseUnknown");
            }
        };

        var response = await PostAsync(fake, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("The investigator returned an invalid result.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Measurement_kind_outside_the_set_is_rejected()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var fake = new FakeInvestigator
        {
            Responder = catalog =>
            {
                var minimum = TemperatureMinimum(catalog);
                return InvestigationJson(minimum.EquipmentId, minimum.Id)
                    .Replace("\"measurementKind\": \"Temperature\"", "\"measurementKind\": \"Pressure\"", StringComparison.Ordinal);
            }
        };

        var response = await PostAsync(fake, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("The investigator returned an invalid result.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Invalid_json_and_a_missing_required_field_are_rejected()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var invalid = new FakeInvestigator { Responder = _ => "{" };
        var missing = new FakeInvestigator
        {
            Responder = catalog =>
            {
                var minimum = TemperatureMinimum(catalog);
                return InvestigationJson(minimum.EquipmentId, minimum.Id, includeSummary: false);
            }
        };

        var invalidResponse = await PostAsync(invalid, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)));
        var missingResponse = await PostAsync(missing, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)));

        Assert.Equal(HttpStatusCode.BadGateway, invalidResponse.StatusCode);
        Assert.Equal("The investigator returned an invalid result.", await invalidResponse.ReadErrorAsync());
        Assert.Equal(HttpStatusCode.BadGateway, missingResponse.StatusCode);
        Assert.Equal("The investigator returned an invalid result.", await missingResponse.ReadErrorAsync());
    }

    [Fact]
    public async Task Provider_failure_returns_bad_gateway()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var fake = new FakeInvestigator { Exception = new InvestigatorUnavailableException() };

        var response = await PostAsync(fake, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("The investigator did not return a result.", await response.ReadErrorAsync());
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_a_provider_failure()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var fake = new FakeInvestigator { Exception = new OperationCanceledException() };

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PostAsync(fake, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15))));

        Assert.Equal(1, fake.Calls);
        Assert.DoesNotContain("The investigator did not return a result.", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("The investigator returned an invalid result.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_facility_matches_the_analysis_endpoint()
    {
        var fake = new FakeInvestigator();

        var response = await PostAsync(
            fake,
            Guid.CreateVersion7(),
            "2024-01-01T10:00:00",
            "2024-01-01T10:00:30");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Facility was not found.", await response.ReadErrorAsync());
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Bad_interval_matches_the_analysis_endpoint()
    {
        var fake = new FakeInvestigator();
        var facilityId = Guid.CreateVersion7();

        var missing = await PostAsync(fake, facilityId, null, "2024-01-01T10:00:30");
        var malformed = await PostAsync(fake, facilityId, "2024-01-01T10:00:00Z", "2024-01-01T10:00:30");
        var reversed = await PostAsync(fake, facilityId, "2024-01-01T10:00:30", "2024-01-01T10:00:00");

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("From is required.", await missing.ReadErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("From must be yyyy-MM-ddTHH:mm:ss.", await malformed.ReadErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);
        Assert.Equal("From must be before to.", await reversed.ReadErrorAsync());
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Interval_longer_than_24_hours_matches_the_analysis_endpoint()
    {
        var fake = new FakeInvestigator();

        var response = await PostAsync(
            fake,
            Guid.CreateVersion7(),
            "2024-01-01T10:00:00",
            "2024-01-02T10:00:01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The requested interval is longer than 24 hours.", await response.ReadErrorAsync());
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Missing_configuration_returns_service_unavailable()
    {
        var facilityId = await SeedAsync("Cold Room A", Equipment("SENSOR01", "Near entrance"));

        var response = await PostRawAsync(Client, facilityId, Text(At(0, 0)), Text(At(0, 5)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Investigator is not configured.", await response.ReadErrorAsync());
    }

    private async Task<HttpResponseMessage> PostAsync(
        FakeInvestigator investigator,
        Guid facilityId,
        string? from,
        string? to)
    {
        await using var factory = CreateFactory(investigator);
        using var client = factory.CreateClient();
        var response = await PostRawAsync(client, facilityId, from, to);
        await response.Content.LoadIntoBufferAsync();
        return response;
    }

    private WebApplicationFactory<Program> CreateFactory(FakeInvestigator investigator) =>
        _postgres.ApiFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInvestigatorClient>();
                services.AddSingleton<IInvestigatorClient>(investigator);
            });
        });

    private static Task<HttpResponseMessage> PostRawAsync(
        HttpClient client,
        Guid facilityId,
        string? from,
        string? to)
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
        return client.PostAsync($"/api/facilities/{facilityId}/investigations{suffix}", content: null);
    }

    private async Task<SeededSensor> SeedTemperatureSeriesAsync()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        AddTemps(sensor, (At(0, 0), 1), (At(0, 5), 2), (At(0, 10), 3));
        var facilityId = await SeedAsync("Cold Room A", sensor);
        return new SeededSensor(facilityId, sensor.Id);
    }

    private async Task<FacilityAnalysis> GetAnalysisAsync(Guid facilityId, DateTime from, DateTime to)
    {
        var response = await Client.GetAsync(
            $"/api/facilities/{facilityId}/analysis?from={Uri.EscapeDataString(Text(from))}&to={Uri.EscapeDataString(Text(to))}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var analysis = await response.Content.ReadFromJsonAsync<FacilityAnalysis>(Json);
        Assert.NotNull(analysis);
        return analysis;
    }

    private static void AssertKindCopied(
        EvidenceCatalog catalog,
        EquipmentAnalysis equipment,
        string measurementKind,
        string slug,
        KindAnalysis kind)
    {
        AssertDecimal(catalog, equipment, measurementKind, slug, "coverage", equipment.CoveragePercent);
        AssertInteger(catalog, equipment, measurementKind, slug, "missing-count", equipment.MissingExpectedSampleCount);
        AssertInteger(catalog, equipment, measurementKind, slug, "ambiguous-count", equipment.AmbiguousSampleCount);
        AssertInteger(catalog, equipment, measurementKind, slug, "count", kind.Count);
        AssertNumber(catalog, equipment, measurementKind, slug, "minimum", kind.Min, kind.MinAt);
        AssertNumber(catalog, equipment, measurementKind, slug, "maximum", kind.Max, kind.MaxAt);
        AssertNumber(catalog, equipment, measurementKind, slug, "mean", kind.Mean, null);
        AssertNumber(catalog, equipment, measurementKind, slug, "median", kind.Median, null);
        AssertNumber(catalog, equipment, measurementKind, slug, "population-standard-deviation", kind.PopulationStdDev, null);
        AssertNumber(catalog, equipment, measurementKind, slug, "first", kind.First, kind.FirstAt);
        AssertNumber(catalog, equipment, measurementKind, slug, "last", kind.Last, kind.LastAt);
        AssertNumber(catalog, equipment, measurementKind, slug, "change", kind.Change, null);
        AssertNumber(catalog, equipment, measurementKind, slug, "duration-seconds", kind.DurationSeconds, null);

        var peaks = 0;
        var dips = 0;
        foreach (var seriesEvent in kind.Events)
        {
            if (seriesEvent.Kind == SeriesEventKind.Peak)
            {
                peaks++;
                AssertNumber(catalog, equipment, measurementKind, slug, $"peak:{peaks}", seriesEvent.Value, seriesEvent.Timestamp);
            }
            else
            {
                dips++;
                AssertNumber(catalog, equipment, measurementKind, slug, $"dip:{dips}", seriesEvent.Value, seriesEvent.Timestamp);
            }
        }
    }

    private static void AssertDecimal(
        EvidenceCatalog catalog,
        EquipmentAnalysis equipment,
        string measurementKind,
        string slug,
        string fact,
        decimal expected)
    {
        var item = Require(catalog, equipment, measurementKind, slug, fact);
        Assert.NotNull(item.Value);
        Assert.Equal(expected, decimal.Parse(item.Value, CultureInfo.InvariantCulture));
        Assert.Null(item.Timestamp);
    }

    private static void AssertInteger(
        EvidenceCatalog catalog,
        EquipmentAnalysis equipment,
        string measurementKind,
        string slug,
        string fact,
        int expected)
    {
        var item = Require(catalog, equipment, measurementKind, slug, fact);
        Assert.Equal(expected.ToString(CultureInfo.InvariantCulture), item.Value);
        Assert.Null(item.Timestamp);
    }

    private static void AssertNumber(
        EvidenceCatalog catalog,
        EquipmentAnalysis equipment,
        string measurementKind,
        string slug,
        string fact,
        double? expected,
        DateTime? timestamp)
    {
        var item = Require(catalog, equipment, measurementKind, slug, fact);
        if (expected is null)
        {
            Assert.Null(item.Value);
        }
        else
        {
            Assert.NotNull(item.Value);
            Assert.Equal(expected.Value, double.Parse(item.Value, CultureInfo.InvariantCulture));
        }

        Assert.Equal(Timestamp(timestamp), item.Timestamp);
    }

    private static EvidenceItem Require(
        EvidenceCatalog catalog,
        EquipmentAnalysis equipment,
        string measurementKind,
        string slug,
        string fact)
    {
        var item = Assert.Single(
            catalog.Items,
            candidate => candidate.EquipmentId == equipment.EquipmentId
                && candidate.MeasurementKind == measurementKind
                && candidate.Fact == fact);
        Assert.Equal($"eq:{equipment.EquipmentId:D}:{slug}:{fact}", item.Id);
        Assert.Equal(equipment.EquipmentName, item.EquipmentName);
        return item;
    }

    private static EvidenceItem TemperatureMinimum(EvidenceCatalog catalog) =>
        Assert.Single(
            catalog.Items,
            item => item.EquipmentName == "SENSOR01"
                && item.MeasurementKind == "Temperature"
                && item.Fact == "minimum");

    private static string InvestigationJson(
        Guid equipmentId,
        string evidenceId,
        string confidence = "Medium",
        string hypothesisConfidence = "Low",
        string supportingObservationId = "obs-1",
        Guid? observationEquipmentId = null,
        Guid? limitationEquipmentId = null,
        string limitationCode = "IncompleteCoverage",
        string to = "2024-01-01T10:00:15",
        bool includeSummary = true,
        bool includeSecondObservation = false)
    {
        var citedEquipment = observationEquipmentId ?? equipmentId;
        var limitationEquipment = limitationEquipmentId ?? equipmentId;
        var summary = includeSummary
            ? """
              "facilityId": "00000000-0000-0000-0000-000000000000",
              "from": "2000-01-01T00:00:00",
              "to": "2000-01-01T00:00:01",
              "overallSummary": "The cited minimum is the start of the series.",
              """
            : """
              "facilityId": "00000000-0000-0000-0000-000000000000",
              """;
        var secondObservation = includeSecondObservation
            ? $$"""
              ,
              {
                "id": "obs-2",
                "title": "Unspecified kind",
                "description": "This note is not tied to one measurement kind.",
                "equipmentIds": ["{{equipmentId}}"],
                "measurementKind": null,
                "from": "2024-01-01T10:00:00",
                "to": "{{to}}",
                "evidenceReferenceIds": ["{{evidenceId}}"],
                "confidence": "Low"
              }
              """
            : string.Empty;
        return $$"""
            {
              {{summary}}
              "observations": [
                {
                  "id": "obs-1",
                  "title": "Starting temperature",
                  "description": "The first valid temperature is the minimum.",
                  "equipmentIds": ["{{citedEquipment}}"],
                  "measurementKind": "Temperature",
                  "from": "2024-01-01T10:00:00",
                  "to": "{{to}}",
                  "evidenceReferenceIds": ["{{evidenceId}}"],
                  "confidence": "{{confidence}}"
                }{{secondObservation}}
              ],
              "hypotheses": [
                {
                  "id": "hyp-1",
                  "statement": "The series does not show a cause for the minimum.",
                  "supportingObservationIds": ["{{supportingObservationId}}"],
                  "evidenceFor": ["The minimum is the first sample."],
                  "uncertainties": ["The series is short."],
                  "confidence": "{{hypothesisConfidence}}"
                }
              ],
              "limitations": [
                {
                  "code": "InsufficientEvidence",
                  "equipmentId": null,
                  "description": "The series does not show a cause."
                },
                {
                  "code": "{{limitationCode}}",
                  "equipmentId": "{{limitationEquipment}}",
                  "description": "The cited equipment is the one in the summary."
                }
              ]
            }
            """;
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

    private static string? Timestamp(DateTime? time) =>
        time?.ToString(AnalysisPeriodService.TimestampFormat, CultureInfo.InvariantCulture);

    private static DateTime At(int minute, int second) =>
        new(2024, 1, 1, 10, minute, second, DateTimeKind.Unspecified);

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record SeededSensor(Guid FacilityId, Guid EquipmentId);

    private sealed class EquipmentSpec
    {
        public required string Name { get; init; }

        public required string Placement { get; init; }

        public string TypeName { get; init; } = "DHT22 Logger";

        public Guid Id { get; set; }

        public List<StoredRow> Rows { get; } = [];
    }

    private sealed record StoredRow(DateTime MeasuredAt, MeasurementKind Kind, double Value);

    private sealed class FakeInvestigator : IInvestigatorClient
    {
        public int Calls { get; private set; }

        public EvidenceCatalog? Catalog { get; private set; }

        public string? Instructions { get; private set; }

        public Exception? Exception { get; init; }

        public Func<EvidenceCatalog, string>? Responder { get; init; }

        public IReadOnlyList<ContextEvent>? ContextEvents { get; private set; }

        public Task<string> InvestigateAsync(
            EvidenceCatalog catalog,
            string instructions,
            CancellationToken cancellationToken,
            IReadOnlyList<ContextEvent>? contextEvents = null)
        {
            Calls++;
            Catalog = catalog;
            Instructions = instructions;
            ContextEvents = contextEvents;
            cancellationToken.ThrowIfCancellationRequested();
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult((Responder ?? throw new InvalidOperationException("The investigator client was called."))(catalog));
        }
    }
}

public sealed class OpenAIInvestigatorClientTests
{
    [Fact]
    public async Task Missing_configuration_fails_before_any_provider_call()
    {
        var catalog = EvidenceCatalog.FromAnalysis(EmptyAnalysis());
        var cases = new[]
        {
            new OpenAIOptions(),
            new OpenAIOptions { ApiKey = " ", Model = " " },
            new OpenAIOptions { Model = "configured-model" }
        };

        foreach (var options in cases)
        {
            var client = new OpenAIInvestigatorClient(
                Options.Create(options),
                NullLogger<OpenAIInvestigatorClient>.Instance);
            var exception = await Assert.ThrowsAsync<InvestigatorNotConfiguredException>(() =>
                client.InvestigateAsync(catalog, InvestigatorInstructions.Text, CancellationToken.None));
            Assert.Equal("Investigator is not configured.", exception.Message);
        }
    }

    private static FacilityAnalysis EmptyAnalysis()
    {
        var kind = new KindAnalysis(
            0, null, null, null, null, null, null, null, null, null, null, null, null, null, []);
        var equipmentId = Guid.CreateVersion7();
        return new FacilityAnalysis(
            Guid.CreateVersion7(),
            "Cold Room A",
            new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2024, 1, 1, 10, 0, 5, DateTimeKind.Unspecified),
            1,
            0,
            0,
            0,
            0,
            [
                new EquipmentAnalysis(
                    equipmentId,
                    "SENSOR01",
                    Guid.CreateVersion7(),
                    "DHT22 Logger",
                    "Near entrance",
                    0,
                    0,
                    0m,
                    false,
                    0,
                    1,
                    kind,
                    kind,
                    [])
            ]);
    }
}
