using System.Globalization;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application.Investigation;

public sealed record ContextEvent(RoomEventType Type, DateTime From, DateTime To);

public sealed record ContextEventDocument(string Type, string From, string To)
{
    public static IReadOnlyList<ContextEventDocument> FromEvents(IReadOnlyList<ContextEvent> events) =>
        events
            .Select(item => new ContextEventDocument(
                item.Type.ToString(),
                Timestamp(item.From),
                Timestamp(item.To)))
            .ToList();

    private static string Timestamp(DateTime value) =>
        value.ToString(AnalysisPeriodService.TimestampFormat, CultureInfo.InvariantCulture);
}
