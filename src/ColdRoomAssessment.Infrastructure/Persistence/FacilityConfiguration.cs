using ColdRoomAssessment.Application;
using ColdRoomAssessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColdRoomAssessment.Infrastructure.Persistence;

internal sealed class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.ToTable("Facilities");

        builder.HasKey(facility => facility.Id);
        builder.Property(facility => facility.Id).ValueGeneratedNever();
        builder.Property(facility => facility.Name)
            .IsRequired()
            .HasMaxLength(TextField.NameMaxLength);
    }
}
