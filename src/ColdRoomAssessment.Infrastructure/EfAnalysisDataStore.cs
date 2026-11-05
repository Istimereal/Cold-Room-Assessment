using ColdRoomAssessment.Application.Coverage;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.Infrastructure;

public sealed class EfAnalysisDataStore(ColdRoomDbContext db) : IAnalysisDataStore
{
    public Task<CoverageFacility?> FindFacilityAsync(Guid facilityId, CancellationToken cancellationToken) =>
        db.Facilities
            .AsNoTracking()
            .Where(facility => facility.Id == facilityId)
            .Select(facility => new CoverageFacility(facility.Id, facility.Name))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CoverageEquipmentRecord>> ListEquipmentAsync(
        Guid facilityId,
        CancellationToken cancellationToken)
    {
        return await db.Equipment
            .AsNoTracking()
            .Where(equipment => equipment.FacilityId == facilityId)
            .OrderBy(equipment => equipment.Name)
            .ThenBy(equipment => equipment.Id)
            .Select(equipment => new CoverageEquipmentRecord(
                equipment.Id,
                equipment.Name,
                equipment.Placement,
                equipment.EquipmentTypeId,
                equipment.EquipmentType!.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnalysisMeasurementRow>> ListMeasurementsAsync(
        Guid facilityId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var intervalFrom = from;
        var intervalTo = to;
        return await (
            from measurement in db.Measurements.AsNoTracking()
            join dataImport in db.DataImports.AsNoTracking()
                on measurement.DataImportId equals dataImport.Id
            join equipment in db.Equipment.AsNoTracking()
                on dataImport.EquipmentId equals equipment.Id
            where equipment.FacilityId == facilityId &&
                measurement.MeasuredAt >= intervalFrom &&
                measurement.MeasuredAt < intervalTo
            select new AnalysisMeasurementRow(
                equipment.Id,
                measurement.MeasuredAt,
                measurement.Kind,
                measurement.Value,
                dataImport.Id)).ToListAsync(cancellationToken);
    }
}
