using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application.Events;

public interface IRoomEventStore
{
    Task<bool> FacilityExistsAsync(Guid facilityId, CancellationToken cancellationToken);

    Task AddAsync(IReadOnlyList<RoomEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoomEvent>> ListOverlappingAsync(
        Guid facilityId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken);
}
