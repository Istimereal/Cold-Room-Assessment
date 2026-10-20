using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ColdRoomAssessment.IntegrationTests.Setup;

public sealed class ColdRoomApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ColdRoomApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ColdRoom"] = _connectionString
            });
        });
    }
}
