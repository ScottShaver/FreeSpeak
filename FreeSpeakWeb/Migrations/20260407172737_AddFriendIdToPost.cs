using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendIdToPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FriendId",
                table: "Posts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Posts_FriendId",
                table: "Posts",
                column: "FriendId");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_AspNetUsers_FriendId",
                table: "Posts",
                column: "FriendId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_AspNetUsers_FriendId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_FriendId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "FriendId",
                table: "Posts");
        }
    }
}
