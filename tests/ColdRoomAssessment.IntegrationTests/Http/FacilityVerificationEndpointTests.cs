using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Application.Investigation;
using ColdRoomAssessment.Application.Verification;
using ColdRoomAssessment.Domain;
using ColdRoomAssessment.Infrastructure;
using ColdRoomAssessment.IntegrationTests.Setup;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ColdRoomAssessment.IntegrationTests.Http;

[Collection(IntegrationCollection.Name)]
public sealed class FacilityVerificationEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private static readonly JsonSerializerOptions Json = CreateJson();

    private readonly PostgresFixture _postgres = postgres;

    [Fact]
    public async Task Valid_result_is_returned_and_cites_catalog_evidence()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var evidenceId = MinimumId(sensor.EquipmentId);
        var investigator = new CountingInvestigator();
        var fake = new FakeVerifier
        {
            Responder = _ => VerificationJson(evidenceId, overall: "Supported")
        };

        var response = await PostAsync(
            fake,
            sensor.FacilityId,
            Text(At(0, 0)),
            Text(At(0, 15)),
            InvestigationBody(sensor.FacilityId, sensor.EquipmentId, evidenceId),
            investigator);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var verification = JsonSerializer.Deserialize<FacilityVerification>(body, Json);
        Assert.NotNull(verification);
        Assert.Equal(sensor.FacilityId, verification.FacilityId);
        Assert.Equal(At(0, 0), verification.From);
        Assert.Equal(DateTimeKind.Unspecified, verification.From.Kind);
        Assert.Equal(At(0, 15), verification.To);
        Assert.Equal(VerificationVerdict.Supported, verification.OverallVerdict);
        var observation = Assert.Single(verification.ObservationVerifications);
        Assert.Equal("obs-1", observation.ObservationId);
        Assert.Equal(VerificationVerdict.Supported, observation.Verdict);
        Assert.Equal("The minimum is the first stored temperature.", observation.Explanation);
        Assert.Equal(evidenceId, Assert.Single(observation.EvidenceIds));
        Assert.Equal(evidenceId, Assert.Single(observation.MissingEvidenceIds));
        Assert.Empty(observation.ConflictingEvidenceIds);
        var hypothesis = Assert.Single(verification.HypothesisVerifications);
        Assert.Equal("hyp-1", hypothesis.HypothesisId);
        Assert.Equal(VerificationVerdict.InsufficientEvidence, hypothesis.Verdict);
        Assert.Equal(evidenceId, Assert.Single(hypothesis.SupportingEvidenceIds));
        Assert.Empty(hypothesis.ContradictingEvidenceIds);
        Assert.Equal("The series is too short to separate a gap from a contradiction.", hypothesis.Uncertainty);
        Assert.Equal(["The interval does not show a cause."], verification.Limitations);
        Assert.Equal(0, investigator.Calls);
    }

    [Fact]
    public async Task Verifier_receives_regenerated_evidence_and_the_supplied_investigation()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var evidenceId = MinimumId(sensor.EquipmentId);
        var investigator = new CountingInvestigator();
        var posted = InvestigationBody(sensor.FacilityId, sensor.EquipmentId, evidenceId);
        var node = JsonNode.Parse(posted)!.AsObject();
        node["deterministicEvidence"] = new JsonArray
        {
            new JsonObject { ["id"] = "client-supplied-evidence", ["value"] = "999" }
        };
        var fake = new FakeVerifier
        {
            Responder = _ => VerificationJson(evidenceId)
        };

        var response = await PostAsync(
            fake,
            sensor.FacilityId,
            Text(At(0, 0)),
            Text(At(0, 15)),
            node.ToJsonString(),
            investigator);

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.Equal(1, fake.Calls);
        Assert.Equal(0, investigator.Calls);
        Assert.NotNull(fake.Catalog);
        Assert.NotNull(fake.Investigation);
        Assert.Equal(VerifierInstructions.Text, fake.Instructions);
        Assert.Contains("Verify rather than extend", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Use only the supplied deterministic evidence.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not invent events or causes.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Distinguish a lack of evidence from contradiction.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Use InsufficientEvidence", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Reject certainty that the supplied evidence does not support.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Report conflicting evidence", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("Cite only evidence ids present in the supplied catalog.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("No operational recommendations.", fake.Instructions, StringComparison.Ordinal);
        Assert.Contains("no knowledge of experiment actions, research conclusions", fake.Instructions, StringComparison.Ordinal);
        Assert.Equal(sensor.FacilityId, fake.Catalog.FacilityId);
        Assert.Equal("Cold Room A", fake.Catalog.FacilityName);
        Assert.Equal(At(0, 0), fake.Catalog.From);
        Assert.Equal(At(0, 15), fake.Catalog.To);
        Assert.DoesNotContain(fake.Catalog.Items, item => item.Id == "client-supplied-evidence");
        var minimum = Assert.Single(
            fake.Catalog.Items,
            item => item.EquipmentId == sensor.EquipmentId
                && item.MeasurementKind == "Temperature"
                && item.Fact == "minimum");
        Assert.Equal(evidenceId, minimum.Id);
        Assert.Equal("1", minimum.Value);
        var count = Assert.Single(
            fake.Catalog.Items,
            item => item.EquipmentId == sensor.EquipmentId
                && item.MeasurementKind == "Temperature"
                && item.Fact == "count");
        Assert.Equal("3", count.Value);
        Assert.Equal("The cited minimum is the start of the series.", fake.Investigation.OverallSummary);
        Assert.Equal("obs-1", Assert.Single(fake.Investigation.Observations).Id);
        Assert.Equal("hyp-1", Assert.Single(fake.Investigation.Hypotheses).Id);
        Assert.Equal(evidenceId, Assert.Single(fake.Investigation.Observations[0].EvidenceReferenceIds));
    }

    [Fact]
    public async Task Invented_evidence_ids_are_rejected()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var evidenceId = MinimumId(sensor.EquipmentId);
        var invented = $"eq:{sensor.EquipmentId:D}:temperature:peak:1";
        var observation = new FakeVerifier
        {
            Responder = catalog =>
            {
                Assert.False(catalog.Contains(invented));
                Assert.True(catalog.Contains(evidenceId));
                return VerificationJson(evidenceId, observationEvidenceId: invented);
            }
        };
        var hypothesis = new FakeVerifier
        {
            Responder = _ => VerificationJson(evidenceId, contradictingEvidenceId: invented)
        };

        var observationResponse = await PostInvestigationAsync(observation, sensor, evidenceId);
        var hypothesisResponse = await PostInvestigationAsync(hypothesis, sensor, evidenceId);

        Assert.Equal(HttpStatusCode.BadGateway, observationResponse.StatusCode);
        Assert.Equal(
            "The verifier cited evidence that is not in the catalog.",
            await observationResponse.ReadErrorAsync());
        Assert.DoesNotContain(invented, await observationResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadGateway, hypothesisResponse.StatusCode);
        Assert.Equal(
            "The verifier cited evidence that is not in the catalog.",
            await hypothesisResponse.ReadErrorAsync());
        Assert.DoesNotContain(invented, await hypothesisResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_observation_id_is_rejected()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var evidenceId = MinimumId(sensor.EquipmentId);
        var fake = new FakeVerifier
        {
            Responder = _ => VerificationJson(evidenceId, observationId: "obs-missing")
        };

        var response = await PostInvestigationAsync(fake, sensor, evidenceId);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(
            "The verifier cited an observation or hypothesis that is not in the investigation.",
            await response.ReadErrorAsync());
        Assert.DoesNotContain("obs-missing", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_hypothesis_id_is_rejected()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var evidenceId = MinimumId(sensor.EquipmentId);
        var fake = new FakeVerifier
        {
            Responder = _ => VerificationJson(evidenceId, hypothesisId: "hyp-missing")
        };

        var response = await PostInvestigationAsync(fake, sensor, evidenceId);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(
            "The verifier cited an observation or hypothesis that is not in the investigation.",
            await response.ReadErrorAsync());
        Assert.DoesNotContain("hyp-missing", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_verdict_is_accepted()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var evidenceId = MinimumId(sensor.EquipmentId);
        var body = InvestigationBody(sensor.FacilityId, sensor.EquipmentId, evidenceId, includeSecond: true);
        foreach (var overall in new[] { "Supported", "PartiallySupported", "NotSupported", "InsufficientEvidence" })
        {
            var fake = new FakeVerifier
            {
                Responder = _ => VerificationJson(
                    evidenceId,
                    overall: overall,
                    includeSecond: true,
                    hypothesisVerdict: "NotSupported")
            };
            var response = await PostAsync(fake, sensor.FacilityId, Text(At(0, 0)), Text(At(0, 15)), body);
            var payload = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, payload);
            var verification = JsonSerializer.Deserialize<FacilityVerification>(payload, Json);
            Assert.NotNull(verification);
            Assert.Equal(overall, verification.OverallVerdict.ToString());
            Assert.Equal(
                [VerificationVerdict.Supported, VerificationVerdict.PartiallySupported],
                verification.ObservationVerifications.Select(item => item.Verdict));
            Assert.Equal(
                [VerificationVerdict.NotSupported, VerificationVerdict.InsufficientEvidence],
                verification.HypothesisVerifications.Select(item => item.Verdict));
        }
    }

    [Fact]
    public async Task Provider_failure_returns_bad_gateway()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var evidenceId = MinimumId(sensor.EquipmentId);
        var fake = new FakeVerifier { Exception = new VerifierUnavailableException() };

        var response = await PostInvestigationAsync(fake, sensor, evidenceId);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("The verifier did not return a result.", await response.ReadErrorAsync());
        Assert.DoesNotContain("ClientResultException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-", body, StringComparison.Ordinal);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_a_provider_failure()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var evidenceId = MinimumId(sensor.EquipmentId);
        var fake = new FakeVerifier { Exception = new OperationCanceledException() };

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PostInvestigationAsync(fake, sensor, evidenceId));

        Assert.Equal(1, fake.Calls);
        Assert.DoesNotContain("The verifier did not return a result.", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("The verifier returned an invalid result.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_verifier_output_is_rejected()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var evidenceId = MinimumId(sensor.EquipmentId);
        var invalid = new FakeVerifier { Responder = _ => "{" };
        var missing = new FakeVerifier
        {
            Responder = _ => VerificationJson(evidenceId).Replace("\"overallVerdict\": \"Supported\",", string.Empty, StringComparison.Ordinal)
        };
        var badVerdict = new FakeVerifier
        {
            Responder = _ => VerificationJson(evidenceId, overall: "Certain")
        };

        var invalidResponse = await PostInvestigationAsync(invalid, sensor, evidenceId);
        var missingResponse = await PostInvestigationAsync(missing, sensor, evidenceId);
        var verdictResponse = await PostInvestigationAsync(badVerdict, sensor, evidenceId);

        Assert.Equal(HttpStatusCode.BadGateway, invalidResponse.StatusCode);
        Assert.Equal("The verifier returned an invalid result.", await invalidResponse.ReadErrorAsync());
        Assert.Equal(HttpStatusCode.BadGateway, missingResponse.StatusCode);
        Assert.Equal("The verifier returned an invalid result.", await missingResponse.ReadErrorAsync());
        Assert.Equal(HttpStatusCode.BadGateway, verdictResponse.StatusCode);
        Assert.Equal("The verifier returned an invalid result.", await verdictResponse.ReadErrorAsync());
    }

    [Fact]
    public async Task Supplied_investigation_with_unknown_evidence_is_rejected_before_the_verifier()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var invented = $"eq:{sensor.EquipmentId:D}:temperature:peak:1";
        var fake = new FakeVerifier { Responder = _ => VerificationJson(invented) };

        var response = await PostAsync(
            fake,
            sensor.FacilityId,
            Text(At(0, 0)),
            Text(At(0, 15)),
            InvestigationBody(sensor.FacilityId, sensor.EquipmentId, invented));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The investigation result is invalid.", await response.ReadErrorAsync());
        Assert.DoesNotContain(invented, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Unknown_facility_matches_the_analysis_endpoint()
    {
        var fake = new FakeVerifier();

        var response = await PostAsync(
            fake,
            Guid.CreateVersion7(),
            "2024-01-01T10:00:00",
            "2024-01-01T10:00:30",
            "{}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Facility was not found.", await response.ReadErrorAsync());
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Bad_interval_matches_the_analysis_endpoint()
    {
        var fake = new FakeVerifier();
        var facilityId = Guid.CreateVersion7();

        var missing = await PostAsync(fake, facilityId, null, "2024-01-01T10:00:30", "{}");
        var malformed = await PostAsync(fake, facilityId, "2024-01-01T10:00:00Z", "2024-01-01T10:00:30", "{}");
        var reversed = await PostAsync(fake, facilityId, "2024-01-01T10:00:30", "2024-01-01T10:00:00", "{}");
        var tooLong = await PostAsync(fake, facilityId, "2024-01-01T10:00:00", "2024-01-02T10:00:01", "{}");

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("From is required.", await missing.ReadErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("From must be yyyy-MM-ddTHH:mm:ss.", await malformed.ReadErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);
        Assert.Equal("From must be before to.", await reversed.ReadErrorAsync());
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("The requested interval is longer than 24 hours.", await tooLong.ReadErrorAsync());
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Missing_configuration_returns_service_unavailable()
    {
        var sensor = await SeedTemperatureSeriesAsync();
        var evidenceId = MinimumId(sensor.EquipmentId);

        var response = await PostRawAsync(
            Client,
            sensor.FacilityId,
            Text(At(0, 0)),
            Text(At(0, 15)),
            InvestigationBody(sensor.FacilityId, sensor.EquipmentId, evidenceId));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Verifier is not configured.", await response.ReadErrorAsync());
    }

    private Task<HttpResponseMessage> PostInvestigationAsync(FakeVerifier verifier, SeededSensor sensor, string evidenceId) =>
        PostAsync(
            verifier,
            sensor.FacilityId,
            Text(At(0, 0)),
            Text(At(0, 15)),
            InvestigationBody(sensor.FacilityId, sensor.EquipmentId, evidenceId));

    private async Task<HttpResponseMessage> PostAsync(
        FakeVerifier verifier,
        Guid facilityId,
        string? from,
        string? to,
        string? body,
        CountingInvestigator? investigator = null)
    {
        await using var factory = CreateFactory(verifier, investigator);
        using var client = factory.CreateClient();
        var response = await PostRawAsync(client, facilityId, from, to, body);
        await response.Content.LoadIntoBufferAsync();
        return response;
    }

    private WebApplicationFactory<Program> CreateFactory(FakeVerifier verifier, CountingInvestigator? investigator) =>
        _postgres.ApiFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IVerifierClient>();
                services.AddSingleton<IVerifierClient>(verifier);
                if (investigator is not null)
                {
                    services.RemoveAll<IInvestigatorClient>();
                    services.AddSingleton<IInvestigatorClient>(investigator);
                }
            });
        });

    private static Task<HttpResponseMessage> PostRawAsync(
        HttpClient client,
        Guid facilityId,
        string? from,
        string? to,
        string? body)
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
        var content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json");
        return client.PostAsync($"/api/facilities/{facilityId}/verifications{suffix}", content);
    }

    private static string InvestigationBody(
        Guid facilityId,
        Guid equipmentId,
        string evidenceId,
        bool includeSecond = false)
    {
        var investigation = new FacilityInvestigation(
            facilityId,
            At(0, 0),
            At(0, 15),
            "The cited minimum is the start of the series.",
            includeSecond
                ?
                [
                    Observation(equipmentId, evidenceId, "obs-1"),
                    Observation(equipmentId, evidenceId, "obs-2")
                ]
                : [Observation(equipmentId, evidenceId, "obs-1")],
            includeSecond
                ?
                [
                    Hypothesis("hyp-1"),
                    Hypothesis("hyp-2")
                ]
                : [Hypothesis("hyp-1")],
            [
                new InvestigationLimitation(
                    InvestigationLimitationCode.InsufficientEvidence,
                    null,
                    "The series does not show a cause.")
            ]);
        return JsonSerializer.Serialize(investigation, Json);
    }

    private static InvestigationObservation Observation(Guid equipmentId, string evidenceId, string id) =>
        new(
            id,
            "Starting temperature",
            "The first valid temperature is the minimum.",
            [equipmentId],
            MeasurementKind.Temperature,
            At(0, 0),
            At(0, 15),
            [evidenceId],
            InvestigationConfidence.Medium);

    private static InvestigationHypothesis Hypothesis(string id) =>
        new(
            id,
            "The series does not show a cause for the minimum.",
            ["obs-1"],
            ["The minimum is the first sample."],
            ["The series is short."],
            InvestigationConfidence.Low);

    private static string VerificationJson(
        string evidenceId,
        string overall = "Supported",
        string observationId = "obs-1",
        string hypothesisId = "hyp-1",
        string? observationEvidenceId = null,
        string? contradictingEvidenceId = null,
        bool includeSecond = false,
        string hypothesisVerdict = "InsufficientEvidence")
    {
        var cited = observationEvidenceId ?? evidenceId;
        var contradicting = contradictingEvidenceId ?? string.Empty;
        var contradictingJson = contradicting.Length == 0 ? string.Empty : $"\"{contradicting}\"";
        var secondObservation = includeSecond
            ? $$"""
              ,
              {
                "observationId": "obs-2",
                "verdict": "PartiallySupported",
                "explanation": "Only part of the claim is present in the catalog.",
                "evidenceIds": ["{{evidenceId}}"],
                "missingEvidenceIds": [],
                "conflictingEvidenceIds": []
              }
              """
            : string.Empty;
        var secondHypothesis = includeSecond
            ? $$"""
              ,
              {
                "hypothesisId": "hyp-2",
                "verdict": "InsufficientEvidence",
                "explanation": "The catalog does not decide this hypothesis.",
                "supportingEvidenceIds": [],
                "contradictingEvidenceIds": [],
                "uncertainty": "Another sensor would be needed, and none is in the catalog."
              }
              """
            : string.Empty;
        return $$"""
            {
              "facilityId": "00000000-0000-0000-0000-000000000000",
              "from": "2000-01-01T00:00:00",
              "to": "2000-01-01T00:00:01",
              "overallVerdict": "{{overall}}",
              "observationVerifications": [
                {
                  "observationId": "{{observationId}}",
                  "verdict": "Supported",
                  "explanation": "The minimum is the first stored temperature.",
                  "evidenceIds": ["{{cited}}"],
                  "missingEvidenceIds": ["{{evidenceId}}"],
                  "conflictingEvidenceIds": []
                }{{secondObservation}}
              ],
              "hypothesisVerifications": [
                {
                  "hypothesisId": "{{hypothesisId}}",
                  "verdict": "{{hypothesisVerdict}}",
                  "explanation": "The catalog does not show a cause.",
                  "supportingEvidenceIds": ["{{evidenceId}}"],
                  "contradictingEvidenceIds": [{{contradictingJson}}],
                  "uncertainty": "The series is too short to separate a gap from a contradiction."
                }{{secondHypothesis}}
              ],
              "limitations": ["The interval does not show a cause."]
            }
            """;
    }

    private async Task<SeededSensor> SeedTemperatureSeriesAsync()
    {
        var sensor = Equipment("SENSOR01", "Near entrance");
        AddTemps(sensor, (At(0, 0), 1), (At(0, 5), 2), (At(0, 10), 3));
        var facilityId = await SeedAsync("Cold Room A", sensor);
        return new SeededSensor(facilityId, sensor.Id);
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

    private static string MinimumId(Guid equipmentId) => $"eq:{equipmentId:D}:temperature:minimum";

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
            spec.Rows.Add(new StoredRow(at, MeasurementKind.Temperature, value));
        }
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

    private sealed class CountingInvestigator : IInvestigatorClient
    {
        public int Calls { get; private set; }

        public Task<string> InvestigateAsync(
            EvidenceCatalog catalog,
            string instructions,
            CancellationToken cancellationToken,
            IReadOnlyList<ContextEvent>? contextEvents = null)
        {
            Calls++;
            throw new InvalidOperationException("The verifier must not call the investigator.");
        }
    }

    private sealed class FakeVerifier : IVerifierClient
    {
        public int Calls { get; private set; }

        public EvidenceCatalog? Catalog { get; private set; }

        public FacilityInvestigation? Investigation { get; private set; }

        public string? Instructions { get; private set; }

        public Exception? Exception { get; init; }

        public IReadOnlyList<ContextEvent>? ContextEvents { get; private set; }

        public Func<EvidenceCatalog, string>? Responder { get; init; }

        public Task<string> VerifyAsync(
            EvidenceCatalog catalog,
            FacilityInvestigation investigation,
            string instructions,
            CancellationToken cancellationToken,
            IReadOnlyList<ContextEvent>? contextEvents = null)
        {
            Calls++;
            Catalog = catalog;
            Investigation = investigation;
            Instructions = instructions;
            ContextEvents = contextEvents;
            cancellationToken.ThrowIfCancellationRequested();
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult((Responder ?? throw new InvalidOperationException("The verifier client was called."))(catalog));
        }
    }
}

public sealed class OpenAIVerifierClientTests
{
    [Fact]
    public async Task Missing_configuration_fails_before_any_provider_call()
    {
        var catalog = EvidenceCatalog.FromAnalysis(EmptyAnalysis());
        var investigation = new FacilityInvestigation(
            Guid.CreateVersion7(),
            new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2024, 1, 1, 10, 0, 5, DateTimeKind.Unspecified),
            "Summary",
            [],
            [],
            []);
        var cases = new[]
        {
            new OpenAIOptions(),
            new OpenAIOptions { ApiKey = " ", Model = " " },
            new OpenAIOptions { Model = "configured-model" },
            new OpenAIOptions { ApiKey = "test-key", VerifierModel = " " },
            new OpenAIOptions { VerifierModel = "configured-verifier", Model = "configured-model" }
        };

        foreach (var options in cases)
        {
            var client = new OpenAIVerifierClient(Options.Create(options));
            var exception = await Assert.ThrowsAsync<VerifierNotConfiguredException>(() =>
                client.VerifyAsync(catalog, investigation, VerifierInstructions.Text, CancellationToken.None));
            Assert.Equal("Verifier is not configured.", exception.Message);
        }
    }

    [Fact]
    public void Verifier_model_falls_back_to_the_shared_model_without_a_provider_call()
    {
        Assert.Null(new OpenAIOptions().ResolveVerifierModel());
        Assert.Equal("shared-model", new OpenAIOptions { Model = " shared-model " }.ResolveVerifierModel());
        Assert.Equal(
            "shared-model",
            new OpenAIOptions { Model = "shared-model", VerifierModel = " " }.ResolveVerifierModel());
        Assert.Equal(
            "verifier-model",
            new OpenAIOptions { Model = "shared-model", VerifierModel = " verifier-model " }.ResolveVerifierModel());
    }

    private static FacilityAnalysis EmptyAnalysis()
    {
        var kind = new KindAnalysis(
            0, null, null, null, null, null, null, null, null, null, null, null, null, null, []);
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
                    Guid.CreateVersion7(),
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
