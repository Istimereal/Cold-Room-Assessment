using System.Globalization;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application.Events;

public sealed class RoomEventService(IRoomEventStore store)
{
    public async Task<OperationResult<RoomEventImportSummary>> ImportAsync(
        Guid facilityId,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (!await store.FacilityExistsAsync(facilityId, cancellationToken))
        {
            return OperationResult<RoomEventImportSummary>.NotFound("Facility was not found.");
        }

        using var reader = new StreamReader(content, leaveOpen: true);
        var parsed = RoomEventLogParser.Parse(await reader.ReadToEndAsync(cancellationToken));
        if (!parsed.Succeeded)
        {
            return OperationResult<RoomEventImportSummary>.Invalid(parsed.Error!);
        }

        var events = parsed.Events
            .Select(item => new RoomEvent
            {
                Id = Guid.CreateVersion7(),
                FacilityId = facilityId,
                Type = item.Type,
                From = item.From,
                To = item.To
            })
            .ToList();
        await store.AddAsync(events, cancellationToken);
        return OperationResult<RoomEventImportSummary>.Success(new RoomEventImportSummary(events.Count));
    }

    public async Task<OperationResult<IReadOnlyList<RoomEventResponse>>> ListAsync(
        Guid facilityId,
        string? fromText,
        string? toText,
        CancellationToken cancellationToken)
    {
        if (!TryTimestamp(fromText, "From", out var from, out var fromError))
        {
            return OperationResult<IReadOnlyList<RoomEventResponse>>.Invalid(fromError!);
        }

        if (!TryTimestamp(toText, "To", out var to, out var toError))
        {
            return OperationResult<IReadOnlyList<RoomEventResponse>>.Invalid(toError!);
        }

        if (from >= to)
        {
            return OperationResult<IReadOnlyList<RoomEventResponse>>.Invalid("From must be before to.");
        }

        if (!await store.FacilityExistsAsync(facilityId, cancellationToken))
        {
            return OperationResult<IReadOnlyList<RoomEventResponse>>.NotFound("Facility was not found.");
        }

        var events = await store.ListOverlappingAsync(facilityId, from, to, cancellationToken);
        return OperationResult<IReadOnlyList<RoomEventResponse>>.Success(
            events.Select(item => new RoomEventResponse(item.Id, item.FacilityId, item.Type, item.From, item.To)).ToList());
    }

    private static bool TryTimestamp(string? text, string fieldName, out DateTime timestamp, out string? error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            timestamp = default;
            error = $"{fieldName} is required.";
            return false;
        }

        if (!DateTime.TryParseExact(
                text,
                AnalysisPeriodService.TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out timestamp))
        {
            error = $"{fieldName} must be yyyy-MM-ddTHH:mm:ss.";
            return false;
        }

        error = null;
        return true;
    }
}

public sealed record RoomEventImportSummary(int ImportedCount);

public sealed record RoomEventResponse(
    Guid Id,
    Guid FacilityId,
    RoomEventType Type,
    DateTime From,
    DateTime To);
