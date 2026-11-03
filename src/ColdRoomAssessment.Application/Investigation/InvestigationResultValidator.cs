using System.Globalization;
using System.Text.Json;
using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Domain;

namespace ColdRoomAssessment.Application.Investigation;

public static class InvestigationResultValidator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool TryCreate(
        string? json,
        FacilityAnalysis analysis,
        EvidenceCatalog catalog,
        out FacilityInvestigation? investigation)
    {
        investigation = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        ModelInvestigation? model;
        try
        {
            model = JsonSerializer.Deserialize<ModelInvestigation>(json, Json);
        }
        catch (JsonException)
        {
            return false;
        }

        if (model is null
            || string.IsNullOrWhiteSpace(model.OverallSummary)
            || model.Observations is null
            || model.Hypotheses is null
            || model.Limitations is null)
        {
            return false;
        }

        var equipmentIds = analysis.Equipment.Select(item => item.EquipmentId).ToHashSet();
        var observations = new List<InvestigationObservation>(model.Observations.Count);
        var observationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in model.Observations)
        {
            if (!TryObservation(item, equipmentIds, catalog, out var observation))
            {
                return false;
            }

            if (!observationIds.Add(observation!.Id))
            {
                return false;
            }

            observations.Add(observation);
        }

        var hypotheses = new List<InvestigationHypothesis>(model.Hypotheses.Count);
        foreach (var item in model.Hypotheses)
        {
            if (!TryHypothesis(item, observationIds, out var hypothesis))
            {
                return false;
            }

            hypotheses.Add(hypothesis!);
        }

        var limitations = new List<InvestigationLimitation>(model.Limitations.Count);
        foreach (var item in model.Limitations)
        {
            if (!TryLimitation(item, equipmentIds, out var limitation))
            {
                return false;
            }

            limitations.Add(limitation!);
        }

        investigation = new FacilityInvestigation(
            analysis.FacilityId,
            analysis.From,
            analysis.To,
            model.OverallSummary,
            observations,
            hypotheses,
            limitations);
        return true;
    }

    private static bool TryObservation(
        ModelObservation? item,
        HashSet<Guid> equipmentIds,
        EvidenceCatalog catalog,
        out InvestigationObservation? observation)
    {
        observation = null;
        if (item is null
            || string.IsNullOrWhiteSpace(item.Id)
            || string.IsNullOrWhiteSpace(item.Title)
            || string.IsNullOrWhiteSpace(item.Description)
            || item.EquipmentIds is null
            || item.EvidenceReferenceIds is null
            || !TryTimestamp(item.From, out var from)
            || !TryTimestamp(item.To, out var to)
            || !TryKind(item.MeasurementKind, out var kind)
            || !TryConfidence(item.Confidence, out var confidence)
            || !TryEquipmentIds(item.EquipmentIds, equipmentIds, out var parsedEquipment)
            || !TryEvidenceIds(item.EvidenceReferenceIds, catalog, out var evidenceIds))
        {
            return false;
        }

        observation = new InvestigationObservation(
            item.Id,
            item.Title,
            item.Description,
            parsedEquipment,
            kind,
            from,
            to,
            evidenceIds,
            confidence);
        return true;
    }

    private static bool TryHypothesis(
        ModelHypothesis? item,
        HashSet<string> observationIds,
        out InvestigationHypothesis? hypothesis)
    {
        hypothesis = null;
        if (item is null
            || string.IsNullOrWhiteSpace(item.Id)
            || string.IsNullOrWhiteSpace(item.Statement)
            || item.SupportingObservationIds is null
            || item.EvidenceFor is null
            || item.Uncertainties is null
            || !TryConfidence(item.Confidence, out var confidence)
            || !TryTextList(item.EvidenceFor, out var evidenceFor)
            || !TryTextList(item.Uncertainties, out var uncertainties))
        {
            return false;
        }

        var supporting = new List<string>(item.SupportingObservationIds.Count);
        foreach (var id in item.SupportingObservationIds)
        {
            if (string.IsNullOrWhiteSpace(id) || !observationIds.Contains(id))
            {
                return false;
            }

            supporting.Add(id);
        }

        hypothesis = new InvestigationHypothesis(
            item.Id,
            item.Statement,
            supporting,
            evidenceFor,
            uncertainties,
            confidence);
        return true;
    }

    private static bool TryLimitation(
        ModelLimitation? item,
        HashSet<Guid> equipmentIds,
        out InvestigationLimitation? limitation)
    {
        limitation = null;
        if (item is null
            || string.IsNullOrWhiteSpace(item.Description)
            || !TryCode(item.Code, out var code))
        {
            return false;
        }

        Guid? equipmentId = null;
        if (item.EquipmentId is not null)
        {
            if (!Guid.TryParse(item.EquipmentId, out var parsed) || !equipmentIds.Contains(parsed))
            {
                return false;
            }

            equipmentId = parsed;
        }

        limitation = new InvestigationLimitation(code, equipmentId, item.Description);
        return true;
    }

    private static bool TryEquipmentIds(
        List<string?> ids,
        HashSet<Guid> equipmentIds,
        out IReadOnlyList<Guid> parsed)
    {
        var result = new List<Guid>(ids.Count);
        foreach (var id in ids)
        {
            if (!Guid.TryParse(id, out var equipmentId) || !equipmentIds.Contains(equipmentId))
            {
                parsed = [];
                return false;
            }

            result.Add(equipmentId);
        }

        parsed = result;
        return true;
    }

    private static bool TryEvidenceIds(
        List<string?> ids,
        EvidenceCatalog catalog,
        out IReadOnlyList<string> parsed)
    {
        var result = new List<string>(ids.Count);
        foreach (var id in ids)
        {
            // The catalog generated for this investigation is authoritative.
            // An id that merely matches the naming pattern is still rejected.
            if (!catalog.Contains(id))
            {
                parsed = [];
                return false;
            }

            result.Add(id!);
        }

        parsed = result;
        return true;
    }

    private static bool TryTextList(List<string?> items, out IReadOnlyList<string> parsed)
    {
        var result = new List<string>(items.Count);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                parsed = [];
                return false;
            }

            result.Add(item);
        }

        parsed = result;
        return true;
    }

    private static bool TryKind(string? text, out MeasurementKind? kind)
    {
        if (text is null)
        {
            kind = null;
            return true;
        }

        if (text == nameof(MeasurementKind.Temperature))
        {
            kind = MeasurementKind.Temperature;
            return true;
        }

        if (text == nameof(MeasurementKind.RelativeHumidity))
        {
            kind = MeasurementKind.RelativeHumidity;
            return true;
        }

        kind = null;
        return false;
    }

    private static bool TryConfidence(string? text, out InvestigationConfidence confidence)
    {
        if (text == nameof(InvestigationConfidence.Low))
        {
            confidence = InvestigationConfidence.Low;
            return true;
        }

        if (text == nameof(InvestigationConfidence.Medium))
        {
            confidence = InvestigationConfidence.Medium;
            return true;
        }

        if (text == nameof(InvestigationConfidence.High))
        {
            confidence = InvestigationConfidence.High;
            return true;
        }

        confidence = default;
        return false;
    }

    private static bool TryCode(string? text, out InvestigationLimitationCode code)
    {
        if (text == nameof(InvestigationLimitationCode.IncompleteCoverage))
        {
            code = InvestigationLimitationCode.IncompleteCoverage;
            return true;
        }

        if (text == nameof(InvestigationLimitationCode.AmbiguousData))
        {
            code = InvestigationLimitationCode.AmbiguousData;
            return true;
        }

        if (text == nameof(InvestigationLimitationCode.MissingData))
        {
            code = InvestigationLimitationCode.MissingData;
            return true;
        }

        if (text == nameof(InvestigationLimitationCode.InsufficientEvidence))
        {
            code = InvestigationLimitationCode.InsufficientEvidence;
            return true;
        }

        code = default;
        return false;
    }

    private static bool TryTimestamp(string? text, out DateTime timestamp) =>
        DateTime.TryParseExact(
            text,
            AnalysisPeriodService.TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out timestamp);

    private sealed class ModelInvestigation
    {
        public string? OverallSummary { get; set; }

        public List<ModelObservation>? Observations { get; set; }

        public List<ModelHypothesis>? Hypotheses { get; set; }

        public List<ModelLimitation>? Limitations { get; set; }
    }

    private sealed class ModelObservation
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public List<string?>? EquipmentIds { get; set; }

        public string? MeasurementKind { get; set; }

        public string? From { get; set; }

        public string? To { get; set; }

        public List<string?>? EvidenceReferenceIds { get; set; }

        public string? Confidence { get; set; }
    }

    private sealed class ModelHypothesis
    {
        public string? Id { get; set; }

        public string? Statement { get; set; }

        public List<string?>? SupportingObservationIds { get; set; }

        public List<string?>? EvidenceFor { get; set; }

        public List<string?>? Uncertainties { get; set; }

        public string? Confidence { get; set; }
    }

    private sealed class ModelLimitation
    {
        public string? Code { get; set; }

        public string? EquipmentId { get; set; }

        public string? Description { get; set; }
    }
}
