using HealthingHand.Data.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using HealthingHand.Data.Entries;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthingHand.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260401203000_AddExerciseActivityType")]
    public partial class AddExerciseActivityType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityType",
                table: "ExerciseEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: nameof(ExerciseActivityType.GeneralWeightLifting));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityType",
                table: "ExerciseEntries");
        }
    }
}
