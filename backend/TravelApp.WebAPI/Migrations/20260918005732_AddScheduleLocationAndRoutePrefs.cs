using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelApp.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleLocationAndRoutePrefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEssential",
                table: "Schedules",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Schedules",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Schedules",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Schedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEssential",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Schedules");
        }
    }
}
