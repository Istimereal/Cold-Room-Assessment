using ColdRoomAssessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColdRoomAssessment.Infrastructure.Persistence;

internal sealed class RoomEventConfiguration : IEntityTypeConfiguration<RoomEvent>
{
    public void Configure(EntityTypeBuilder<RoomEvent> builder)
    {
        builder.ToTable("RoomEvents", table =>
            table.HasCheckConstraint("CK_RoomEvents_From_Before_Or_Equal_To", "\"From\" <= \"To\""));

        builder.HasKey(roomEvent => roomEvent.Id);
        builder.Property(roomEvent => roomEvent.Id).ValueGeneratedNever();
        builder.Property(roomEvent => roomEvent.From)
            .HasColumnType("timestamp without time zone")
            .IsRequired();
        builder.Property(roomEvent => roomEvent.To)
            .HasColumnType("timestamp without time zone")
            .IsRequired();
        builder.Property(roomEvent => roomEvent.Type)
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(roomEvent => new { roomEvent.FacilityId, roomEvent.From });

        builder.HasOne(roomEvent => roomEvent.Facility)
            .WithMany(facility => facility.RoomEvents)
            .HasForeignKey(roomEvent => roomEvent.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
