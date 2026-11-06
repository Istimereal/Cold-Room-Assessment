#pragma warning disable OPENAI001 // The test reads the experimental Responses request body.

using System.Text;
using ColdRoomAssessment.Infrastructure;
using OpenAI.Responses;

namespace ColdRoomAssessment.IntegrationTests.Http;

public sealed class OpenAIResponsesTransportTests
{
    [Fact]
    public void Request_body_bytes_keep_the_model_and_store_false()
    {
        var request = new CreateResponseOptions
        {
            Model = "gpt-6-luna",
            StoredOutputEnabled = false
        };
        request.InputItems.Add(ResponseItem.CreateUserMessageItem("ping"));

        var json = Encoding.UTF8.GetString(ResponsesRequestTransport.CreateRequestBody(request));

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
