using ColdRoomAssessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColdRoomAssessment.Infrastructure.Persistence;

internal sealed class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
{
    public void Configure(EntityTypeBuilder<Measurement> builder)
    {
        builder.ToTable("Measurements");

        builder.HasKey(measurement => measurement.Id);
        builder.Property(measurement => measurement.Id).ValueGeneratedNever();
        builder.Property(measurement => measurement.MeasuredAt)
            .HasColumnType("timestamp without time zone")
            .IsRequired();
        builder.Property(measurement => measurement.Value)
            .HasColumnType("double precision")
            .IsRequired();
        builder.Property(measurement => measurement.Kind)
            .HasConversion<string>()
            .IsRequired();
        builder.Property(measurement => measurement.Unit)
            .IsRequired();

        builder.HasOne(measurement => measurement.DataImport)
            .WithMany(import => import.Measurements)
            .HasForeignKey(measurement => measurement.DataImportId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
