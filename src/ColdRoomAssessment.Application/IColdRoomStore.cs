using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application;

public interface IColdRoomStore
{
    Task AddFacilityAsync(Facility facility, CancellationToken cancellationToken);

    Task<Facility?> GetFacilityAsync(Guid id, CancellationToken cancellationToken);

    Task AddEquipmentTypeAsync(EquipmentType equipmentType, CancellationToken cancellationToken);

    Task<EquipmentType?> GetEquipmentTypeAsync(Guid id, CancellationToken cancellationToken);

    Task AddEquipmentAsync(Equipment equipment, CancellationToken cancellationToken);

    Task<Equipment?> GetEquipmentAsync(Guid id, CancellationToken cancellationToken);
}
