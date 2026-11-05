using System.Text.Json;
using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Investigation;

namespace ColdRoomAssessment.Application.Verification;

public static class VerifierResultValidator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool TryCreate(
        string? json,
        FacilityAnalysis analysis,
        EvidenceCatalog catalog,
        FacilityInvestigation investigation,
        out FacilityVerification? verification,
        out VerifierInvalidReason reason)
    {
        verification = null;
        reason = VerifierInvalidReason.Output;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        ModelVerification? model;
        try
        {
            model = JsonSerializer.Deserialize<ModelVerification>(json, Json);
        }
        catch (JsonException)
        {
            return false;
        }

        if (model is null
            || !TryVerdict(model.OverallVerdict, out var overall)
            || model.ObservationVerifications is null
            || model.HypothesisVerifications is null
            || model.Limitations is null)
        {
            return false;
        }

        var observationIds = investigation.Observations
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        var hypothesisIds = investigation.Hypotheses
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        var seenObservations = new HashSet<string>(StringComparer.Ordinal);
        var observations = new List<ObservationVerification>(model.ObservationVerifications.Count);
        foreach (var item in model.ObservationVerifications)
        {
            if (!TryObservation(item, observationIds, seenObservations, catalog, out var observation, out reason))
            {
                return false;
            }

            observations.Add(observation!);
        }

        var seenHypotheses = new HashSet<string>(StringComparer.Ordinal);
        var hypotheses = new List<HypothesisVerification>(model.HypothesisVerifications.Count);
        foreach (var item in model.HypothesisVerifications)
        {
            if (!TryHypothesis(item, hypothesisIds, seenHypotheses, catalog, out var hypothesis, out reason))
            {
                return false;
            }

            hypotheses.Add(hypothesis!);
        }

        var limitations = new List<string>(model.Limitations.Count);
        foreach (var item in model.Limitations)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                reason = VerifierInvalidReason.Output;
                return false;
            }

            limitations.Add(item);
        }

        verification = new FacilityVerification(
            analysis.FacilityId,
            analysis.From,
            analysis.To,
            overall,
            observations,
            hypotheses,
            limitations);
        return true;
    }

    private static bool TryObservation(
        ModelObservationVerification? item,
        HashSet<string> observationIds,
        HashSet<string> seen,
        EvidenceCatalog catalog,
        out ObservationVerification? observation,
        out VerifierInvalidReason reason)
    {
        observation = null;
        reason = VerifierInvalidReason.Output;
        if (item is null || string.IsNullOrWhiteSpace(item.Explanation) || !TryVerdict(item.Verdict, out var verdict))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.ObservationId))
        {
            return false;
        }

        if (!observationIds.Contains(item.ObservationId))
        {
            reason = VerifierInvalidReason.Reference;
            return false;
        }

        if (!seen.Add(item.ObservationId))
        {
            return false;
        }

        if (!TryEvidenceIds(item.EvidenceIds, catalog, out var evidenceIds, out reason)
            || !TryEvidenceIds(item.MissingEvidenceIds, catalog, out var missing, out reason)
            || !TryEvidenceIds(item.ConflictingEvidenceIds, catalog, out var conflicting, out reason))
        {
            return false;
        }

        observation = new ObservationVerification(
            item.ObservationId,
            verdict,
            item.Explanation,
            evidenceIds,
            missing,
            conflicting);
        return true;
    }

    private static bool TryHypothesis(
        ModelHypothesisVerification? item,
        HashSet<string> hypothesisIds,
        HashSet<string> seen,
        EvidenceCatalog catalog,
        out HypothesisVerification? hypothesis,
        out VerifierInvalidReason reason)
    {
        hypothesis = null;
        reason = VerifierInvalidReason.Output;
        if (item is null
            || string.IsNullOrWhiteSpace(item.Explanation)
            || string.IsNullOrWhiteSpace(item.Uncertainty)
            || !TryVerdict(item.Verdict, out var verdict))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.HypothesisId))
        {
            return false;
        }

        if (!hypothesisIds.Contains(item.HypothesisId))
        {
            reason = VerifierInvalidReason.Reference;
            return false;
        }

        if (!seen.Add(item.HypothesisId))
        {
            return false;
        }

        if (!TryEvidenceIds(item.SupportingEvidenceIds, catalog, out var supporting, out reason)
            || !TryEvidenceIds(item.ContradictingEvidenceIds, catalog, out var contradicting, out reason))
        {
            return false;
        }

        hypothesis = new HypothesisVerification(
            item.HypothesisId,
            verdict,
            item.Explanation,
            supporting,
            contradicting,
            item.Uncertainty);
        return true;
    }

    private static bool TryEvidenceIds(
        List<string?>? ids,
        EvidenceCatalog catalog,
        out IReadOnlyList<string> parsed,
        out VerifierInvalidReason reason)
    {
        parsed = [];
        reason = VerifierInvalidReason.Output;
        if (ids is null)
        {
            return false;
        }

        var result = new List<string>(ids.Count);
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (!catalog.Contains(id))
            {
                reason = VerifierInvalidReason.Evidence;
                return false;
            }

            result.Add(id);
        }

        parsed = result;
        return true;
    }

    private static bool TryVerdict(string? text, out VerificationVerdict verdict)
    {
        if (text == nameof(VerificationVerdict.Supported))
        {
            verdict = VerificationVerdict.Supported;
            return true;
        }

        if (text == nameof(VerificationVerdict.PartiallySupported))
        {
            verdict = VerificationVerdict.PartiallySupported;
            return true;
        }

        if (text == nameof(VerificationVerdict.NotSupported))
        {
            verdict = VerificationVerdict.NotSupported;
            return true;
        }

        if (text == nameof(VerificationVerdict.InsufficientEvidence))
        {
            verdict = VerificationVerdict.InsufficientEvidence;
            return true;
        }

        verdict = default;
        return false;
    }

    private sealed class ModelVerification
    {
        public string? OverallVerdict { get; set; }

        public List<ModelObservationVerification>? ObservationVerifications { get; set; }

        public List<ModelHypothesisVerification>? HypothesisVerifications { get; set; }

        public List<string?>? Limitations { get; set; }
    }

    private sealed class ModelObservationVerification
    {
        public string? ObservationId { get; set; }

        public string? Verdict { get; set; }

        public string? Explanation { get; set; }

        public List<string?>? EvidenceIds { get; set; }

        public List<string?>? MissingEvidenceIds { get; set; }

        public List<string?>? ConflictingEvidenceIds { get; set; }
    }

    private sealed class ModelHypothesisVerification
    {
        public string? HypothesisId { get; set; }

        public string? Verdict { get; set; }

        public string? Explanation { get; set; }

        public List<string?>? SupportingEvidenceIds { get; set; }

        public List<string?>? ContradictingEvidenceIds { get; set; }

        public string? Uncertainty { get; set; }
    }
}
