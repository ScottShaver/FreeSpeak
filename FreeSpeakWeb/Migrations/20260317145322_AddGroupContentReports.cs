using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupContentReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroupContentReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    PostId = table.Column<int>(type: "integer", nullable: true),
                    CommentId = table.Column<int>(type: "integer", nullable: true),
                    ReporterId = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    ViolatedRuleId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewerId = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupContentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupContentReports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupContentReports_AspNetUsers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupContentReports_GroupPostComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "GroupPostComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GroupContentReports_GroupPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "GroupPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GroupContentReports_GroupRules_ViolatedRuleId",
                        column: x => x.ViolatedRuleId,
                        principalTable: "GroupRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GroupContentReports_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupContentReports_CommentId",
                table: "GroupContentReports",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContentReports_CreatedAt",
                table: "GroupContentReports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContentReports_GroupId",
                table: "GroupContentReports",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContentReports_GroupId_Status",
                table: "GroupContentReports",
                columns: new[] { "GroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupContentReports_PostId",
                table: "GroupContentReports",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContentReports_ReporterId",
                table: "GroupContentReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContentReports_ReviewerId",
                table: "GroupContentReports",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContentReports_Status",
                table: "GroupContentReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GroupContentReports_ViolatedRuleId",
                table: "GroupContentReports",
                column: "ViolatedRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupContentReports");
        }
    }
}
