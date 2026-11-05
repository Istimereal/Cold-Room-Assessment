namespace ColdRoomAssessment.Infrastructure;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public string? ApiKey { get; set; }

    public string? Model { get; set; }
}
