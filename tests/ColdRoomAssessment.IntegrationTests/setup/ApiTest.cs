namespace ColdRoomAssessment.IntegrationTests.Setup;

public abstract class ApiTest : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    protected ApiTest(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.ResetAsync();
        Client = _postgres.ApiFactory.CreateClient();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}
