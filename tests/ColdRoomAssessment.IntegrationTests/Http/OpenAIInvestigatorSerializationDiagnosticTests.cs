#pragma warning disable OPENAI001 // The diagnostic reads the experimental Responses request the investigator sends.

using ColdRoomAssessment.Infrastructure;
using OpenAI.Responses;

namespace ColdRoomAssessment.IntegrationTests.Http;

public sealed class OpenAIInvestigatorSerializationDiagnosticTests
{
    [Fact]
    public void Serialized_request_reports_top_level_model_without_message_content()
    {
        const string secret = "SECRET-EVIDENCE-TEXT";
        var request = new CreateResponseOptions
        {
            Model = "gpt-6-luna",
            StoredOutputEnabled = false,
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "facility_investigation",
                    jsonSchema: BinaryData.FromString(
                        """{"type":"object","additionalProperties":false,"properties":{"overallSummary":{"type":"string"}},"required":["overallSummary"]}"""),
                    jsonSchemaIsStrict: true)
            }
        };
        request.InputItems.Add(ResponseItem.CreateSystemMessageItem("system instructions " + secret));
        request.InputItems.Add(ResponseItem.CreateUserMessageItem(secret));

        var first = OpenAIInvestigatorClient.DescribeSerializedRequest(request);
        var second = OpenAIInvestigatorClient.DescribeSerializedRequest(request);

        Assert.Equal(first, second);
        Assert.True(first.ContainsModel);
        Assert.Equal("gpt-6-luna", first.Model);
        Assert.Equal("model,text,input,store", first.TopLevelProperties);
        Assert.True(first.PayloadBytes > 0);
        Assert.DoesNotContain(secret, first.Model);
        Assert.DoesNotContain(secret, first.TopLevelProperties);
    }

    [Fact]
    public void Serialized_request_reports_when_top_level_model_is_absent()
    {
        var request = new CreateResponseOptions
        {
            StoredOutputEnabled = false
        };
        request.InputItems.Add(ResponseItem.CreateUserMessageItem("hello"));

        var payload = OpenAIInvestigatorClient.DescribeSerializedRequest(request);
        var names = payload.TopLevelProperties.Split(',');

        Assert.False(payload.ContainsModel);
        Assert.Equal("absent", payload.Model);
        Assert.DoesNotContain("model", names);
        Assert.Contains("input", names);
        Assert.Contains("store", names);
        Assert.True(payload.PayloadBytes > 0);
    }

    [Fact]
    public void Request_body_bytes_keep_the_model_and_store_false()
    {
        var request = new CreateResponseOptions
        {
            Model = "gpt-6-luna",
            StoredOutputEnabled = false
        };
        request.InputItems.Add(ResponseItem.CreateUserMessageItem("ping"));

        var json = System.Text.Encoding.UTF8.GetString(ResponsesRequestTransport.CreateRequestBody(request));

        Assert.Contains("\"model\":\"gpt-6-luna\"", json, StringComparison.Ordinal);
        Assert.Contains("\"store\":false", json, StringComparison.Ordinal);
        Assert.StartsWith("{", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_key_trim_removes_surrounding_whitespace_before_the_header()
    {
        const string secret = "sk-test-value";

        Assert.Equal(secret, ResponsesRequestTransport.HeaderSafeApiKey("  " + secret + "\r\n"));
        Assert.Equal(secret, ResponsesRequestTransport.HeaderSafeApiKey(secret + "\n"));
    }

    [Theory]
    [InlineData("sk-test-value\nembedded")]
    [InlineData("sk-test-value\rembedded")]
    [InlineData("sk-test-value\0embedded")]
    [InlineData("\0sk-test-value")]
    public void Api_key_with_a_remaining_control_character_is_rejected(string apiKey)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ResponsesRequestTransport.HeaderSafeApiKey(apiKey));

        Assert.Equal("OpenAI API key contains invalid control characters.", exception.Message);
        Assert.DoesNotContain("sk-test-value", exception.Message, StringComparison.Ordinal);
    }
}
