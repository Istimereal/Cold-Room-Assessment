using ColdRoomAssessment.Application;
using ColdRoomAssessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColdRoomAssessment.Infrastructure.Persistence;

internal sealed class EquipmentTypeConfiguration : IEntityTypeConfiguration<EquipmentType>
{
    public void Configure(EntityTypeBuilder<EquipmentType> builder)
    {
        builder.ToTable("EquipmentTypes");

        builder.HasKey(equipmentType => equipmentType.Id);
        builder.Property(equipmentType => equipmentType.Id).ValueGeneratedNever();
        builder.Property(equipmentType => equipmentType.Name)
            .IsRequired()
            .HasMaxLength(TextField.NameMaxLength);

        builder.HasIndex(equipmentType => equipmentType.Name).IsUnique();
    }
}
