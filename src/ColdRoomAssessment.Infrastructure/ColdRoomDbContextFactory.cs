using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ColdRoomAssessment.Infrastructure;

public sealed class ColdRoomDbContextFactory : IDesignTimeDbContextFactory<ColdRoomDbContext>
{
    public const string LocalConnectionString =
        "Host=localhost;Port=5432;Database=coldroom;Username=coldroom;Password=coldroom";

    public ColdRoomDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ColdRoomDbContext>()
            .UseNpgsql(LocalConnectionString)
            .Options;

        return new ColdRoomDbContext(options);
    }
}
