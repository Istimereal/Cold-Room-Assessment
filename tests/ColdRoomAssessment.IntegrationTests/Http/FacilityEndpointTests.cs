using System.Net;
using ColdRoomAssessment.Application;
using ColdRoomAssessment.IntegrationTests.Setup;

namespace ColdRoomAssessment.IntegrationTests.Http;

[Collection(IntegrationCollection.Name)]
public sealed class FacilityEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    [Fact]
    public async Task Create_returns_the_facility_and_get_reads_it_back()
    {
        var created = await Client.PostFacilityAsync("Cold Room A");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var facility = await created.ReadRequiredAsync<FacilityResponse>();
        ColdRoomHttp.AssertVersion7(facility.Id);
        Assert.Equal("Cold Room A", facility.Name);
        Assert.Empty(facility.Equipment);
        Assert.Equal($"/api/facilities/{facility.Id}", Location(created));

        var fetched = await Client.GetAsync($"/api/facilities/{facility.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var body = await fetched.ReadRequiredAsync<FacilityResponse>();
        Assert.Equal(facility.Id, body.Id);
        Assert.Equal("Cold Room A", body.Name);
        Assert.Empty(body.Equipment);
    }

    [Fact]
    public async Task Get_includes_equipment()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");
        var created = await Client.PostEquipmentAsync(
            facility.Id,
            equipmentType.Id,
            "SENSOR01",
            "Near entrance, upper shelf");
        var equipment = await created.ReadRequiredAsync<EquipmentResponse>();

        var fetched = await Client.GetAsync($"/api/facilities/{facility.Id}");
        var body = await fetched.ReadRequiredAsync<FacilityResponse>();
        var item = Assert.Single(body.Equipment);
        Assert.Equal(equipment.Id, item.Id);
        Assert.Equal(equipmentType.Id, item.EquipmentTypeId);
        Assert.Equal("SENSOR01", item.Name);
        Assert.Equal("Near entrance, upper shelf", item.Placement);
    }

    [Fact]
    public async Task Create_allows_the_same_name_for_another_facility()
    {
        var first = await Client.CreateFacilityAsync("Cold Room A");
        var second = await Client.CreateFacilityAsync("Cold Room A");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("Cold Room A", second.Name);
    }

    [Fact]
    public async Task Create_trims_the_name()
    {
        var facility = await Client.CreateFacilityAsync("  Cold Room A  ");

        Assert.Equal("Cold Room A", facility.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_when_name_is_blank_returns_400(string? name)
    {
        var response = await Client.PostFacilityAsync(name);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Name is required.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Create_when_name_is_too_long_returns_400()
    {
        var response = await Client.PostFacilityAsync(new string('a', TextField.NameMaxLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Name must be at most 200 characters.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Create_accepts_a_name_at_the_length_limit()
    {
        var name = new string('a', TextField.NameMaxLength);
        var facility = await Client.CreateFacilityAsync(name);

        Assert.Equal(name, facility.Name);
    }

    [Fact]
    public async Task Get_when_missing_returns_404()
    {
        var response = await Client.GetAsync($"/api/facilities/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Facility was not found.", await response.ReadErrorAsync());
    }

    private static string Location(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Location", out var locations));
        return Assert.Single(locations);
    }
}
