using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeSpeakWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogTableWithPartitioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the partitioned table using raw SQL because EF Core doesn't natively support partitioned tables
            migrationBuilder.Sql(@"
                CREATE TABLE ""AuditLogs"" (
                    ""Id"" bigint NOT NULL,
                    ""ActionStamp"" timestamp with time zone NOT NULL,
                    ""UserId"" character varying(450) NOT NULL,
                    ""ActionCategory"" character varying(100) NOT NULL,
                    ""ActionDetails"" TEXT NOT NULL,
                    CONSTRAINT ""PK_AuditLogs"" PRIMARY KEY (""Id"", ""ActionStamp"")
                ) PARTITION BY RANGE (""ActionStamp"");
            ");

            // Create indexes on the partitioned table
            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActionCategory",
                table: "AuditLogs",
                column: "ActionCategory");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActionStamp",
                table: "AuditLogs",
                column: "ActionStamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_ActionStamp",
                table: "AuditLogs",
                columns: new[] { "UserId", "ActionStamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
