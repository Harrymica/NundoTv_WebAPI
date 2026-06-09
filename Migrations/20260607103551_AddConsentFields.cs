using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NundoTv_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Interest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    HasConsentedToAds = table.Column<bool>(type: "boolean", nullable: false),
                    HasConsentedToDataSharing = table.Column<bool>(type: "boolean", nullable: false),
                    IsPremiumAdFree = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserInterest",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterestId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInterest", x => new { x.UserId, x.InterestId });
                    table.ForeignKey(
                        name: "FK_UserInterest_Interest_InterestId",
                        column: x => x.InterestId,
                        principalTable: "Interest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserInterest_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Interest",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("05d9b552-639a-4d78-a261-d3aa6675a616"), "News" },
                    { new Guid("377b83c0-d307-4750-bdc7-4460fd7998da"), "Kids" },
                    { new Guid("50846e37-f81f-4b54-b3fa-9c99bf734657"), "Lifestyle" },
                    { new Guid("52a6dd79-bb0a-4224-a8c8-83bc09f1281e"), "Sports" },
                    { new Guid("5507972f-9496-4030-963e-6bd4a05fece6"), "Food" },
                    { new Guid("556cca01-d02f-488d-88a3-8fd4d09f767f"), "Fashion" },
                    { new Guid("684192c0-3b2f-49a3-a893-6b80df12e45e"), "Movies" },
                    { new Guid("863dd752-2494-4b8d-a585-76553b2d84ba"), "Discoveries" },
                    { new Guid("94515ebf-d900-4114-b06e-449147b4cf66"), "Cartoon" },
                    { new Guid("cbbb7dd5-e4bc-4817-ad64-00a28041ecaa"), "Science" },
                    { new Guid("dd4070c2-aa01-48ca-9e1e-ca8b3cf950d5"), "Reality Show" },
                    { new Guid("e198424a-a924-4ac6-b420-7eba685908d6"), "Anime" },
                    { new Guid("e2b9ea42-9240-45cf-a04a-012f770f4326"), "Entertainment" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserInterest_InterestId",
                table: "UserInterest",
                column: "InterestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserInterest");

            migrationBuilder.DropTable(
                name: "Interest");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
