using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PCA.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPirFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PirActualDate",
                table: "ChangeRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PirClosureNotes",
                table: "ChangeRequests",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PirCompletedById",
                table: "ChangeRequests",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PirIssuesEncountered",
                table: "ChangeRequests",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PirLessonsLearned",
                table: "ChangeRequests",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PirOutcome",
                table: "ChangeRequests",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "PirRollbackExecuted",
                table: "ChangeRequests",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_PirCompletedById",
                table: "ChangeRequests",
                column: "PirCompletedById");

            migrationBuilder.AddForeignKey(
                name: "FK_ChangeRequests_AspNetUsers_PirCompletedById",
                table: "ChangeRequests",
                column: "PirCompletedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChangeRequests_AspNetUsers_PirCompletedById",
                table: "ChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_ChangeRequests_PirCompletedById",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "PirActualDate",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "PirClosureNotes",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "PirCompletedById",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "PirIssuesEncountered",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "PirLessonsLearned",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "PirOutcome",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "PirRollbackExecuted",
                table: "ChangeRequests");
        }
    }
}
