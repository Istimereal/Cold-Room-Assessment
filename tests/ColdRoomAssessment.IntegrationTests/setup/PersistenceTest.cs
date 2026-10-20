using ColdRoomAssessment.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.IntegrationTests.Setup;

public abstract class PersistenceTest : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    protected PersistenceTest(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public Task InitializeAsync() => _postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected ColdRoomDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ColdRoomDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        return new ColdRoomDbContext(options);
    }
}
