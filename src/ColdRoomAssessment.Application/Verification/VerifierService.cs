using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Events;
using ColdRoomAssessment.Application.Investigation;

namespace ColdRoomAssessment.Application.Verification;

public sealed class VerifierService(AnalysisService analysis, IRoomEventStore roomEvents, IVerifierClient client)
{
    public async Task<OperationResult<FacilityVerification>> VerifyAsync(
        Guid facilityId,
        string? fromText,
        string? toText,
        string? investigationJson,
        bool includeContext,
        CancellationToken cancellationToken)
    {
        var aligned = await analysis.GetAsync(facilityId, fromText, toText, cancellationToken);
        if (aligned.Status != OperationStatus.Success)
        {
            return new OperationResult<FacilityVerification>(aligned.Status, null, aligned.Error);
        }

        var facilityAnalysis = aligned.Value!;
        // The posted investigation is the result already shown in the UI.
        // Evidence is rebuilt from stored measurements and replaces anything the client sends.
        var catalog = EvidenceCatalog.FromAnalysis(facilityAnalysis);
        if (!SubmittedInvestigation.TryAccept(investigationJson, facilityAnalysis, catalog, out var investigation))
        {
            return OperationResult<FacilityVerification>.Invalid("The investigation result is invalid.");
        }

        var accepted = investigation!;
        IReadOnlyList<ContextEvent>? context = null;
        var instructions = VerifierInstructions.Text;
        if (includeContext)
        {
            var events = await roomEvents.ListOverlappingAsync(
                facilityId,
                facilityAnalysis.From,
                facilityAnalysis.To,
                cancellationToken);
            context = events.Select(item => new ContextEvent(item.Type, item.From, item.To)).ToList();
            instructions = VerifierInstructions.ContextText;
        }

        var text = await client.VerifyAsync(catalog, accepted, instructions, cancellationToken, context);
        if (!VerifierResultValidator.TryCreate(
                text,
                facilityAnalysis,
                catalog,
                accepted,
                out var verification,
                out var reason))
        {
            throw new VerifierInvalidResultException(reason);
        }

        return OperationResult<FacilityVerification>.Success(verification!);
    }
}
