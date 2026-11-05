namespace ColdRoomAssessment.Application.Verification;

public sealed record FacilityVerification(
    Guid FacilityId,
    DateTime From,
    DateTime To,
    VerificationVerdict OverallVerdict,
    IReadOnlyList<ObservationVerification> ObservationVerifications,
    IReadOnlyList<HypothesisVerification> HypothesisVerifications,
    IReadOnlyList<string> Limitations);

public sealed record ObservationVerification(
    string ObservationId,
    VerificationVerdict Verdict,
    string Explanation,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> MissingEvidenceIds,
    IReadOnlyList<string> ConflictingEvidenceIds);

public sealed record HypothesisVerification(
    string HypothesisId,
    VerificationVerdict Verdict,
    string Explanation,
    IReadOnlyList<string> SupportingEvidenceIds,
    IReadOnlyList<string> ContradictingEvidenceIds,
    string Uncertainty);

public enum VerificationVerdict
{
    Supported,
    PartiallySupported,
    NotSupported,
    InsufficientEvidence
}
