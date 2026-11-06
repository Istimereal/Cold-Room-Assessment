#pragma warning disable OPENAI001 // The Responses API is experimental in this SDK. Keep the suppression in this file.

using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColdRoomAssessment.Application.Investigation;
using ColdRoomAssessment.Application.Verification;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace ColdRoomAssessment.Infrastructure;

public sealed class OpenAIVerifierClient(IOptions<OpenAIOptions> options) : IVerifierClient
{
    private static readonly JsonSerializerOptions PayloadJson = CreatePayloadJson();

    private static readonly BinaryData Schema = BinaryData.FromString("""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "facilityId": { "type": "string" },
            "from": { "type": "string" },
            "to": { "type": "string" },
            "overallVerdict": {
              "type": "string",
              "enum": ["Supported", "PartiallySupported", "NotSupported", "InsufficientEvidence"]
            },
            "observationVerifications": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "observationId": { "type": "string" },
                  "verdict": {
                    "type": "string",
                    "enum": ["Supported", "PartiallySupported", "NotSupported", "InsufficientEvidence"]
                  },
                  "explanation": { "type": "string" },
                  "evidenceIds": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "missingEvidenceIds": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "conflictingEvidenceIds": {
                    "type": "array",
                    "items": { "type": "string" }
                  }
                },
                "required": [
                  "observationId",
                  "verdict",
                  "explanation",
                  "evidenceIds",
                  "missingEvidenceIds",
                  "conflictingEvidenceIds"
                ]
              }
            },
            "hypothesisVerifications": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "hypothesisId": { "type": "string" },
                  "verdict": {
                    "type": "string",
                    "enum": ["Supported", "PartiallySupported", "NotSupported", "InsufficientEvidence"]
                  },
                  "explanation": { "type": "string" },
                  "supportingEvidenceIds": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "contradictingEvidenceIds": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "uncertainty": { "type": "string" }
                },
                "required": [
                  "hypothesisId",
                  "verdict",
                  "explanation",
                  "supportingEvidenceIds",
                  "contradictingEvidenceIds",
                  "uncertainty"
                ]
              }
            },
            "limitations": {
              "type": "array",
              "items": { "type": "string" }
            }
          },
          "required": [
            "facilityId",
            "from",
            "to",
            "overallVerdict",
            "observationVerifications",
            "hypothesisVerifications",
            "limitations"
          ]
        }
        """);

    public async Task<string> VerifyAsync(
        EvidenceCatalog catalog,
        FacilityInvestigation investigation,
        string instructions,
        CancellationToken cancellationToken)
    {
        var apiKey = options.Value.ApiKey;
        var model = options.Value.ResolveVerifierModel();
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            throw new VerifierNotConfiguredException();
        }

        var request = new CreateResponseOptions
        {
            Model = model,
            StoredOutputEnabled = false,
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "facility_verification",
                    jsonSchema: Schema,
                    jsonSchemaIsStrict: true)
            }
        };
        request.InputItems.Add(ResponseItem.CreateSystemMessageItem(instructions));
        request.InputItems.Add(ResponseItem.CreateUserMessageItem(JsonSerializer.Serialize(
            new VerifierRequest(catalog, investigation),
            PayloadJson)));

        try
        {
            var text = await ResponsesRequestTransport.CreateAsync(
                options.Value,
                apiKey,
                request,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new VerifierUnavailableException();
            }

            return text;
        }
        catch (VerifierUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new VerifierUnavailableException();
        }
        catch (ClientResultException)
        {
            throw new VerifierUnavailableException();
        }
        catch (HttpRequestException)
        {
            throw new VerifierUnavailableException();
        }
    }

    private static JsonSerializerOptions CreatePayloadJson()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        json.Converters.Add(new JsonStringEnumConverter());
        return json;
    }

    private sealed record VerifierRequest(EvidenceCatalog DeterministicEvidence, FacilityInvestigation Investigation);
}
