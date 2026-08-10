using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DirectiveDrift.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P9Mastery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Assisted",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CertificationId",
                table: "Runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VariantDisclosureJson",
                table: "Runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildId",
                table: "Certifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BuildVersion",
                table: "Certifications",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CertificationVersion",
                table: "Certifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "Certifications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Certifications",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "MissionContentVersion",
                table: "Certifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderProfileId",
                table: "Certifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RulesVersion",
                table: "Certifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ScoreVersion",
                table: "Certifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Slot",
                table: "CertificationRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "SchemaMetadata",
                keyColumn: "Key",
                keyValue: "schema-version",
                column: "Value",
                value: "3");

            migrationBuilder.CreateIndex(
                name: "IX_Certifications_BuildId_BuildVersion",
                table: "Certifications",
                columns: new[] { "BuildId", "BuildVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificationRuns_CertificationId_Slot",
                table: "CertificationRuns",
                columns: new[] { "CertificationId", "Slot" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Certifications_BuildVersions_BuildId_BuildVersion",
                table: "Certifications",
                columns: new[] { "BuildId", "BuildVersion" },
                principalTable: "BuildVersions",
                principalColumns: new[] { "BuildId", "Version" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Certifications_BuildVersions_BuildId_BuildVersion",
                table: "Certifications");

            migrationBuilder.DropIndex(
                name: "IX_Certifications_BuildId_BuildVersion",
                table: "Certifications");

            migrationBuilder.DropIndex(
                name: "IX_CertificationRuns_CertificationId_Slot",
                table: "CertificationRuns");

            migrationBuilder.DropColumn(
                name: "Assisted",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "CertificationId",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "VariantDisclosureJson",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "BuildId",
                table: "Certifications");

            migrationBuilder.DropColumn(
                name: "BuildVersion",
                table: "Certifications");

            migrationBuilder.DropColumn(
                name: "CertificationVersion",
                table: "Certifications");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Certifications");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Certifications");

            migrationBuilder.DropColumn(
                name: "MissionContentVersion",
                table: "Certifications");

            migrationBuilder.DropColumn(
                name: "ProviderProfileId",
                table: "Certifications");

            migrationBuilder.DropColumn(
                name: "RulesVersion",
                table: "Certifications");

            migrationBuilder.DropColumn(
                name: "ScoreVersion",
                table: "Certifications");

            migrationBuilder.DropColumn(
                name: "Slot",
                table: "CertificationRuns");

            migrationBuilder.UpdateData(
                table: "SchemaMetadata",
                keyColumn: "Key",
                keyValue: "schema-version",
                column: "Value",
                value: "2");
        }
    }
}
