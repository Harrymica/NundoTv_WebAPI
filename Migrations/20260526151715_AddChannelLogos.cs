using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NundoTv_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelLogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockedChannelLogos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BlockedChannelId = table.Column<int>(type: "integer", nullable: false),
                    IptvChannelId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    Format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    InUse = table.Column<bool>(type: "boolean", nullable: false),
                    Feed = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedChannelLogos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlockedChannelLogos_BlockedChannels_BlockedChannelId",
                        column: x => x.BlockedChannelId,
                        principalTable: "BlockedChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelLogos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelId = table.Column<int>(type: "integer", nullable: false),
                    IptvChannelId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    Format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    InUse = table.Column<bool>(type: "boolean", nullable: false),
                    Feed = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelLogos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelLogos_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockedChannelLogos_BlockedChannelId",
                table: "BlockedChannelLogos",
                column: "BlockedChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelLogos_ChannelId",
                table: "ChannelLogos",
                column: "ChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedChannelLogos");

            migrationBuilder.DropTable(
                name: "ChannelLogos");
        }
    }
}
