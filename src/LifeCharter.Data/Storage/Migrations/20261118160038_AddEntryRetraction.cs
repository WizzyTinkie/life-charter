using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeCharter.Data.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryRetraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "retracts_entry_id",
                table: "log_entries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "retracts_entry_id",
                table: "log_entries");
        }
    }
}
