using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PCA.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalMode",
                table: "ApprovalTemplates",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "AllMustApprove")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AutoTriggerOn",
                table: "ApprovalTemplates",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ConditionField",
                table: "ApprovalTemplates",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ConditionValue",
                table: "ApprovalTemplates",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ApprovalMode", table: "ApprovalTemplates");
            migrationBuilder.DropColumn(name: "AutoTriggerOn", table: "ApprovalTemplates");
            migrationBuilder.DropColumn(name: "ConditionField", table: "ApprovalTemplates");
            migrationBuilder.DropColumn(name: "ConditionValue", table: "ApprovalTemplates");
        }
    }
}
