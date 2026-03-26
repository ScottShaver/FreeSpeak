using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableFileUploads",
                table: "Groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresFileApproval",
                table: "Groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GroupFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    UploaderId = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedById = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeclinedReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DownloadCount = table.Column<int>(type: "integer", nullable: false),
                    VirusScanCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    VirusScanPassed = table.Column<bool>(type: "boolean", nullable: true),
                    VirusScanCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupFiles_AspNetUsers_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupFiles_AspNetUsers_UploaderId",
                        column: x => x.UploaderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupFiles_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupFiles_GroupId",
                table: "GroupFiles",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFiles_GroupId_OriginalFileName",
                table: "GroupFiles",
                columns: new[] { "GroupId", "OriginalFileName" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupFiles_GroupId_Status_UploadedAt",
                table: "GroupFiles",
                columns: new[] { "GroupId", "Status", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupFiles_ReviewedById",
                table: "GroupFiles",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFiles_Status",
                table: "GroupFiles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFiles_UploadedAt",
                table: "GroupFiles",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFiles_UploaderId",
                table: "GroupFiles",
                column: "UploaderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupFiles");

            migrationBuilder.DropColumn(
                name: "EnableFileUploads",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "RequiresFileApproval",
                table: "Groups");
        }
    }
}
