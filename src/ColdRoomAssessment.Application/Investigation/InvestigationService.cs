using ColdRoomAssessment.Application.Analysis;

namespace ColdRoomAssessment.Application.Investigation;

public sealed class InvestigationService(AnalysisService analysis, IInvestigatorClient client)
{
    public async Task<OperationResult<FacilityInvestigation>> InvestigateAsync(
        Guid facilityId,
        string? fromText,
        string? toText,
        CancellationToken cancellationToken)
    {
        var aligned = await analysis.GetAsync(facilityId, fromText, toText, cancellationToken);
        if (aligned.Status != OperationStatus.Success)
        {
            return new OperationResult<FacilityInvestigation>(aligned.Status, null, aligned.Error);
        }

        var facilityAnalysis = aligned.Value!;
        var catalog = EvidenceCatalog.FromAnalysis(facilityAnalysis);
        var text = await client.InvestigateAsync(catalog, InvestigatorInstructions.Text, cancellationToken);
        if (!InvestigationResultValidator.TryCreate(text, facilityAnalysis, catalog, out var investigation))
        {
            throw new InvestigatorInvalidResultException();
        }

        return OperationResult<FacilityInvestigation>.Success(investigation!);
    }
}
