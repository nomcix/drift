using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectiveDrift.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestProfiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchemaMetadata",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchemaMetadata", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Builds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    MissionId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LatestVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Builds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Builds_GuestProfiles_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "GuestProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Certifications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Certifications_GuestProfiles_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "GuestProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BuildVersions",
                columns: table => new
                {
                    BuildId = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CanonicalJson = table.Column<string>(type: "TEXT", nullable: false),
                    HasBeenUsed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildVersions", x => new { x.BuildId, x.Version });
                    table.ForeignKey(
                        name: "FK_BuildVersions_Builds_BuildId",
                        column: x => x.BuildId,
                        principalTable: "Builds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    BuildId = table.Column<string>(type: "TEXT", nullable: false),
                    BuildVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    MissionId = table.Column<string>(type: "TEXT", nullable: false),
                    VariantId = table.Column<string>(type: "TEXT", nullable: false),
                    Turn = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StateHash = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderProfileId = table.Column<string>(type: "TEXT", nullable: false),
                    ScriptedPlanJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Runs_BuildVersions_BuildId_BuildVersion",
                        columns: x => new { x.BuildId, x.BuildVersion },
                        principalTable: "BuildVersions",
                        principalColumns: new[] { "BuildId", "Version" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Runs_GuestProfiles_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "GuestProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CertificationRuns",
                columns: table => new
                {
                    CertificationId = table.Column<string>(type: "TEXT", nullable: false),
                    RunId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificationRuns", x => new { x.CertificationId, x.RunId });
                    table.ForeignKey(
                        name: "FK_CertificationRuns_Certifications_CertificationId",
                        column: x => x.CertificationId,
                        principalTable: "Certifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificationRuns_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DecisionRecords",
                columns: table => new
                {
                    RunId = table.Column<string>(type: "TEXT", nullable: false),
                    Turn = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentId = table.Column<string>(type: "TEXT", nullable: false),
                    OperationId = table.Column<string>(type: "TEXT", nullable: false),
                    ActionId = table.Column<string>(type: "TEXT", nullable: false),
                    DecisionJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionRecords", x => new { x.RunId, x.Turn, x.AgentId });
                    table.ForeignKey(
                        name: "FK_DecisionRecords_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DomainEvents",
                columns: table => new
                {
                    RunId = table.Column<string>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Turn = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    EventJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainEvents", x => new { x.RunId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_DomainEvents_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RunSnapshots",
                columns: table => new
                {
                    RunId = table.Column<string>(type: "TEXT", nullable: false),
                    Turn = table.Column<int>(type: "INTEGER", nullable: false),
                    StateJson = table.Column<byte[]>(type: "BLOB", nullable: false),
                    StateHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSnapshots", x => new { x.RunId, x.Turn });
                    table.ForeignKey(
                        name: "FK_RunSnapshots_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TurnOperations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    RunId = table.Column<string>(type: "TEXT", nullable: false),
                    Turn = table.Column<int>(type: "INTEGER", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaseToken = table.Column<string>(type: "TEXT", nullable: true),
                    LeaseExpiresAtUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    HeartbeatUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnOperations_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsageLedger",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    RunId = table.Column<string>(type: "TEXT", nullable: false),
                    OperationId = table.Column<string>(type: "TEXT", nullable: false),
                    ReservedInputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    ReservedOutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    ReservedCostMicros = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualInputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualOutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualCostMicros = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageLedger_GuestProfiles_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "GuestProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsageLedger_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsageLedger_TurnOperations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "TurnOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "SchemaMetadata",
                columns: new[] { "Key", "Value" },
                values: new object[] { "schema-version", "1" });

            migrationBuilder.CreateIndex(
                name: "IX_Builds_OwnerId_CreatedAt",
                table: "Builds",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificationRuns_RunId",
                table: "CertificationRuns",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_Certifications_OwnerId",
                table: "Certifications",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Runs_BuildId_BuildVersion",
                table: "Runs",
                columns: new[] { "BuildId", "BuildVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_OwnerId_CreatedAt",
                table: "Runs",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TurnOperations_RunId",
                table: "TurnOperations",
                column: "RunId",
                unique: true,
                filter: "Status IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_TurnOperations_RunId_IdempotencyKey",
                table: "TurnOperations",
                columns: new[] { "RunId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedger_OperationId",
                table: "UsageLedger",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedger_OwnerId",
                table: "UsageLedger",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedger_RunId",
                table: "UsageLedger",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificationRuns");

            migrationBuilder.DropTable(
                name: "DecisionRecords");

            migrationBuilder.DropTable(
                name: "DomainEvents");

            migrationBuilder.DropTable(
                name: "RunSnapshots");

            migrationBuilder.DropTable(
                name: "SchemaMetadata");

            migrationBuilder.DropTable(
                name: "UsageLedger");

            migrationBuilder.DropTable(
                name: "Certifications");

            migrationBuilder.DropTable(
                name: "TurnOperations");

            migrationBuilder.DropTable(
                name: "Runs");

            migrationBuilder.DropTable(
                name: "BuildVersions");

            migrationBuilder.DropTable(
                name: "Builds");

            migrationBuilder.DropTable(
                name: "GuestProfiles");
        }
    }
}
