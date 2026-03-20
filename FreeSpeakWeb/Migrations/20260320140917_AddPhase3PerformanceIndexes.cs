using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_GroupPosts_Status_GroupId_CreatedAt",
                table: "GroupPosts",
                columns: new[] { "Status", "GroupId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostComments_PostId_CreatedAt",
                table: "GroupPostComments",
                columns: new[] { "PostId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostId_CreatedAt",
                table: "Comments",
                columns: new[] { "PostId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupPosts_Status_GroupId_CreatedAt",
                table: "GroupPosts");

            migrationBuilder.DropIndex(
                name: "IX_GroupPostComments_PostId_CreatedAt",
                table: "GroupPostComments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PostId_CreatedAt",
                table: "Comments");
        }
    }
}
