using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupPostCommentAndLikeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_GroupPosts_GroupPostId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Likes_GroupPosts_GroupPostId",
                table: "Likes");

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

            migrationBuilder.CreateTable(
                name: "GroupPostComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    AuthorId = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ParentCommentId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupPostComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupPostComments_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupPostComments_GroupPostComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "GroupPostComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupPostComments_GroupPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "GroupPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupPostLikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupPostLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupPostLikes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupPostLikes_GroupPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "GroupPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupPostCommentLikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommentId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupPostCommentLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupPostCommentLikes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupPostCommentLikes_GroupPostComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "GroupPostComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostCommentLikes_CommentId",
                table: "GroupPostCommentLikes",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostCommentLikes_CommentId_UserId",
                table: "GroupPostCommentLikes",
                columns: new[] { "CommentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostCommentLikes_UserId",
                table: "GroupPostCommentLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostComments_AuthorId",
                table: "GroupPostComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostComments_CreatedAt",
                table: "GroupPostComments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostComments_ParentCommentId",
                table: "GroupPostComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostComments_PostId",
                table: "GroupPostComments",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostLikes_PostId",
                table: "GroupPostLikes",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostLikes_PostId_UserId",
                table: "GroupPostLikes",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupPostLikes_UserId",
                table: "GroupPostLikes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupPostCommentLikes");

            migrationBuilder.DropTable(
                name: "GroupPostLikes");

            migrationBuilder.DropTable(
                name: "GroupPostComments");

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

            migrationBuilder.CreateIndex(
                name: "IX_Likes_GroupPostId",
                table: "Likes",
                column: "GroupPostId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_GroupPostId",
                table: "Comments",
                column: "GroupPostId");

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
    }
}
