using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexesForPostQueryPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId_AudienceType_CreatedAt",
                table: "Posts",
                columns: new[] { "AuthorId", "AudienceType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_Status_AddresseeId",
                table: "Friendships",
                columns: new[] { "Status", "AddresseeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_Status_RequesterId",
                table: "Friendships",
                columns: new[] { "Status", "RequesterId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_AuthorId_AudienceType_CreatedAt",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Friendships_Status_AddresseeId",
                table: "Friendships");

            migrationBuilder.DropIndex(
                name: "IX_Friendships_Status_RequesterId",
                table: "Friendships");
        }
    }
}
