using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NundoTv_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicChannelResolutionToStreamLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChannelResolverKey",
                table: "StreamLinks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresChannelSearch",
                table: "StreamLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StreamType",
                table: "StreamLinks",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChannelResolverKey",
                table: "StreamLinks");

            migrationBuilder.DropColumn(
                name: "RequiresChannelSearch",
                table: "StreamLinks");

            migrationBuilder.DropColumn(
                name: "StreamType",
                table: "StreamLinks");
        }
    }
}
