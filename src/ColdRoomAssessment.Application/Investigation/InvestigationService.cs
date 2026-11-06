using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Events;

namespace ColdRoomAssessment.Application.Investigation;

public sealed class InvestigationService(AnalysisService analysis, IRoomEventStore roomEvents, IInvestigatorClient client)
{
    public async Task<OperationResult<FacilityInvestigation>> InvestigateAsync(
        Guid facilityId,
        string? fromText,
        string? toText,
        bool includeContext,
        CancellationToken cancellationToken)
    {
        var aligned = await analysis.GetAsync(facilityId, fromText, toText, cancellationToken);
        if (aligned.Status != OperationStatus.Success)
        {
            return new OperationResult<FacilityInvestigation>(aligned.Status, null, aligned.Error);
        }

        var facilityAnalysis = aligned.Value!;
        var catalog = EvidenceCatalog.FromAnalysis(facilityAnalysis);
        IReadOnlyList<ContextEvent>? context = null;
        var instructions = InvestigatorInstructions.Text;
        if (includeContext)
        {
            context = await ContextForAsync(facilityId, facilityAnalysis, cancellationToken);
            instructions = InvestigatorInstructions.ContextText;
        }

        var text = await client.InvestigateAsync(catalog, instructions, cancellationToken, context);
        if (!InvestigationResultValidator.TryCreate(text, facilityAnalysis, catalog, out var investigation))
        {
            throw new InvestigatorInvalidResultException();
        }

        return OperationResult<FacilityInvestigation>.Success(investigation!);
    }

    private async Task<IReadOnlyList<ContextEvent>> ContextForAsync(
        Guid facilityId,
        FacilityAnalysis facilityAnalysis,
        CancellationToken cancellationToken)
    {
        var events = await roomEvents.ListOverlappingAsync(
            facilityId,
            facilityAnalysis.From,
            facilityAnalysis.To,
            cancellationToken);
        return events.Select(item => new ContextEvent(item.Type, item.From, item.To)).ToList();
    }
}
