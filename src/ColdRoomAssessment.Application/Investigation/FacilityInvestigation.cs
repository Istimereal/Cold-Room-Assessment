using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application.Investigation;

public sealed record FacilityInvestigation(
    Guid FacilityId,
    DateTime From,
    DateTime To,
    string OverallSummary,
    IReadOnlyList<InvestigationObservation> Observations,
    IReadOnlyList<InvestigationHypothesis> Hypotheses,
    IReadOnlyList<InvestigationLimitation> Limitations);

public sealed record InvestigationObservation(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<Guid> EquipmentIds,
    MeasurementKind? MeasurementKind,
    DateTime From,
    DateTime To,
    IReadOnlyList<string> EvidenceReferenceIds,
    InvestigationConfidence Confidence);

public sealed record InvestigationHypothesis(
    string Id,
    string Statement,
    IReadOnlyList<string> SupportingObservationIds,
    IReadOnlyList<string> EvidenceFor,
    IReadOnlyList<string> Uncertainties,
    InvestigationConfidence Confidence);

public sealed record InvestigationLimitation(
    InvestigationLimitationCode Code,
    Guid? EquipmentId,
    string Description);

public enum InvestigationConfidence
{
    Low,
    Medium,
    High
}

public enum InvestigationLimitationCode
{
    IncompleteCoverage,
    AmbiguousData,
    MissingData,
    InsufficientEvidence
}
