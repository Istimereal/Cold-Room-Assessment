namespace ColdRoomAssessment.Infrastructure;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public string? ApiKey { get; set; }

    public string? Model { get; set; }

    public string? VerifierModel { get; set; }

    /// <summary>
    /// Optional Responses API base, including <c>/v1</c>. When blank, requests go to https://api.openai.com/v1.
    /// </summary>
    public string? Endpoint { get; set; }

    public string? ResolveVerifierModel()
    {
        if (!string.IsNullOrWhiteSpace(VerifierModel))
        {
            return VerifierModel.Trim();
        }

        return string.IsNullOrWhiteSpace(Model) ? null : Model.Trim();
    }
}
