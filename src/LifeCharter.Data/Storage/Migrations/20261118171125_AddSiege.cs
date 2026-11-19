using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeCharter.Data.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddSiege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rating",
                table: "log_entries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "habit_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    automatically = table.Column<int>(type: "INTEGER", nullable: false),
                    without_remembering = table.Column<int>(type: "INTEGER", nullable: false),
                    without_thinking = table.Column<int>(type: "INTEGER", nullable: false),
                    before_realizing = table.Column<int>(type: "INTEGER", nullable: false),
                    device_id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_habit_checks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "siege_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    difficulty = table.Column<string>(type: "TEXT", nullable: false),
                    damage_multiplier = table.Column<double>(type: "REAL", nullable: false),
                    attacks_per_day = table.Column<int>(type: "INTEGER", nullable: false),
                    rest_days = table.Column<string>(type: "TEXT", nullable: false),
                    ward_cap = table.Column<int>(type: "INTEGER", nullable: false),
                    zero_integrity = table.Column<string>(type: "TEXT", nullable: false),
                    cleanse_sessions = table.Column<int>(type: "INTEGER", nullable: false),
                    desired_retention = table.Column<double>(type: "REAL", nullable: false),
                    start_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    device_id = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_siege_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vacations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    start_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    end_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    device_id = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ward_uses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    device_id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ward_uses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_habit_checks_node_id",
                table: "habit_checks",
                column: "node_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "habit_checks");

            migrationBuilder.DropTable(
                name: "siege_settings");

            migrationBuilder.DropTable(
                name: "vacations");

            migrationBuilder.DropTable(
                name: "ward_uses");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "log_entries");
        }
    }
}
