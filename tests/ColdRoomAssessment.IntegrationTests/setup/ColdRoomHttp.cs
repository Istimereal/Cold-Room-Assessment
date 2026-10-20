using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColdRoomAssessment.Api;
using ColdRoomAssessment.Application;

namespace ColdRoomAssessment.IntegrationTests.Setup;

internal static class ColdRoomHttp
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Task<HttpResponseMessage> PostFacilityAsync(this HttpClient client, string? name) =>
        client.PostAsJsonAsync("/api/facilities", new { name }, Json);

    public static Task<HttpResponseMessage> PostEquipmentTypeAsync(this HttpClient client, string? name) =>
        client.PostAsJsonAsync("/api/equipment-types", new { name }, Json);

    public static Task<HttpResponseMessage> PostEquipmentAsync(
        this HttpClient client,
        Guid facilityId,
        Guid equipmentTypeId,
        string? name,
        string? placement) =>
        client.PostAsJsonAsync(
            $"/api/facilities/{facilityId}/equipment",
            new { equipmentTypeId, name, placement },
            Json);

    public static async Task<T> ReadRequiredAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<T>(Json);
        Assert.NotNull(body);
        return body;
    }

    public static async Task<string> ReadErrorAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(Json);
        Assert.NotNull(body);
        return body.Error;
    }

    public static void AssertVersion7(Guid id)
    {
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal('7', id.ToString("D")[14]);
    }

    public static async Task<FacilityResponse> CreateFacilityAsync(this HttpClient client, string name)
    {
        var response = await client.PostFacilityAsync(name);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadRequiredAsync<FacilityResponse>();
    }

    public static async Task<EquipmentTypeResponse> CreateEquipmentTypeAsync(this HttpClient client, string name)
    {
        var response = await client.PostEquipmentTypeAsync(name);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadRequiredAsync<EquipmentTypeResponse>();
    }
}
