using ColdRoomAssessment.Application.Coverage;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.Infrastructure;

public sealed class EfAnalysisPeriodStore(ColdRoomDbContext db) : IAnalysisPeriodStore
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

    public async Task<IReadOnlyList<EquipmentMeasurementFacts>> ListMeasurementFactsAsync(
        Guid facilityId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var intervalFrom = from;
        var intervalTo = to;
        var rows = await (
            from measurement in db.Measurements.AsNoTracking()
            join dataImport in db.DataImports.AsNoTracking()
                on measurement.DataImportId equals dataImport.Id
            join equipment in db.Equipment.AsNoTracking()
                on dataImport.EquipmentId equals equipment.Id
            where equipment.FacilityId == facilityId &&
                measurement.MeasuredAt >= intervalFrom &&
                measurement.MeasuredAt < intervalTo
            select new
            {
                EquipmentId = equipment.Id,
                measurement.MeasuredAt,
                ImportId = dataImport.Id
            }).ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.EquipmentId)
            .Select(group => new EquipmentMeasurementFacts(
                group.Key,
                group.Select(row => row.MeasuredAt).Distinct().OrderBy(time => time).ToArray(),
                group.Count(),
                group.Select(row => row.ImportId).Distinct().Count()))
            .ToArray();
    }
}
