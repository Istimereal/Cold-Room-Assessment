using System.Globalization;

namespace ColdRoomAssessment.Application.Imports;

public static class Dht22LogParser
{
    private static readonly string[] Header = ["Date", "Time", "Temperature", "Humidity"];

    public static Dht22LogParseResult Parse(string text)
    {
        var rows = new List<Dht22LogRow>();
        var invalidRowCount = 0;
        var formatRecognized = false;
        var normalized = text.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

        foreach (var rawLine in normalized.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var fields = rawLine.Split(';');
            if (IsHeader(fields))
            {
                formatRecognized = true;
                continue;
            }

            if (!formatRecognized || !TryParseRow(fields, out var row))
            {
                invalidRowCount++;
                continue;
            }

            rows.Add(row);
        }

        return new Dht22LogParseResult(formatRecognized, rows, invalidRowCount);
    }

    private static bool IsHeader(string[] fields)
    {
        if (fields.Length != Header.Length)
        {
            return false;
        }

        for (var index = 0; index < Header.Length; index++)
        {
            if (!string.Equals(fields[index].Trim(), Header[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseRow(string[] fields, out Dht22LogRow row)
    {
        row = null!;
        if (fields.Length != Header.Length)
        {
            return false;
        }

        if (!DateTime.TryParseExact(
                fields[0].Trim(),
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return false;
        }

        if (!DateTime.TryParseExact(
                fields[1].Trim(),
                "HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var time))
        {
            return false;
        }

        if (!TryParseNumber(fields[2], out var temperature) || !TryParseNumber(fields[3], out var humidity))
        {
            return false;
        }

        var measuredAt = new DateTime(
            date.Year,
            date.Month,
            date.Day,
            time.Hour,
            time.Minute,
            time.Second,
            DateTimeKind.Unspecified);
        row = new Dht22LogRow(measuredAt, temperature, humidity);
        return true;
    }

    private static bool TryParseNumber(string field, out double value)
    {
        value = 0;
        var text = field.Trim();
        if (text.Length == 0 || (text.Contains('.') && text.Contains(',')))
        {
            return false;
        }

        if (text.Contains(','))
        {
            text = text.Replace(',', '.');
        }

        return double.TryParse(
            text,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }
}

public sealed record Dht22LogRow(DateTime MeasuredAt, double Temperature, double Humidity);

public sealed record Dht22LogParseResult(
    bool FormatRecognized,
    IReadOnlyList<Dht22LogRow> Rows,
    int InvalidRowCount);
