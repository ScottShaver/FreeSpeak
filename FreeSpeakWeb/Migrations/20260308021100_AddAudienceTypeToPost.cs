using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAudienceTypeToPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AudienceType",
                table: "Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudienceType",
                table: "Posts");
        }
    }
}
