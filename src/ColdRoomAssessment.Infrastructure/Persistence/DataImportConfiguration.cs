using ColdRoomAssessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColdRoomAssessment.Infrastructure.Persistence;

internal sealed class DataImportConfiguration : IEntityTypeConfiguration<DataImport>
{
    public void Configure(EntityTypeBuilder<DataImport> builder)
    {
        builder.ToTable("DataImports");

        builder.HasKey(import => import.Id);
        builder.Property(import => import.Id).ValueGeneratedNever();
        builder.Property(import => import.OriginalFileName);
        builder.Property(import => import.ImportedAt)
            .HasColumnType("timestamp with time zone");
        builder.Property(import => import.FirstMeasurementAt)
            .HasColumnType("timestamp without time zone");
        builder.Property(import => import.LastMeasurementAt)
            .HasColumnType("timestamp without time zone");
        builder.Property(import => import.ImportedRowCount)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(import => import.InvalidRowCount)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(import => import.Status)
            .HasConversion<string>()
            .HasDefaultValue(ImportStatus.Pending)
            .IsRequired();

        builder.HasOne(import => import.Equipment)
            .WithMany(equipment => equipment.DataImports)
            .HasForeignKey(import => import.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
