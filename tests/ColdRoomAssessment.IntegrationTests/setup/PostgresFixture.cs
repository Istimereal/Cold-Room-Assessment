using ColdRoomAssessment.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ColdRoomAssessment.IntegrationTests.Setup;

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    private string? _previousConnectionString;

    public string ConnectionString { get; private set; } = string.Empty;

    public ColdRoomApiFactory ApiFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ColdRoom");
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        Environment.SetEnvironmentVariable("ConnectionStrings__ColdRoom", ConnectionString);

        var options = new DbContextOptionsBuilder<ColdRoomDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new ColdRoomDbContext(options);
        await db.Database.MigrateAsync();

        ApiFactory = new ColdRoomApiFactory(ConnectionString);
    }

    public async Task ResetAsync()
    {
        var options = new DbContextOptionsBuilder<ColdRoomDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new ColdRoomDbContext(options);
        await db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE "Measurements", "DataImports", "Equipment", "EquipmentTypes", "Facilities"
            RESTART IDENTITY CASCADE
            """);
    }

    public async Task DisposeAsync()
    {
        if (ApiFactory is not null)
        {
            await ApiFactory.DisposeAsync();
        }

        Environment.SetEnvironmentVariable("ConnectionStrings__ColdRoom", _previousConnectionString);
        await _container.DisposeAsync();
    }
}
