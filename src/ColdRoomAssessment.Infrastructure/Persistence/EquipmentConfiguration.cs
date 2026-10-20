using ColdRoomAssessment.Application;
using ColdRoomAssessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColdRoomAssessment.Infrastructure.Persistence;

internal sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment");

        builder.HasKey(equipment => equipment.Id);
        builder.Property(equipment => equipment.Id).ValueGeneratedNever();
        builder.Property(equipment => equipment.Name)
            .IsRequired()
            .HasMaxLength(TextField.NameMaxLength);
        builder.Property(equipment => equipment.Placement)
            .IsRequired()
            .HasMaxLength(TextField.PlacementMaxLength);

        builder.HasIndex(equipment => new { equipment.FacilityId, equipment.Name }).IsUnique();

        builder.HasOne(equipment => equipment.Facility)
            .WithMany(facility => facility.Equipment)
            .HasForeignKey(equipment => equipment.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(equipment => equipment.EquipmentType)
            .WithMany(equipmentType => equipmentType.Equipment)
            .HasForeignKey(equipment => equipment.EquipmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
