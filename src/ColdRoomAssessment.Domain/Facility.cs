namespace ColdRoomAssessment.Domain;

public sealed class Facility
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}
