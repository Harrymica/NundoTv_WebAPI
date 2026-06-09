using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NundoTv_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddClerkAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserInterest_Interest_InterestId",
                table: "UserInterest");

            migrationBuilder.DropForeignKey(
                name: "FK_UserInterest_Users_UserId",
                table: "UserInterest");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserInterest",
                table: "UserInterest");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Interest",
                table: "Interest");

            migrationBuilder.RenameTable(
                name: "UserInterest",
                newName: "UserInterests");

            migrationBuilder.RenameTable(
                name: "Interest",
                newName: "Interests");

            migrationBuilder.RenameIndex(
                name: "IX_UserInterest_InterestId",
                table: "UserInterests",
                newName: "IX_UserInterests_InterestId");

            migrationBuilder.AddColumn<string>(
                name: "ClerkId",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "HasCompletedOnboarding",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageUrl",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserInterests",
                table: "UserInterests",
                columns: new[] { "UserId", "InterestId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Interests",
                table: "Interests",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ClerkId",
                table: "Users",
                column: "ClerkId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserInterests_Interests_InterestId",
                table: "UserInterests",
                column: "InterestId",
                principalTable: "Interests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserInterests_Users_UserId",
                table: "UserInterests",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserInterests_Interests_InterestId",
                table: "UserInterests");

            migrationBuilder.DropForeignKey(
                name: "FK_UserInterests_Users_UserId",
                table: "UserInterests");

            migrationBuilder.DropIndex(
                name: "IX_Users_ClerkId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserInterests",
                table: "UserInterests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Interests",
                table: "Interests");

            migrationBuilder.AddForeignKey(
                name: "FK_UserInterest_Interest_InterestId",
                table: "UserInterest",
                column: "InterestId",
                principalTable: "Interest",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserInterest_Users_UserId",
                table: "UserInterest",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
