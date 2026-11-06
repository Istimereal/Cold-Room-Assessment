namespace ColdRoomAssessment.Application.Events;

public static class RoomEventOverlap
{
    // A zero-duration event overlaps when its timestamp is in [from, to).
    public static bool Overlaps(DateTime eventFrom, DateTime eventTo, DateTime from, DateTime to) =>
        eventFrom < to && eventTo >= from;
}
