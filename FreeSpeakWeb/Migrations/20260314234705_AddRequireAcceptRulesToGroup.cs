using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddRequireAcceptRulesToGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireAcceptRules",
                table: "Groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireAcceptRules",
                table: "Groups");
        }
    }
}
