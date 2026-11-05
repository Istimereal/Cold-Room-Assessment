using System.Globalization;
using System.Text.Json;
using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Coverage;
using ColdRoomAssessment.Application.Investigation;

namespace ColdRoomAssessment.Application.Verification;

public static class SubmittedInvestigation
{
    public static bool TryAccept(
        string? json,
        FacilityAnalysis analysis,
        EvidenceCatalog catalog,
        out FacilityInvestigation? investigation)
    {
        investigation = null;
        if (string.IsNullOrWhiteSpace(json) || !IdentityMatches(json, analysis))
        {
            return false;
        }

        if (!InvestigationResultValidator.TryCreate(json, analysis, catalog, out investigation))
        {
            return false;
        }

        var hypothesisIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hypothesis in investigation!.Hypotheses)
        {
            if (!hypothesisIds.Add(hypothesis.Id))
            {
                investigation = null;
                return false;
            }
        }

        return true;
    }

    private static bool IdentityMatches(string json, FacilityAnalysis analysis)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var root = document.RootElement;
            if (!TryString(root, "facilityId", "FacilityId", out var facilityText)
                || !Guid.TryParse(facilityText, out var facilityId)
                || facilityId != analysis.FacilityId
                || !TryString(root, "from", "From", out var fromText)
                || fromText != Timestamp(analysis.From)
                || !TryString(root, "to", "To", out var toText)
                || toText != Timestamp(analysis.To))
            {
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryString(JsonElement root, string camelName, string pascalName, out string text)
    {
        text = string.Empty;
        if (!root.TryGetProperty(camelName, out var property) && !root.TryGetProperty(pascalName, out property))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var value = property.GetString();
        if (value is null)
        {
            return false;
        }

        text = value;
        return true;
    }

    private static string Timestamp(DateTime value) =>
        value.ToString(AnalysisPeriodService.TimestampFormat, CultureInfo.InvariantCulture);
}
