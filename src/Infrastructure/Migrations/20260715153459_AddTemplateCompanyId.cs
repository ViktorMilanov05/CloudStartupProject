using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateCompanyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Templates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill existing templates with the company of their creator so the
            // foreign key below is satisfied for pre-existing rows.
            migrationBuilder.Sql(@"
                UPDATE t
                SET t.CompanyId = u.CompanyId
                FROM Templates t
                INNER JOIN AspNetUsers u ON u.Id = t.CreatedById
                WHERE u.CompanyId IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_CompanyId",
                table: "Templates",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Companies_CompanyId",
                table: "Templates",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Companies_CompanyId",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Templates_CompanyId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Templates");
        }
    }
}
