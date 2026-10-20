using ColdRoomAssessment.Application;
using ColdRoomAssessment.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ColdRoomAssessment.Infrastructure;

public sealed class EfColdRoomStore(ColdRoomDbContext db) : IColdRoomStore
{
    public async Task AddFacilityAsync(Facility facility, CancellationToken cancellationToken)
    {
        db.Facilities.Add(facility);
        await SaveAsync(cancellationToken);
    }

    public Task<Facility?> GetFacilityAsync(Guid id, CancellationToken cancellationToken) =>
        db.Facilities
            .Include(facility => facility.Equipment)
            .FirstOrDefaultAsync(facility => facility.Id == id, cancellationToken);

    public async Task AddEquipmentTypeAsync(EquipmentType equipmentType, CancellationToken cancellationToken)
    {
        db.EquipmentTypes.Add(equipmentType);
        await SaveAsync(cancellationToken);
    }

    public Task<EquipmentType?> GetEquipmentTypeAsync(Guid id, CancellationToken cancellationToken) =>
        db.EquipmentTypes.FirstOrDefaultAsync(equipmentType => equipmentType.Id == id, cancellationToken);

    public async Task AddEquipmentAsync(Equipment equipment, CancellationToken cancellationToken)
    {
        db.Equipment.Add(equipment);
        await SaveAsync(cancellationToken);
    }

    public Task<Equipment?> GetEquipmentAsync(Guid id, CancellationToken cancellationToken) =>
        db.Equipment
            .Include(equipment => equipment.Facility)
            .Include(equipment => equipment.EquipmentType)
            .FirstOrDefaultAsync(equipment => equipment.Id == id, cancellationToken);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new UniqueConstraintViolationException(exception);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }
        }

        return false;
    }
}
