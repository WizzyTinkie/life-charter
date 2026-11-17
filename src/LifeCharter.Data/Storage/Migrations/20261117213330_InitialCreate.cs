using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeCharter.Data.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    action = table.Column<string>(type: "TEXT", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    device_id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "edges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    from_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    to_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    device_id = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_edges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    metric = table.Column<string>(type: "TEXT", nullable: false),
                    target = table.Column<double>(type: "REAL", nullable: false),
                    unit = table.Column<string>(type: "TEXT", nullable: false),
                    window = table.Column<string>(type: "TEXT", nullable: false),
                    periods = table.Column<int>(type: "INTEGER", nullable: false),
                    consecutive = table.Column<bool>(type: "INTEGER", nullable: false),
                    start_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    deadline = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    device_id = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "log_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    goal_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    quest_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    amount = table.Column<double>(type: "REAL", nullable: false),
                    unit = table.Column<string>(type: "TEXT", nullable: false),
                    note = table.Column<string>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    device_id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    attribute = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    x = table.Column<double>(type: "REAL", nullable: false),
                    y = table.Column<double>(type: "REAL", nullable: false),
                    retention_model = table.Column<string>(type: "TEXT", nullable: false),
                    complexity = table.Column<string>(type: "TEXT", nullable: false),
                    xp_reward = table.Column<int>(type: "INTEGER", nullable: false),
                    device_id = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    goal_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    schedule = table.Column<string>(type: "TEXT", nullable: false),
                    times_per_week = table.Column<int>(type: "INTEGER", nullable: false),
                    amount = table.Column<double>(type: "REAL", nullable: false),
                    unit = table.Column<string>(type: "TEXT", nullable: false),
                    cue = table.Column<string>(type: "TEXT", nullable: false),
                    aliases = table.Column<string>(type: "TEXT", nullable: false),
                    device_id = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_allocations_node_id",
                table: "allocations",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "IX_edges_from_node_id",
                table: "edges",
                column: "from_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_edges_to_node_id",
                table: "edges",
                column: "to_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_goals_node_id",
                table: "goals",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "IX_log_entries_node_id",
                table: "log_entries",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "IX_quests_node_id",
                table: "quests",
                column: "node_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allocations");

            migrationBuilder.DropTable(
                name: "edges");

            migrationBuilder.DropTable(
                name: "goals");

            migrationBuilder.DropTable(
                name: "log_entries");

            migrationBuilder.DropTable(
                name: "nodes");

            migrationBuilder.DropTable(
                name: "quests");
        }
    }
}
