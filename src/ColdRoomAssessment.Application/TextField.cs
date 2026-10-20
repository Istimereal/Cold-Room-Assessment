namespace ColdRoomAssessment.Application;

public static class TextField
{
    public const int NameMaxLength = 200;

    public const int PlacementMaxLength = 500;

    public static bool TryTrim(
        string? value,
        string fieldName,
        int maxLength,
        out string trimmed,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            trimmed = string.Empty;
            error = $"{fieldName} is required.";
            return false;
        }

        trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            error = $"{fieldName} must be at most {maxLength} characters.";
            return false;
        }

        error = null;
        return true;
    }
}
