using System.Text.Json;
using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Investigation;
using ColdRoomAssessment.Application.Verification;
using ColdRoomAssessment.Domain;
using ColdRoomAssessment.Infrastructure;

namespace ColdRoomAssessment.IntegrationTests.Events;

public sealed class InvestigationContextTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Blind_request_is_the_sensor_catalog_and_context_stays_out_of_evidence_ids()
    {
        var catalog = Catalog();
        var blind = OpenAIInvestigatorClient.UserMessage(catalog, null);
        var context = new ContextEvent(
            RoomEventType.DoorOpened,
            new DateTime(2024, 6, 1, 10, 0, 1),
            new DateTime(2024, 6, 1, 10, 0, 2));
        var aware = OpenAIInvestigatorClient.UserMessage(catalog, [context]);

        Assert.Equal(JsonSerializer.Serialize(catalog, Json), blind);
        Assert.DoesNotContain("contextEvents", blind, StringComparison.Ordinal);
        Assert.DoesNotContain("sensorEvidence", blind, StringComparison.Ordinal);
        Assert.Contains("No experiment actions or room events.", InvestigatorInstructions.Text, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(aware);
        var sensor = document.RootElement.GetProperty("sensorEvidence");
        var items = sensor.GetProperty("items");
        Assert.Equal(catalog.Items.Count, items.GetArrayLength());
        Assert.Equal(catalog.Items[0].Id, items[0].GetProperty("id").GetString());
        var events = document.RootElement.GetProperty("contextEvents");
        var roomEvent = Assert.Single(events.EnumerateArray());
        Assert.Equal("DoorOpened", roomEvent.GetProperty("type").GetString());
        Assert.Equal("2024-06-01T10:00:01", roomEvent.GetProperty("from").GetString());
        Assert.Equal("2024-06-01T10:00:02", roomEvent.GetProperty("to").GetString());
        Assert.False(roomEvent.TryGetProperty("id", out _));
        Assert.DoesNotContain(catalog.Items, item => item.Id.Contains("DoorOpened", StringComparison.Ordinal));
        Assert.Contains("temporally associated with", InvestigatorInstructions.ContextText, StringComparison.Ordinal);
        Assert.Contains("coincides with", InvestigatorInstructions.ContextText, StringComparison.Ordinal);
        Assert.Contains("follows", InvestigatorInstructions.ContextText, StringComparison.Ordinal);
        Assert.Contains("overlaps", InvestigatorInstructions.ContextText, StringComparison.Ordinal);
        Assert.Contains("caused by", InvestigatorInstructions.ContextText, StringComparison.Ordinal);
        Assert.Contains("due to", InvestigatorInstructions.ContextText, StringComparison.Ordinal);
    }

    [Fact]
    public void Context_payload_includes_only_the_supplied_overlapping_events()
    {
        var windowFrom = new DateTime(2024, 6, 1, 10, 0, 0);
        var windowTo = new DateTime(2024, 6, 1, 10, 5, 0);
        var events = new[]
        {
            new ContextEvent(RoomEventType.DoorOpened, windowFrom.AddSeconds(1), windowFrom.AddSeconds(2)),
            new ContextEvent(RoomEventType.ProductsIn, windowFrom.AddHours(1), windowFrom.AddHours(1).AddMinutes(1)),
            new ContextEvent(RoomEventType.ProductsOut, windowFrom, windowFrom)
        };
        var overlapping = events
            .Where(item => ColdRoomAssessment.Application.Events.RoomEventOverlap.Overlaps(item.From, item.To, windowFrom, windowTo))
            .ToList();
        var message = OpenAIInvestigatorClient.UserMessage(Catalog(), overlapping);

        using var document = JsonDocument.Parse(message);
        var types = document.RootElement.GetProperty("contextEvents").EnumerateArray()
            .Select(item => item.GetProperty("type").GetString())
            .ToList();
        Assert.Equal(["DoorOpened", "ProductsOut"], types);
    }

    [Fact]
    public void Verifier_receives_context_only_when_it_is_supplied()
    {
        var catalog = Catalog();
        var investigation = new FacilityInvestigation(
            catalog.FacilityId,
            new DateTime(2024, 6, 1, 10, 0, 0),
            new DateTime(2024, 6, 1, 10, 5, 0),
            "No claim.",
            [],
            [],
            []);
        var context = new ContextEvent(
            RoomEventType.ProductsIn,
            new DateTime(2024, 6, 1, 10, 1, 0),
            new DateTime(2024, 6, 1, 10, 2, 0));

        var disabled = OpenAIVerifierClient.UserMessage(catalog, investigation, null);
        var enabled = OpenAIVerifierClient.UserMessage(catalog, investigation, [context]);

        Assert.DoesNotContain("contextEvents", disabled, StringComparison.Ordinal);
        Assert.Contains("deterministicEvidence", disabled, StringComparison.Ordinal);
        Assert.Contains("You have no knowledge of experiment actions", VerifierInstructions.Text, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(enabled);
        Assert.Equal(
            catalog.Items[0].Id,
            document.RootElement.GetProperty("deterministicEvidence").GetProperty("items")[0].GetProperty("id").GetString());
        var roomEvent = Assert.Single(document.RootElement.GetProperty("contextEvents").EnumerateArray());
        Assert.Equal("ProductsIn", roomEvent.GetProperty("type").GetString());
        Assert.Equal("2024-06-01T10:01:00", roomEvent.GetProperty("from").GetString());
        Assert.Equal("2024-06-01T10:02:00", roomEvent.GetProperty("to").GetString());
        Assert.Contains("temporally associated with", VerifierInstructions.ContextText, StringComparison.Ordinal);
        Assert.Contains("not sensor evidence", VerifierInstructions.ContextText, StringComparison.Ordinal);
        Assert.Contains("caused by", VerifierInstructions.ContextText, StringComparison.Ordinal);
    }

    private static EvidenceCatalog Catalog()
    {
        var equipmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var kind = new KindAnalysis(1, 3, null, 3, null, 3, 3, 0, 3, null, 3, null, 0, 0, []);
        var equipment = new EquipmentAnalysis(
            equipmentId,
            "SENSOR01",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Sensor",
            "Near entrance",
            1,
            1,
            100,
            true,
            0,
            0,
            kind,
            kind,
            []);
        var analysis = new FacilityAnalysis(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Cold Room A",
            new DateTime(2024, 6, 1, 10, 0, 0),
            new DateTime(2024, 6, 1, 10, 5, 0),
            1,
            1,
            1,
            1,
            1,
            [equipment]);
        return EvidenceCatalog.FromAnalysis(analysis);
    }
}
