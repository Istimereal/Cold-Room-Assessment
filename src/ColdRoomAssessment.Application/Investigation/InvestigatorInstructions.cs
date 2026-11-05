namespace ColdRoomAssessment.Application.Investigation;

public static class InvestigatorInstructions
{
    public const string Text =
        """
        Use only the supplied evidence.
        Separate observations from hypotheses.
        Do not claim a cause the evidence does not show.
        Mention coverage, ambiguity, and missing data when they weaken a claim.
        Identify equipment by the supplied id and name.
        Cite catalog ids. Do not construct new evidence ids.
        An evidence id is valid only when it is present in the supplied catalog, even if it matches the id pattern.
        No operational advice.
        No experiment actions or room events.
        Say when the evidence is insufficient.
        """;
}
