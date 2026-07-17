using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NundoTv_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddEpgChannelMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EpgChannelMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EpgChannelId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpgChannelMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EpgChannelMappings_ChannelId",
                table: "EpgChannelMappings",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_EpgChannelMappings_EpgChannelId",
                table: "EpgChannelMappings",
                column: "EpgChannelId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EpgChannelMappings");
        }
    }
}
