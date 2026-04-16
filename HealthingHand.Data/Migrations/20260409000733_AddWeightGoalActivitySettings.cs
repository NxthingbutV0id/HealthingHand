using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthingHand.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeightGoalActivitySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExerciseFrequency",
                table: "WeightGoals",
                type: "TEXT",
                nullable: false,
                defaultValue: "Moderate");

            migrationBuilder.AddColumn<string>(
                name: "ExerciseIntensity",
                table: "WeightGoals",
                type: "TEXT",
                nullable: false,
                defaultValue: "Medium");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExerciseFrequency",
                table: "WeightGoals");

            migrationBuilder.DropColumn(
                name: "ExerciseIntensity",
                table: "WeightGoals");
        }
    }
}
