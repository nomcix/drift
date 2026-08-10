using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectiveDrift.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P8AiRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderDecisionCheckpoints",
                columns: table => new
                {
                    OperationId = table.Column<string>(type: "TEXT", nullable: false),
                    AgentId = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderProfileId = table.Column<string>(type: "TEXT", nullable: false),
                    PreDecisionStateHash = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false),
                    ContextJson = table.Column<string>(type: "TEXT", nullable: false),
                    PromptTemplateHash = table.Column<string>(type: "TEXT", nullable: false),
                    DiagnosticCode = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    CostMicros = table.Column<int>(type: "INTEGER", nullable: false),
                    LatencyMilliseconds = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderDecisionCheckpoints", x => new { x.OperationId, x.AgentId });
                    table.ForeignKey(
                        name: "FK_ProviderDecisionCheckpoints_TurnOperations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "TurnOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "SchemaMetadata",
                keyColumn: "Key",
                keyValue: "schema-version",
                column: "Value",
                value: "2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderDecisionCheckpoints");

            migrationBuilder.UpdateData(
                table: "SchemaMetadata",
                keyColumn: "Key",
                keyValue: "schema-version",
                column: "Value",
                value: "1");
        }
    }
}
