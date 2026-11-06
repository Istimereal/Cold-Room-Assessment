using ColdRoomAssessment.Application.Events;
using ColdRoomAssessment.Domain;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.Infrastructure;

public sealed class EfRoomEventStore(ColdRoomDbContext db) : IRoomEventStore
{
    public Task<bool> FacilityExistsAsync(Guid facilityId, CancellationToken cancellationToken) =>
        db.Facilities.AsNoTracking().AnyAsync(facility => facility.Id == facilityId, cancellationToken);

    public async Task AddAsync(IReadOnlyList<RoomEvent> events, CancellationToken cancellationToken)
    {
        db.RoomEvents.AddRange(events);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoomEvent>> ListOverlappingAsync(
        Guid facilityId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var events = await db.RoomEvents
            .AsNoTracking()
            .Where(roomEvent => roomEvent.FacilityId == facilityId)
            .ToListAsync(cancellationToken);
        return events
            .Where(roomEvent => RoomEventOverlap.Overlaps(roomEvent.From, roomEvent.To, from, to))
            .OrderBy(roomEvent => roomEvent.From)
            .ThenBy(roomEvent => roomEvent.To)
            .ThenBy(roomEvent => roomEvent.Id)
            .ToList();
    }
}
