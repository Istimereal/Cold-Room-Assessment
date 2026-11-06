#pragma warning disable OPENAI001 // The Responses API is experimental in this SDK. Keep the suppression in this file.

using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ColdRoomAssessment.Application.Investigation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace ColdRoomAssessment.Infrastructure;

public sealed class OpenAIInvestigatorClient(
    IOptions<OpenAIOptions> options,
    ILogger<OpenAIInvestigatorClient> logger) : IInvestigatorClient
{
    private static readonly JsonSerializerOptions CatalogJson = new(JsonSerializerDefaults.Web);

    private static readonly Regex SensitiveContent = new(
        """sk-[A-Za-z0-9_\-.]{8,}|Bearer\s+\S+|authorization\s*[:=]\s*\S+|api[_-]?key\s*[:=]\s*\S+""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly BinaryData Schema = BinaryData.FromString("""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "overallSummary": { "type": "string" },
            "observations": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "id": { "type": "string" },
                  "title": { "type": "string" },
                  "description": { "type": "string" },
                  "equipmentIds": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "measurementKind": {
                    "anyOf": [
                      { "type": "string", "enum": ["Temperature", "RelativeHumidity"] },
                      { "type": "null" }
                    ]
                  },
                  "from": { "type": "string" },
                  "to": { "type": "string" },
                  "evidenceReferenceIds": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "confidence": {
                    "type": "string",
                    "enum": ["Low", "Medium", "High"]
                  }
                },
                "required": [
                  "id",
                  "title",
                  "description",
                  "equipmentIds",
                  "measurementKind",
                  "from",
                  "to",
                  "evidenceReferenceIds",
                  "confidence"
                ]
              }
            },
            "hypotheses": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "id": { "type": "string" },
                  "statement": { "type": "string" },
                  "supportingObservationIds": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "evidenceFor": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "uncertainties": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "confidence": {
                    "type": "string",
                    "enum": ["Low", "Medium", "High"]
                  }
                },
                "required": [
                  "id",
                  "statement",
                  "supportingObservationIds",
                  "evidenceFor",
                  "uncertainties",
                  "confidence"
                ]
              }
            },
            "limitations": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "code": {
                    "type": "string",
                    "enum": [
                      "IncompleteCoverage",
                      "AmbiguousData",
                      "MissingData",
                      "InsufficientEvidence"
                    ]
                  },
                  "equipmentId": {
                    "anyOf": [
                      { "type": "string" },
                      { "type": "null" }
                    ]
                  },
                  "description": { "type": "string" }
                },
                "required": ["code", "equipmentId", "description"]
              }
            }
          },
          "required": ["overallSummary", "observations", "hypotheses", "limitations"]
        }
        """);

    public async Task<string> InvestigateAsync(
        EvidenceCatalog catalog,
        string instructions,
        CancellationToken cancellationToken,
        IReadOnlyList<ContextEvent>? contextEvents = null)
    {
        var apiKey = options.Value.ApiKey;
        var model = options.Value.Model;
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            throw new InvestigatorNotConfiguredException();
        }

        var request = InvestigationRequest(catalog, model, instructions, contextEvents);

        try
        {
            LogSerializedRequest(request);
            LogEvidenceShape(catalog, ResponsesRequestTransport.CreateRequestBody(request).Length);
            var text = await ResponsesRequestTransport.CreateAsync(
                options.Value,
                apiKey,
                request,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvestigatorUnavailableException();
            }

            return text;
        }
        catch (InvestigatorUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            LogProviderFailure(ex, statusCode: null);
            throw new InvestigatorUnavailableException();
        }
        catch (ClientResultException ex)
        {
            LogProviderFailure(ex, ex.Status > 0 ? ex.Status : null);
            throw new InvestigatorUnavailableException();
        }
        catch (HttpRequestException ex)
        {
            LogProviderFailure(ex, ex.StatusCode is { } status ? (int)status : null);
            throw new InvestigatorUnavailableException();
        }
    }

    private void LogEvidenceShape(EvidenceCatalog catalog, int payloadBytes)
    {
        foreach (var equipmentId in catalog.Items.Select(item => item.EquipmentId).Distinct())
        {
            var temperature = catalog.Items
                .Where(item => item.EquipmentId == equipmentId && item.MeasurementKind == "Temperature")
                .ToList();
            logger.LogInformation(
                "Temperature series {EquipmentId}. ValidSamples: {ValidSamples}; Rising: {Rising}; Falling: {Falling}; Stable: {Stable}",
                equipmentId,
                temperature.FirstOrDefault(item => item.Fact == "count")?.Value ?? "unavailable",
                temperature.Count(item => item.Fact.StartsWith("rise:", StringComparison.Ordinal)),
                temperature.Count(item => item.Fact.StartsWith("fall:", StringComparison.Ordinal)),
                temperature.Count(item => item.Fact.StartsWith("stable:", StringComparison.Ordinal)));
        }

        logger.LogInformation(
            "Investigator evidence catalog. Items: {Items}; TemperatureEvents: {TemperatureEvents}; StablePeriods: {StablePeriods}; PayloadBytes: {PayloadBytes}",
            catalog.Items.Count,
            catalog.Items.Count(item =>
                item.MeasurementKind == "Temperature"
                && (item.Fact.StartsWith("rise:", StringComparison.Ordinal)
                    || item.Fact.StartsWith("fall:", StringComparison.Ordinal))),
            catalog.Items.Count(item =>
                item.MeasurementKind == "Temperature" && item.Fact.StartsWith("stable:", StringComparison.Ordinal)),
            payloadBytes);
    }

    internal static int InvestigationPayloadBytes(EvidenceCatalog catalog) =>
        ResponsesRequestTransport.CreateRequestBody(
            InvestigationRequest(catalog, "gpt-6-luna", InvestigatorInstructions.Text, contextEvents: null)).Length;

    internal static string UserMessage(EvidenceCatalog catalog, IReadOnlyList<ContextEvent>? contextEvents)
    {
        if (contextEvents is null)
        {
            return JsonSerializer.Serialize(catalog, CatalogJson);
        }

        return JsonSerializer.Serialize(
            new InvestigationContextPayload(catalog, ContextEventDocument.FromEvents(contextEvents)),
            CatalogJson);
    }

    private static CreateResponseOptions InvestigationRequest(
        EvidenceCatalog catalog,
        string model,
        string instructions,
        IReadOnlyList<ContextEvent>? contextEvents)
    {
        var request = new CreateResponseOptions
        {
            Model = model,
            StoredOutputEnabled = false,
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "facility_investigation",
                    jsonSchema: Schema,
                    jsonSchemaIsStrict: true)
            }
        };
        request.InputItems.Add(ResponseItem.CreateSystemMessageItem(instructions));
        request.InputItems.Add(ResponseItem.CreateUserMessageItem(UserMessage(catalog, contextEvents)));
        return request;
    }

    private sealed record InvestigationContextPayload(
        EvidenceCatalog SensorEvidence,
        IReadOnlyList<ContextEventDocument> ContextEvents);

    private void LogSerializedRequest(CreateResponseOptions request)
    {
        try
        {
            var payload = DescribeSerializedRequest(request);
            logger.LogInformation(
                "OpenAI Investigator serialized request. ContainsModel: {ContainsModel}; Model: {SerializedModel}; TopLevelProperties: {TopLevelProperties}; PayloadBytes: {PayloadBytes}",
                payload.ContainsModel ? "True" : "False",
                RedactSerializedModel(payload.Model),
                payload.TopLevelProperties,
                payload.PayloadBytes);
        }
        catch (Exception)
        {
            logger.LogInformation(
                "OpenAI Investigator serialized request. ContainsModel: unavailable; Model: unavailable; TopLevelProperties: unavailable; PayloadBytes: unavailable");
        }
    }

    // The posted body is this same SDK wire serialization, written to the HTTP request as bytes.
    internal static SerializedResponsesPayload DescribeSerializedRequest(CreateResponseOptions request)
    {
        using BinaryContent content = request;
        using var buffer = new MemoryStream();
        content.WriteTo(buffer, CancellationToken.None);
        var payloadBytes = checked((int)buffer.Length);
        buffer.Position = 0;
        using var document = JsonDocument.Parse(buffer);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new SerializedResponsesPayload(false, "absent", "unavailable", payloadBytes);
        }

        var names = new List<string>();
        var containsModel = false;
        var model = "absent";
        foreach (var property in document.RootElement.EnumerateObject())
        {
            names.Add(property.Name);
            if (!property.NameEquals("model"u8))
            {
                continue;
            }

            containsModel = true;
            model = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? "null",
                JsonValueKind.Null => "null",
                _ => "non-string"
            };
        }

        return new SerializedResponsesPayload(containsModel, model, string.Join(',', names), payloadBytes);
    }

    private string RedactSerializedModel(string value)
    {
        var sanitized = value.ReplaceLineEndings(" ");
        var apiKey = options.Value.ApiKey;
        if (!string.IsNullOrEmpty(apiKey) && apiKey.Length >= 8)
        {
            sanitized = sanitized.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
        }

        sanitized = SensitiveContent.Replace(sanitized, "[redacted]");
        const int maxLength = 80;
        if (sanitized.Length > maxLength)
        {
            sanitized = string.Concat(sanitized.AsSpan(0, maxLength), "…");
        }

        return sanitized;
    }

    internal readonly record struct SerializedResponsesPayload(
        bool ContainsModel,
        string Model,
        string TopLevelProperties,
        int PayloadBytes);

    private void LogProviderFailure(Exception exception, int? statusCode)
    {
        logger.LogError(
            "OpenAI investigator call failed. ExceptionType: {ExceptionType}; StatusCode: {StatusCode}; Message: {ProviderMessage}",
            exception.GetType().FullName,
            statusCode?.ToString() ?? "unavailable",
            SafeProviderMessage(exception.Message));
    }

    private string SafeProviderMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "unavailable";
        }

        var sanitized = message.ReplaceLineEndings(" ").Trim();
        var apiKey = options.Value.ApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey) && apiKey.Length >= 8)
        {
            sanitized = sanitized.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
        }

        sanitized = SensitiveContent.Replace(sanitized, "[redacted]");
        const int maxLength = 400;
        if (sanitized.Length > maxLength)
        {
            sanitized = string.Concat(sanitized.AsSpan(0, maxLength), "…");
        }

        return sanitized;
    }
}
