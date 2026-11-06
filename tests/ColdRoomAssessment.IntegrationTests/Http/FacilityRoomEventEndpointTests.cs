using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColdRoomAssessment.Application.Events;
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

namespace ColdRoomAssessment.IntegrationTests.Http;

[Collection(IntegrationCollection.Name)]
public sealed class FacilityRoomEventEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private const string From = "2024-06-01T10:00:00";
    private const string To = "2024-06-01T10:00:15";

    private static readonly JsonSerializerOptions Json = CreateJson();

    private readonly PostgresFixture _postgres = postgres;

    [Fact]
    public async Task Import_stores_events_and_the_overlap_query_returns_only_overlapping_rows()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");

        var imported = await ImportEventsAsync(facility.Id, EventLog());

        Assert.Equal(HttpStatusCode.Created, imported.StatusCode);
        var summary = await imported.Content.ReadFromJsonAsync<RoomEventImportSummary>(Json);
        Assert.NotNull(summary);
        Assert.Equal(5, summary.ImportedCount);

        var listed = await Client.GetAsync($"/api/facilities/{facility.Id}/events?from={From}&to={To}");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var events = await listed.Content.ReadFromJsonAsync<List<RoomEventResponse>>(Json);
        Assert.NotNull(events);
        Assert.Equal([RoomEventType.Begin, RoomEventType.DoorOpened, RoomEventType.ProductsIn], events.Select(item => item.Type));
        Assert.Equal(events[0].From, events[0].To);
        Assert.All(events, item => Assert.Equal(DateTimeKind.Unspecified, item.From.Kind));

        await using var db = CreateDbContext();
        Assert.Equal(5, await db.RoomEvents.CountAsync());
        Assert.Equal(0, await db.Measurements.CountAsync());
    }

    [Fact]
    public async Task Malformed_date_and_unknown_action_are_rejected()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");

        var badDate = await ImportEventsAsync(
            facility.Id,
            "Date;Action;Start Time;End Time\n01.06.2024;Door opened;10:00:00;10:00:01");
        var unknown = await ImportEventsAsync(
            facility.Id,
            "Date;Action;Start Time;End Time\n2024-06-01;Window opened;10:00:00;10:00:01");

        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
        Assert.Contains("A date or time is invalid.", await badDate.ReadErrorAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("The action was not recognized.", await unknown.ReadErrorAsync(), StringComparison.Ordinal);
        await using var db = CreateDbContext();
        Assert.Equal(0, await db.RoomEvents.CountAsync());
    }

    [Fact]
    public async Task Blind_investigation_excludes_events_and_context_includes_only_overlapping_events()
    {
        var facility = await SeedFacilityAsync();
        var blind = new RecordingInvestigator();
        var aware = new RecordingInvestigator();

        var blindResponse = await PostInvestigationAsync(blind, facility, includeContext: false);
        var awareResponse = await PostInvestigationAsync(aware, facility, includeContext: true);

        Assert.Equal(HttpStatusCode.OK, blindResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, awareResponse.StatusCode);
        Assert.Null(blind.ContextEvents);
        Assert.Equal(InvestigatorInstructions.Text, blind.Instructions);
        Assert.DoesNotContain(blind.Catalog!.Items, item => item.Value is not null && item.Value.Contains("Door", StringComparison.Ordinal));
        Assert.Equal(InvestigatorInstructions.ContextText, aware.Instructions);
        Assert.Equal(
            [RoomEventType.Begin, RoomEventType.DoorOpened, RoomEventType.ProductsIn],
            aware.ContextEvents!.Select(item => item.Type));
        Assert.Equal(blind.Catalog.Items.Select(item => item.Id), aware.Catalog!.Items.Select(item => item.Id));
        var contextEvents = aware.ContextEvents;
        Assert.NotNull(contextEvents);
        Assert.DoesNotContain(aware.Catalog.Items, item => contextEvents.Any(roomEvent => item.Id.Contains(roomEvent.Type.ToString(), StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Verifier_receives_context_only_when_enabled()
    {
        var facility = await SeedFacilityAsync();
        var disabled = new RecordingVerifier();
        var enabled = new RecordingVerifier();
        var investigation = InvestigationJson(facility);

        var without = await PostVerificationAsync(disabled, facility, investigation, includeContext: false);
        var with = await PostVerificationAsync(enabled, facility, investigation, includeContext: true);

        Assert.Equal(HttpStatusCode.OK, without.StatusCode);
        Assert.Equal(HttpStatusCode.OK, with.StatusCode);
        Assert.Null(disabled.ContextEvents);
        Assert.Equal(VerifierInstructions.Text, disabled.Instructions);
        Assert.Equal(VerifierInstructions.ContextText, enabled.Instructions);
        Assert.Equal(
            [RoomEventType.Begin, RoomEventType.DoorOpened, RoomEventType.ProductsIn],
            enabled.ContextEvents!.Select(item => item.Type));
        Assert.Equal(disabled.Catalog!.Items.Select(item => item.Id), enabled.Catalog!.Items.Select(item => item.Id));
    }

    private async Task<Guid> SeedFacilityAsync()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");
        var equipment = await Client.PostEquipmentAsync(facility.Id, equipmentType.Id, "SENSOR01", "Near entrance");
        Assert.Equal(HttpStatusCode.Created, equipment.StatusCode);
        var created = await equipment.ReadRequiredAsync<ColdRoomAssessment.Application.EquipmentResponse>();
        var measurements = await Client.PostAsync(
            $"/api/equipment/{created.Id}/imports",
            File("Date;Time;Temperature;Humidity\n01.06.2024;10:00:00;3,0;40\n01.06.2024;10:00:05;3,5;41", "sensor.csv"));
        Assert.Equal(HttpStatusCode.Created, measurements.StatusCode);
        var events = await ImportEventsAsync(facility.Id, EventLog());
        Assert.Equal(HttpStatusCode.Created, events.StatusCode);
        return facility.Id;
    }

    private Task<HttpResponseMessage> ImportEventsAsync(Guid facilityId, string csv) =>
        Client.PostAsync($"/api/facilities/{facilityId}/events/import", File(csv, "events.csv"));

    private async Task<HttpResponseMessage> PostInvestigationAsync(
        RecordingInvestigator investigator,
        Guid facilityId,
        bool includeContext)
    {
        await using var factory = Factory(services =>
        {
            services.RemoveAll<IInvestigatorClient>();
            services.AddSingleton<IInvestigatorClient>(investigator);
        });
        using var client = factory.CreateClient();
        var context = includeContext ? "&includeContext=true" : string.Empty;
        return await client.PostAsync($"/api/facilities/{facilityId}/investigations?from={From}&to={To}{context}", content: null);
    }

    private async Task<HttpResponseMessage> PostVerificationAsync(
        RecordingVerifier verifier,
        Guid facilityId,
        string investigation,
        bool includeContext)
    {
        await using var factory = Factory(services =>
        {
            services.RemoveAll<IVerifierClient>();
            services.AddSingleton<IVerifierClient>(verifier);
        });
        using var client = factory.CreateClient();
        var context = includeContext ? "&includeContext=true" : string.Empty;
        using var content = new StringContent(investigation, Encoding.UTF8, "application/json");
        return await client.PostAsync($"/api/facilities/{facilityId}/verifications?from={From}&to={To}{context}", content);
    }

    private WebApplicationFactory<Program> Factory(Action<IServiceCollection> configure) =>
        _postgres.ApiFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(configure));

    private ColdRoomDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ColdRoomDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;
        return new ColdRoomDbContext(options);
    }

    private static string EventLog() =>
        """
        Date;Action;Start Time;End Time
        2024-06-01;Door opened;10:00:01;10:00:02
        2024-06-01;Products in;10:00:03;10:00:04
        2024-06-01;Products out;11:00:00;11:00:01
        2024-06-01;Door closed;10:00:15;10:00:15
        2024-06-01;Begin;10:00:00;10:00:00
        """;

    private static string InvestigationJson(Guid facilityId) =>
        $$"""
        {
          "facilityId": "{{facilityId:D}}",
          "from": "{{From}}",
          "to": "{{To}}",
          "overallSummary": "No claim.",
          "observations": [],
          "hypotheses": [],
          "limitations": []
        }
        """;

    private static MultipartFormDataContent File(string csv, string fileName)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);
        return content;
    }

    private static JsonSerializerOptions CreateJson()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        json.Converters.Add(new JsonStringEnumConverter());
        return json;
    }

    private sealed class RecordingInvestigator : IInvestigatorClient
    {
        public EvidenceCatalog? Catalog { get; private set; }

        public string? Instructions { get; private set; }

        public IReadOnlyList<ContextEvent>? ContextEvents { get; private set; }

        public Task<string> InvestigateAsync(
            EvidenceCatalog catalog,
            string instructions,
            CancellationToken cancellationToken,
            IReadOnlyList<ContextEvent>? contextEvents = null)
        {
            Catalog = catalog;
            Instructions = instructions;
            ContextEvents = contextEvents;
            return Task.FromResult(
                """
                {"overallSummary":"No claim.","observations":[],"hypotheses":[],"limitations":[]}
                """);
        }
    }

    private sealed class RecordingVerifier : IVerifierClient
    {
        public EvidenceCatalog? Catalog { get; private set; }

        public string? Instructions { get; private set; }

        public IReadOnlyList<ContextEvent>? ContextEvents { get; private set; }

        public Task<string> VerifyAsync(
            EvidenceCatalog catalog,
            FacilityInvestigation investigation,
            string instructions,
            CancellationToken cancellationToken,
            IReadOnlyList<ContextEvent>? contextEvents = null)
        {
            Catalog = catalog;
            Instructions = instructions;
            ContextEvents = contextEvents;
            return Task.FromResult(
                $$"""
                {
                  "facilityId": "{{investigation.FacilityId:D}}",
                  "from": "{{From}}",
                  "to": "{{To}}",
                  "overallVerdict": "InsufficientEvidence",
                  "observationVerifications": [],
                  "hypothesisVerifications": [],
                  "limitations": []
                }
                """);
        }
    }
}
