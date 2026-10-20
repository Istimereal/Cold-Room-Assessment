namespace ColdRoomAssessment.Domain;

public sealed class Equipment
{
    public Guid Id { get; set; }

    public Guid FacilityId { get; set; }

    public Guid EquipmentTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Placement { get; set; } = string.Empty;

    public Facility? Facility { get; set; }

    public EquipmentType? EquipmentType { get; set; }

    public ICollection<DataImport> DataImports { get; set; } = new List<DataImport>();
}
