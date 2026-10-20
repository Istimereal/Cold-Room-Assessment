using System.Net;
using System.Net.Http.Json;
using ColdRoomAssessment.Application;
using ColdRoomAssessment.IntegrationTests.Setup;

namespace ColdRoomAssessment.IntegrationTests.Http;

[Collection(IntegrationCollection.Name)]
public sealed class EquipmentEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    [Fact]
    public async Task Create_returns_equipment_for_an_existing_facility_and_type()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");

        var created = await Client.PostEquipmentAsync(
            facility.Id,
            equipmentType.Id,
            "SENSOR01",
            "Near entrance, upper shelf");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var equipment = await created.ReadRequiredAsync<EquipmentResponse>();
        ColdRoomHttp.AssertVersion7(equipment.Id);
        Assert.Equal(facility.Id, equipment.FacilityId);
        Assert.Equal("Cold Room A", equipment.FacilityName);
        Assert.Equal(equipmentType.Id, equipment.EquipmentTypeId);
        Assert.Equal("DHT22 Logger", equipment.EquipmentTypeName);
        Assert.Equal("SENSOR01", equipment.Name);
        Assert.Equal("Near entrance, upper shelf", equipment.Placement);
        Assert.Equal($"/api/equipment/{equipment.Id}", Location(created));
    }

    [Fact]
    public async Task Get_includes_facility_and_type_names()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");
        var created = await Client.PostEquipmentAsync(
            facility.Id,
            equipmentType.Id,
            "SENSOR01",
            "Near entrance, upper shelf");
        var equipment = await created.ReadRequiredAsync<EquipmentResponse>();

        var fetched = await Client.GetAsync($"/api/equipment/{equipment.Id}");

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var body = await fetched.ReadRequiredAsync<EquipmentResponse>();
        Assert.Equal(equipment.Id, body.Id);
        Assert.Equal("Cold Room A", body.FacilityName);
        Assert.Equal("DHT22 Logger", body.EquipmentTypeName);
        Assert.Equal("SENSOR01", body.Name);
        Assert.Equal("Near entrance, upper shelf", body.Placement);
    }

    [Fact]
    public async Task Create_allows_the_same_name_in_another_facility()
    {
        var firstFacility = await Client.CreateFacilityAsync("Cold Room A");
        var secondFacility = await Client.CreateFacilityAsync("Cold Room B");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");

        var first = await Client.PostEquipmentAsync(
            firstFacility.Id,
            equipmentType.Id,
            "SENSOR01",
            "Near entrance, upper shelf");
        var second = await Client.PostEquipmentAsync(
            secondFacility.Id,
            equipmentType.Id,
            "SENSOR01",
            "Back wall, lower shelf");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.NotEqual(
            (await first.ReadRequiredAsync<EquipmentResponse>()).Id,
            (await second.ReadRequiredAsync<EquipmentResponse>()).Id);
    }

    [Fact]
    public async Task Create_when_the_trimmed_name_exists_in_the_same_facility_returns_409()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");
        await Client.PostEquipmentAsync(facility.Id, equipmentType.Id, "SENSOR01", "Near entrance");

        var response = await Client.PostEquipmentAsync(
            facility.Id,
            equipmentType.Id,
            "  SENSOR01  ",
            "Another shelf");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "Equipment named 'SENSOR01' already exists in this facility.",
            await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Create_when_the_facility_does_not_exist_returns_404()
    {
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");

        var response = await Client.PostEquipmentAsync(
            Guid.CreateVersion7(),
            equipmentType.Id,
            "SENSOR01",
            "Near entrance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Facility was not found.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Create_when_the_equipment_type_does_not_exist_returns_404()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");

        var response = await Client.PostEquipmentAsync(
            facility.Id,
            Guid.CreateVersion7(),
            "SENSOR01",
            "Near entrance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Equipment type was not found.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Create_when_the_equipment_type_id_is_missing_returns_400()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");

        var response = await Client.PostAsJsonAsync(
            $"/api/facilities/{facility.Id}/equipment",
            new { name = "SENSOR01", placement = "Near entrance" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Equipment type id is required.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Create_trims_name_and_placement()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");

        var response = await Client.PostEquipmentAsync(
            facility.Id,
            equipmentType.Id,
            "  SENSOR01  ",
            "  Near entrance, upper shelf  ");
        var equipment = await response.ReadRequiredAsync<EquipmentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("SENSOR01", equipment.Name);
        Assert.Equal("Near entrance, upper shelf", equipment.Placement);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_when_name_is_blank_returns_400(string? name)
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");

        var response = await Client.PostEquipmentAsync(facility.Id, equipmentType.Id, name, "Near entrance");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Name is required.", await response.ReadErrorAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_when_placement_is_blank_returns_400(string? placement)
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");

        var response = await Client.PostEquipmentAsync(facility.Id, equipmentType.Id, "SENSOR01", placement);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Placement is required.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Create_when_name_is_too_long_returns_400()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");

        var response = await Client.PostEquipmentAsync(
            facility.Id,
            equipmentType.Id,
            new string('a', TextField.NameMaxLength + 1),
            "Near entrance");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Name must be at most 200 characters.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Create_when_placement_is_too_long_returns_400()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");

        var response = await Client.PostEquipmentAsync(
            facility.Id,
            equipmentType.Id,
            "SENSOR01",
            new string('p', TextField.PlacementMaxLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Placement must be at most 500 characters.", await response.ReadErrorAsync());
    }

    [Fact]
    public async Task Create_accepts_placement_at_the_length_limit()
    {
        var facility = await Client.CreateFacilityAsync("Cold Room A");
        var equipmentType = await Client.CreateEquipmentTypeAsync("DHT22 Logger");
        var placement = new string('p', TextField.PlacementMaxLength);

        var response = await Client.PostEquipmentAsync(facility.Id, equipmentType.Id, "SENSOR01", placement);
        var equipment = await response.ReadRequiredAsync<EquipmentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(placement, equipment.Placement);
    }

    [Fact]
    public async Task Get_when_missing_returns_404()
    {
        var response = await Client.GetAsync($"/api/equipment/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Equipment was not found.", await response.ReadErrorAsync());
    }

    private static string Location(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Location", out var locations));
        return Assert.Single(locations);
    }
}
