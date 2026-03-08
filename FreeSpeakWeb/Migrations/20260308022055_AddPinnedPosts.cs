using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddPinnedPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PinnedPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    PinnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PinnedPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PinnedPosts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PinnedPosts_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PinnedPosts_PinnedAt",
                table: "PinnedPosts",
                column: "PinnedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedPosts_PostId",
                table: "PinnedPosts",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedPosts_UserId",
                table: "PinnedPosts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedPosts_UserId_PostId",
                table: "PinnedPosts",
                columns: new[] { "UserId", "PostId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PinnedPosts");
        }
    }
}
