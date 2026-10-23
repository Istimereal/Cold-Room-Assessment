using ColdRoomAssessment.Application.Imports;
using ColdRoomAssessment.Domain;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.Infrastructure;

public sealed class EfMeasurementImportStore(ColdRoomDbContext db) : IMeasurementImportStore
{
    public Task<Equipment?> FindEquipmentAsync(Guid equipmentId, CancellationToken cancellationToken) =>
        db.Equipment.AsNoTracking().FirstOrDefaultAsync(equipment => equipment.Id == equipmentId, cancellationToken);

    public async Task AddImportAsync(
        DataImport import,
        IReadOnlyList<Measurement> measurements,
        CancellationToken cancellationToken)
    {
        db.DataImports.Add(import);
        db.Measurements.AddRange(measurements);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImportSummary>> ListImportsAsync(
        Guid equipmentId,
        CancellationToken cancellationToken)
    {
        return await db.DataImports
            .AsNoTracking()
            .Where(import => import.EquipmentId == equipmentId)
            .OrderBy(import => import.FirstMeasurementAt)
            .ThenBy(import => import.Id)
            .Select(import => new ImportSummary(
                import.Id,
                import.EquipmentId,
                import.Equipment!.Name,
                import.OriginalFileName,
                import.ImportedAt,
                import.FirstMeasurementAt,
                import.LastMeasurementAt,
                import.ImportedRowCount,
                import.InvalidRowCount,
                import.Measurements.Count(),
                import.Status))
            .ToListAsync(cancellationToken);
    }
}
