using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthingHand.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutIntensityAndHeartRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AverageHeartRate",
                table: "WorkoutEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte>(
                name: "SelfReportedIntensity",
                table: "WorkoutEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageHeartRate",
                table: "WorkoutEntries");

            migrationBuilder.DropColumn(
                name: "SelfReportedIntensity",
                table: "WorkoutEntries");
        }
    }
}
