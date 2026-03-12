using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupPostTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupPostId",
                table: "Likes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupPostId",
                table: "Comments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GroupBannedMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    BannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupBannedMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupBannedMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupBannedMembers_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    AuthorId = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LikeCount = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    ShareCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupPosts_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupPosts_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupPostImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupPostImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupPostImages_GroupPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "GroupPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PinnedGroupPosts",
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
                    table.PrimaryKey("PK_PinnedGroupPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PinnedGroupPosts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PinnedGroupPosts_GroupPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "GroupPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Likes_GroupPostId",
                table: "Likes",
                column: "GroupPostId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_GroupPostId",
                table: "Comments",
                column: "GroupPostId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupBannedMembers_BannedAt",
                table: "GroupBannedMembers",
                column: "BannedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GroupBannedMembers_GroupId",
                table: "GroupBannedMembers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupBannedMembers_GroupId_UserId",
                table: "GroupBannedMembers",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupBannedMembers_UserId",
                table: "GroupBannedMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostImages_PostId",
                table: "GroupPostImages",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostImages_PostId_DisplayOrder",
                table: "GroupPostImages",
                columns: new[] { "PostId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupPosts_AuthorId",
                table: "GroupPosts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPosts_CreatedAt",
                table: "GroupPosts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPosts_GroupId",
                table: "GroupPosts",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPosts_GroupId_CreatedAt",
                table: "GroupPosts",
                columns: new[] { "GroupId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PinnedGroupPosts_PinnedAt",
                table: "PinnedGroupPosts",
                column: "PinnedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedGroupPosts_PostId",
                table: "PinnedGroupPosts",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedGroupPosts_UserId",
                table: "PinnedGroupPosts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedGroupPosts_UserId_PostId",
                table: "PinnedGroupPosts",
                columns: new[] { "UserId", "PostId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_GroupPosts_GroupPostId",
                table: "Comments",
                column: "GroupPostId",
                principalTable: "GroupPosts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Likes_GroupPosts_GroupPostId",
                table: "Likes",
                column: "GroupPostId",
                principalTable: "GroupPosts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_GroupPosts_GroupPostId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Likes_GroupPosts_GroupPostId",
                table: "Likes");

            migrationBuilder.DropTable(
                name: "GroupBannedMembers");

            migrationBuilder.DropTable(
                name: "GroupPostImages");

            migrationBuilder.DropTable(
                name: "PinnedGroupPosts");

            migrationBuilder.DropTable(
                name: "GroupPosts");

            migrationBuilder.DropIndex(
                name: "IX_Likes_GroupPostId",
                table: "Likes");

            migrationBuilder.DropIndex(
                name: "IX_Comments_GroupPostId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "GroupPostId",
                table: "Likes");

            migrationBuilder.DropColumn(
                name: "GroupPostId",
                table: "Comments");
        }
    }
}
