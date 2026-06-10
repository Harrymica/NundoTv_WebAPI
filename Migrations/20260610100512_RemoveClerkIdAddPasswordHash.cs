using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NundoTv_WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClerkIdAddPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("TRUNCATE TABLE \"UserInterests\" CASCADE;");
            migrationBuilder.Sql("TRUNCATE TABLE \"Users\" CASCADE;");

            migrationBuilder.DropIndex(
                name: "IX_Users_ClerkId",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "ClerkId",
                table: "Users",
                newName: "PasswordHash");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "Users",
                newName: "ClerkId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ClerkId",
                table: "Users",
                column: "ClerkId",
                unique: true);
        }
    }
}
