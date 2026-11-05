using System.Net;
using System.Text.Json;
using ColdRoomAssessment.Application;
using ColdRoomAssessment.Domain;
using ColdRoomAssessment.Infrastructure;
using ColdRoomAssessment.IntegrationTests.Setup;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.IntegrationTests.Http;

[Collection(IntegrationCollection.Name)]
public sealed class EquipmentTypeEndpointTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

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

    [Fact]
    public async Task List_returns_id_and_name_ordered_by_name_then_id()
    {
        var low = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var high = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var other = Guid.Parse("00000000-0000-0000-0000-000000000010");
        await using var db = CreateDbContext();
        db.EquipmentTypes.AddRange(
            new EquipmentType { Id = other, Name = "sensor" },
            new EquipmentType { Id = high, Name = "m-type" },
            new EquipmentType { Id = low, Name = "M-type" });
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/equipment-types");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(["M-type", "m-type", "sensor"], items.Select(item => item.GetProperty("name").GetString()));
        Assert.Equal([low, high, other], items.Select(item => item.GetProperty("id").GetGuid()));
        Assert.All(items, item =>
        {
            var names = item.EnumerateObject().Select(property => property.Name).ToArray();
            Assert.Equal(["id", "name"], names);
        });
    }

    private ColdRoomDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ColdRoomDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;
        return new ColdRoomDbContext(options);
    }

    private static string Location(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Location", out var locations));
        return Assert.Single(locations);
    }
}
