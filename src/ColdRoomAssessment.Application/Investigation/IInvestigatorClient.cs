namespace ColdRoomAssessment.Application.Investigation;

public interface IInvestigatorClient
{
    Task<string> InvestigateAsync(
        EvidenceCatalog catalog,
        string instructions,
        CancellationToken cancellationToken,
        IReadOnlyList<ContextEvent>? contextEvents = null);
}
