namespace ColdRoomAssessment.Application.Verification;

public static class VerifierInstructions
{
    public const string Text =
        """
        Verify the supplied investigation against the supplied deterministic evidence. Verify rather than extend it.
        Do not add observations, hypotheses, events, or causes.
        Use only the supplied deterministic evidence.
        Do not invent events or causes.
        Distinguish a lack of evidence from contradiction.
        Use InsufficientEvidence when the evidence neither supports nor contradicts the claim.
        Reject certainty that the supplied evidence does not support.
        Report conflicting evidence when the catalog conflicts with a claim.
        Cite only evidence ids present in the supplied catalog. Do not construct new evidence ids.
        No operational recommendations.
        You have no knowledge of experiment actions, research conclusions, gold findings, or hidden labels.
        """;

    public const string ContextText =
        """
        Verify the supplied investigation against the supplied deterministic evidence and the supplied room events. Verify rather than extend it.
        Do not add observations, hypotheses, events, or causes.
        Use only the supplied deterministic evidence and the supplied room events.
        Do not invent events or causes.
        Room events are not sensor evidence. Do not treat a room event as an evidence id.
        Check that a cited room event exists, and that its type and timestamps match the supplied room events.
        Check claimed temporal relationships: coincides with, follows, overlaps, and temporally associated with.
        Do not accept causation, including caused by or due to.
        Distinguish a lack of evidence from contradiction.
        Use InsufficientEvidence when the evidence neither supports nor contradicts the claim.
        Reject certainty that the supplied evidence does not support.
        Report conflicting evidence when the catalog conflicts with a claim.
        Cite only evidence ids present in the supplied catalog. Do not construct new evidence ids.
        No operational recommendations.
        You have no knowledge of research conclusions, gold findings, or hidden labels beyond the supplied room events.
        """;
}
