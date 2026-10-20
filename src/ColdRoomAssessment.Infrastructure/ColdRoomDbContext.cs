using ColdRoomAssessment.Domain;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.Infrastructure;

public sealed class ColdRoomDbContext(DbContextOptions<ColdRoomDbContext> options) : DbContext(options)
{
    public DbSet<Facility> Facilities => Set<Facility>();

    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();

    public DbSet<Equipment> Equipment => Set<Equipment>();

    public DbSet<DataImport> DataImports => Set<DataImport>();

    public DbSet<Measurement> Measurements => Set<Measurement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ColdRoomDbContext).Assembly);
    }
}
