// ------------------------------------------------------------------------------------------------
// <copyright file="20261109154445_AddCommitReports.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCommitReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommitFlagTotals",
                columns: table => new
                {
                    CommitId = table.Column<long>(type: "bigint", nullable: false),
                    Flag = table.Column<string>(type: "nvarchar(446)", maxLength: 446, nullable: false),
                    RepositoryId = table.Column<long>(type: "bigint", nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Lines = table.Column<int>(type: "int", nullable: false),
                    Hits = table.Column<int>(type: "int", nullable: false),
                    Misses = table.Column<int>(type: "int", nullable: false),
                    Partials = table.Column<int>(type: "int", nullable: false),
                    Coverage = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: true),
                    IsCarriedForward = table.Column<bool>(type: "bit", nullable: false),
                    CarriedForwardFromSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitFlagTotals", x => new { x.CommitId, x.Flag });
                    table.ForeignKey(
                        name: "FK_CommitFlagTotals_Commits_CommitId",
                        column: x => x.CommitId,
                        principalTable: "Commits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommitReports",
                columns: table => new
                {
                    CommitId = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Codec = table.Column<byte>(type: "tinyint", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    FileIndex = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitReports", x => x.CommitId);
                    table.ForeignKey(
                        name: "FK_CommitReports_Commits_CommitId",
                        column: x => x.CommitId,
                        principalTable: "Commits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommitSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommitId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    UploadId = table.Column<long>(type: "bigint", nullable: true),
                    CarriedForwardFromSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SourceSessionId = table.Column<long>(type: "bigint", nullable: true),
                    DataUploadId = table.Column<long>(type: "bigint", nullable: true),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Flags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(271)", maxLength: 271, nullable: true),
                    CiService = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BuildUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Lines = table.Column<int>(type: "int", nullable: false),
                    Hits = table.Column<int>(type: "int", nullable: false),
                    Misses = table.Column<int>(type: "int", nullable: false),
                    Partials = table.Column<int>(type: "int", nullable: false),
                    Coverage = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: true),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitSessions_Commits_CommitId",
                        column: x => x.CommitId,
                        principalTable: "Commits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UploadCoverages",
                columns: table => new
                {
                    UploadId = table.Column<long>(type: "bigint", nullable: false),
                    Codec = table.Column<byte>(type: "tinyint", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadCoverages", x => x.UploadId);
                    table.ForeignKey(
                        name: "FK_UploadCoverages_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommitFlagTotals_RepositoryId_Branch_Flag_CompletedAt",
                table: "CommitFlagTotals",
                columns: new[] { "RepositoryId", "Branch", "Flag", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommitSessions_CommitId",
                table: "CommitSessions",
                column: "CommitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommitFlagTotals");

            migrationBuilder.DropTable(
                name: "CommitReports");

            migrationBuilder.DropTable(
                name: "CommitSessions");

            migrationBuilder.DropTable(
                name: "UploadCoverages");
        }
    }
}
