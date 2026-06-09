using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NundoTv_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialIptv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockedChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IptvId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AltNames = table.Column<List<string>>(type: "jsonb", nullable: false),
                    Network = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Owners = table.Column<List<string>>(type: "jsonb", nullable: false),
                    Country = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Categories = table.Column<List<string>>(type: "jsonb", nullable: false),
                    IsNsfw = table.Column<bool>(type: "boolean", nullable: false),
                    Launched = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Closed = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ReplacedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Website = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StreamUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Logo = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    BlockReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BlockRef = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    BlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlockedKeywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Keyword = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedKeywords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IptvId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AltNames = table.Column<List<string>>(type: "jsonb", nullable: false),
                    Network = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Owners = table.Column<List<string>>(type: "jsonb", nullable: false),
                    Country = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Categories = table.Column<List<string>>(type: "jsonb", nullable: false),
                    IsNsfw = table.Column<bool>(type: "boolean", nullable: false),
                    Launched = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Closed = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ReplacedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Website = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StreamUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Logo = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "text", nullable: false),
                    ChannelName = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WatchDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    ConsentGiven = table.Column<bool>(type: "boolean", nullable: false),
                    DeviceType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryEvents", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "BlockedKeywords",
                columns: new[] { "Id", "Keyword" },
                values: new object[,]
                {
                    { 1, "xxx" },
                    { 2, "porn" },
                    { 3, "adult" },
                    { 4, "sex" },
                    { 5, "18+" },
                    { 6, "nsfw" },
                    { 7, "onlyfans" },
                    { 8, "hentai" },
                    { 9, "casino" },
                    { 10, "gambling" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockedChannels_IptvId",
                table: "BlockedChannels",
                column: "IptvId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Channels_IptvId",
                table: "Channels",
                column: "IptvId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedChannels");

            migrationBuilder.DropTable(
                name: "BlockedKeywords");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "TelemetryEvents");
        }
    }
}
