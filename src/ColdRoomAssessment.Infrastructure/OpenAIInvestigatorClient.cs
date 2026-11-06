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
        CancellationToken cancellationToken)
    {
        var apiKey = options.Value.ApiKey;
        var model = options.Value.Model;
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            throw new InvestigatorNotConfiguredException();
        }

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
        request.InputItems.Add(ResponseItem.CreateUserMessageItem(
            JsonSerializer.Serialize(catalog, CatalogJson)));

        try
        {
            ClientResult<ResponseResult> response = await new ResponsesClient(apiKey).CreateResponseAsync(
                request,
                cancellationToken);
            var text = response.Value.GetOutputText();
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
