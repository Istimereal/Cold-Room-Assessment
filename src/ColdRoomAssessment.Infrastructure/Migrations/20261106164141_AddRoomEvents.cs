using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdRoomAssessment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    From = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    To = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomEvents", x => x.Id);
                    table.CheckConstraint("CK_RoomEvents_From_Before_Or_Equal_To", "\"From\" <= \"To\"");
                    table.ForeignKey(
                        name: "FK_RoomEvents_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomEvents_FacilityId_From",
                table: "RoomEvents",
                columns: new[] { "FacilityId", "From" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomEvents");
        }
    }
}
