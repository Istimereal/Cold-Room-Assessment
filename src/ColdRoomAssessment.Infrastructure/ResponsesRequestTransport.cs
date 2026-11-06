#pragma warning disable OPENAI001 // CreateResponseOptions is experimental in OpenAI 2.14.0.

using System.ClientModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OpenAI.Responses;

namespace ColdRoomAssessment.Infrastructure;

internal static class ResponsesRequestTransport
{
    private const string DefaultEndpoint = "https://api.openai.com/v1";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(100)
    };

    public static async Task<string> CreateAsync(
        OpenAIOptions options,
        string apiKey,
        CreateResponseOptions request,
        CancellationToken cancellationToken)
    {
        var bytes = CreateRequestBody(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, RequestUri(options));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", HeaderSafeApiKey(apiKey));
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Content = new ByteArrayContent(bytes);
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await Http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(ProviderError(body, (int)response.StatusCode), null, response.StatusCode);
        }

        try
        {
            return ReadOutputText(body) ?? "";
        }
        catch (JsonException)
        {
            throw new HttpRequestException(
                $"Responses request returned a body that was not JSON. Status {(int)response.StatusCode}.");
        }
    }

    internal static string HeaderSafeApiKey(string apiKey)
    {
        var key = apiKey.Trim();
        if (key.IndexOfAny('\r', '\n', '\0') >= 0)
        {
            throw new InvalidOperationException("OpenAI API key contains invalid control characters.");
        }

        return key;
    }

    internal static byte[] CreateRequestBody(CreateResponseOptions request)
    {
        using BinaryContent content = request;
        using var buffer = new MemoryStream();
        content.WriteTo(buffer, CancellationToken.None);
        return buffer.ToArray();
    }

    private static Uri RequestUri(OpenAIOptions options)
    {
        var endpoint = string.IsNullOrWhiteSpace(options.Endpoint)
            ? DefaultEndpoint
            : options.Endpoint.Trim().TrimEnd('/');
        return new Uri(endpoint + "/responses");
    }

    private static string ProviderError(string body, int statusCode)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                var text = message.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }
        catch (JsonException)
        {
        }

        return $"Responses request failed with status {statusCode}.";
    }

    private static string? ReadOutputText(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("output", out var output)
            || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var text = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("type", out var itemType)
                || itemType.ValueKind != JsonValueKind.String
                || itemType.GetString() != "message"
                || !item.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object
                    || !part.TryGetProperty("type", out var partType)
                    || partType.ValueKind != JsonValueKind.String
                    || partType.GetString() != "output_text"
                    || !part.TryGetProperty("text", out var partText)
                    || partText.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                text.Append(partText.GetString());
            }
        }

        return text.Length == 0 ? null : text.ToString();
    }
}
