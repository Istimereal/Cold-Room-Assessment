using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Investigation;

namespace ColdRoomAssessment.Application.Verification;

public sealed class VerifierService(AnalysisService analysis, IVerifierClient client)
{
    public async Task<OperationResult<FacilityVerification>> VerifyAsync(
        Guid facilityId,
        string? fromText,
        string? toText,
        string? investigationJson,
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
        var text = await client.VerifyAsync(catalog, accepted, VerifierInstructions.Text, cancellationToken);
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
