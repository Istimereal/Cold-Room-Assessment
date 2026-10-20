using System.Net;
using ColdRoomAssessment.Application;
using ColdRoomAssessment.IntegrationTests.Setup;

namespace ColdRoomAssessment.IntegrationTests.Http;

[Collection(IntegrationCollection.Name)]
public sealed class EquipmentTypeEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    [Fact]
    public async Task Create_returns_the_type_and_get_reads_it_back()
    {
        var created = await Client.PostEquipmentTypeAsync("DHT22 Logger");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var equipmentType = await created.ReadRequiredAsync<EquipmentTypeResponse>();
        ColdRoomHttp.AssertVersion7(equipmentType.Id);
        Assert.Equal("DHT22 Logger", equipmentType.Name);
        Assert.Equal($"/api/equipment-types/{equipmentType.Id}", Location(created));

        var fetched = await Client.GetAsync($"/api/equipment-types/{equipmentType.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var body = await fetched.ReadRequiredAsync<EquipmentTypeResponse>();
        Assert.Equal(equipmentType.Id, body.Id);
        Assert.Equal("DHT22 Logger", body.Name);
    }

    [Fact]
    public async Task Create_when_the_trimmed_name_already_exists_returns_409()
    {
        await Client.CreateEquipmentTypeAsync("DHT22 Logger");

        var response = await Client.PostEquipmentTypeAsync("  DHT22 Logger  ");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "An equipment type named 'DHT22 Logger' already exists.",
            await response.ReadErrorAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_when_name_is_blank_returns_400(string? name)
    {
        var response = await Client.PostEquipmentTypeAsync(name);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Name is required.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Create_when_name_is_too_long_returns_400()
    {
        var response = await Client.PostEquipmentTypeAsync(new string('a', TextField.NameMaxLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Name must be at most 200 characters.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Get_when_missing_returns_404()
    {
        var response = await Client.GetAsync($"/api/equipment-types/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Equipment type was not found.", await response.ReadErrorAsync());
    }

    private static string Location(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Location", out var locations));
        return Assert.Single(locations);
    }
}
