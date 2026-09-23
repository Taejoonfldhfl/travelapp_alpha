using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelApp.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleHotelCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHotelCheckIn",
                table: "Schedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHotelCheckIn",
                table: "Schedules");
        }
    }
}
