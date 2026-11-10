// ------------------------------------------------------------------------------------------------
// <copyright file="20261111101037_AddComparisons.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddComparisons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comparisons",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommitId = table.Column<long>(type: "bigint", nullable: false),
                    PullNumber = table.Column<int>(type: "int", nullable: false),
                    ForkPointSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ComparedToSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    HeadRevision = table.Column<int>(type: "int", nullable: false),
                    BaseRevision = table.Column<int>(type: "int", nullable: true),
                    HeadCoverage = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: true),
                    BaseCoverage = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: true),
                    PatchLines = table.Column<int>(type: "int", nullable: false),
                    PatchHits = table.Column<int>(type: "int", nullable: false),
                    PatchMisses = table.Column<int>(type: "int", nullable: false),
                    PatchPartials = table.Column<int>(type: "int", nullable: false),
                    IndirectChangeCount = table.Column<int>(type: "int", nullable: false),
                    DiffSource = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DiffIncomplete = table.Column<bool>(type: "bit", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comparisons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comparisons_Commits_CommitId",
                        column: x => x.CommitId,
                        principalTable: "Commits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Comparisons_CommitId_PullNumber",
                table: "Comparisons",
                columns: new[] { "CommitId", "PullNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comparisons");
        }
    }
}
