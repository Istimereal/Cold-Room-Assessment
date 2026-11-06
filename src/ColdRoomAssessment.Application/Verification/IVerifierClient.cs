using ColdRoomAssessment.Application.Investigation;

namespace ColdRoomAssessment.Application.Verification;

public interface IVerifierClient
{
    Task<string> VerifyAsync(
        EvidenceCatalog catalog,
        FacilityInvestigation investigation,
        string instructions,
        CancellationToken cancellationToken,
        IReadOnlyList<ContextEvent>? contextEvents = null);
}
