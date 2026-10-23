using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application.Imports;

public interface IMeasurementImportStore
{
    Task<Equipment?> FindEquipmentAsync(Guid equipmentId, CancellationToken cancellationToken);

    Task AddImportAsync(
        DataImport import,
        IReadOnlyList<Measurement> measurements,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ImportSummary>> ListImportsAsync(Guid equipmentId, CancellationToken cancellationToken);
}
