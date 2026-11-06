namespace ColdRoomAssessment.Domain;

public sealed class RoomEvent
{
    public Guid Id { get; set; }

    public Guid FacilityId { get; set; }

    public RoomEventType Type { get; set; }

    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public Facility? Facility { get; set; }
}
