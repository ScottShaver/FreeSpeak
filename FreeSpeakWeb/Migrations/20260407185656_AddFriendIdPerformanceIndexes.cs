using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendIdPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_AspNetUsers_FriendId",
                table: "Posts");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId_CreatedAt",
                table: "Posts",
                columns: new[] { "AuthorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId_FriendId_CreatedAt",
                table: "Posts",
                columns: new[] { "AuthorId", "FriendId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_FriendId_CreatedAt",
                table: "Posts",
                columns: new[] { "FriendId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_AspNetUsers_FriendId",
                table: "Posts",
                column: "FriendId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_AspNetUsers_FriendId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_AuthorId_CreatedAt",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_AuthorId_FriendId_CreatedAt",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_FriendId_CreatedAt",
                table: "Posts");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_AspNetUsers_FriendId",
                table: "Posts",
                column: "FriendId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
