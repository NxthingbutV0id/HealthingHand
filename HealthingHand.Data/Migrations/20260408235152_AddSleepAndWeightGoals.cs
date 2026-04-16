using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthingHand.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSleepAndWeightGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SleepGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DesiredWakeTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    PreferredSleepHours = table.Column<float>(type: "REAL", nullable: false),
                    BestRecommendedBedtime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SleepGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SleepGoals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeightGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrentWeightKg = table.Column<float>(type: "REAL", nullable: false),
                    GoalWeightKg = table.Column<float>(type: "REAL", nullable: false),
                    GoalType = table.Column<string>(type: "TEXT", nullable: false),
                    PacePreference = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeightGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeightGoals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SleepGoals_UserId",
                table: "SleepGoals",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeightGoals_UserId",
                table: "WeightGoals",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SleepGoals");

            migrationBuilder.DropTable(
                name: "WeightGoals");
        }
    }
}
