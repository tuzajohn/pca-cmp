using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PCA.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FlowId",
                table: "ApprovalSteps",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalFlows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EntityType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentStepOrder = table.Column<int>(type: "int", nullable: false),
                    InitiatedById = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InitiatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReturnComment = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReturnedById = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalFlows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalFlows_ApprovalTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ApprovalTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalFlows_AspNetUsers_InitiatedById",
                        column: x => x.InitiatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalFlows_AspNetUsers_ReturnedById",
                        column: x => x.ReturnedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_FlowId",
                table: "ApprovalSteps",
                column: "FlowId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalFlows_EntityType_EntityId",
                table: "ApprovalFlows",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalFlows_InitiatedById",
                table: "ApprovalFlows",
                column: "InitiatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalFlows_ReturnedById",
                table: "ApprovalFlows",
                column: "ReturnedById");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalFlows_TemplateId",
                table: "ApprovalFlows",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalSteps_ApprovalFlows_FlowId",
                table: "ApprovalSteps",
                column: "FlowId",
                principalTable: "ApprovalFlows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalSteps_ApprovalFlows_FlowId",
                table: "ApprovalSteps");

            migrationBuilder.DropTable(
                name: "ApprovalFlows");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalSteps_FlowId",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "FlowId",
                table: "ApprovalSteps");
        }
    }
}
