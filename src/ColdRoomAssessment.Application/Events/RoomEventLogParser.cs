using System.Globalization;
using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application.Events;

public static class RoomEventLogParser
{
    private static readonly string[] Header = ["Date", "Action", "Start Time", "End Time"];

    private static readonly Dictionary<string, RoomEventType> Actions = new(StringComparer.Ordinal)
    {
        ["Begin"] = RoomEventType.Begin,
        ["End"] = RoomEventType.End,
        ["Door opened"] = RoomEventType.DoorOpened,
        ["Door closed"] = RoomEventType.DoorClosed,
        ["Products in"] = RoomEventType.ProductsIn,
        ["Products out"] = RoomEventType.ProductsOut
    };

    public static RoomEventLogParseResult Parse(string text)
    {
        var normalized = text.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var events = new List<RoomEventDraft>();
        var headerSeen = false;
        var lineNumber = 0;
        foreach (var rawLine in normalized.Split('\n'))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var fields = rawLine.Split(';');
            if (!headerSeen)
            {
                if (!IsHeader(fields))
                {
                    return RoomEventLogParseResult.Failed("The file format was not recognized.");
                }

                headerSeen = true;
                continue;
            }

            if (!TryParseRow(fields, out var draft, out var error))
            {
                return RoomEventLogParseResult.Failed($"Row {lineNumber}: {error}");
            }

            events.Add(draft);
        }

        if (!headerSeen)
        {
            return RoomEventLogParseResult.Failed("The file format was not recognized.");
        }

        if (events.Count == 0)
        {
            return RoomEventLogParseResult.Failed("The file does not contain an event.");
        }

        return RoomEventLogParseResult.Success(events);
    }

    private static bool IsHeader(string[] fields)
    {
        if (fields.Length != Header.Length)
        {
            return false;
        }

        for (var index = 0; index < Header.Length; index++)
        {
            if (!string.Equals(fields[index].Trim(), Header[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseRow(string[] fields, out RoomEventDraft draft, out string error)
    {
        draft = null!;
        if (fields.Length != Header.Length)
        {
            error = "A row must be Date;Action;Start Time;End Time.";
            return false;
        }

        if (!DateTime.TryParseExact(
                fields[0].Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            error = "A date or time is invalid.";
            return false;
        }

        if (!TryTime(fields[2], out var start) || !TryTime(fields[3], out var end))
        {
            error = "A date or time is invalid.";
            return false;
        }

        if (!Actions.TryGetValue(fields[1].Trim(), out var type))
        {
            error = "The action was not recognized.";
            return false;
        }

        var from = Combine(date, start);
        var to = Combine(date, end);
        if (from > to)
        {
            error = "From is after to.";
            return false;
        }

        draft = new RoomEventDraft(type, from, to);
        error = string.Empty;
        return true;
    }

    private static bool TryTime(string field, out DateTime time) =>
        DateTime.TryParseExact(
            field.Trim(),
            "HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);

    private static DateTime Combine(DateTime date, DateTime time) =>
        new(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, DateTimeKind.Unspecified);
}

public sealed record RoomEventDraft(RoomEventType Type, DateTime From, DateTime To);

public sealed record RoomEventLogParseResult(IReadOnlyList<RoomEventDraft> Events, string? Error)
{
    public bool Succeeded => Error is null;

    public static RoomEventLogParseResult Success(IReadOnlyList<RoomEventDraft> events) => new(events, null);

    public static RoomEventLogParseResult Failed(string error) => new([], error);
}
