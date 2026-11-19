using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeCharter.Data.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddNodeAliases : Migration
    {
        private static readonly Dictionary<string, string> StarterAliases = new()
        {
            ["Sleep"] = "slept, bed, nap",
            ["Hydration"] = "water, drink",
            ["Nutrition"] = "meal, food, ate",
            ["Mind"] = "meditate, meditation, journal",
            ["Screen time"] = "screen, offline",
            ["Social"] = "friends, meetup, family",
            ["Cardio"] = "run, running, jog, bike, cycling, swim",
            ["Strength"] = "gym, lift, workout, pushups",
            ["Mobility"] = "stretch, stretching, yoga",
            ["Steps"] = "walk, walking",
            ["Sport"] = "football, tennis, climbing",
            ["Languages"] = "language, vocab, vocabulary",
            ["Programming"] = "code, coding",
            ["Reading"] = "read, book",
            ["Courses"] = "course, lesson, class",
            ["Creative"] = "draw, drawing, music, guitar, writing",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "aliases",
                table: "nodes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Give starter nodes created before aliases existed the same aliases as a new
            // starter tree. Both devices run this migration, so the values agree after a sync.
            foreach (var (title, aliases) in StarterAliases)
            {
                migrationBuilder.Sql(
                    $"UPDATE nodes SET aliases = '{aliases}' WHERE title = '{title}' AND aliases = '' AND kind <> 'Root';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aliases",
                table: "nodes");
        }
    }
}
