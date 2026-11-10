// ------------------------------------------------------------------------------------------------
// <copyright file="20261111103425_AddRepositoryFlags.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepositoryFlags",
                columns: table => new
                {
                    RepositoryId = table.Column<long>(type: "bigint", nullable: false),
                    Flag = table.Column<string>(type: "character varying(446)", maxLength: 446, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryFlags", x => new { x.RepositoryId, x.Flag });
                    table.ForeignKey(
                        name: "FK_RepositoryFlags_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Written by hand: a database that already received uploads knows flags. They are taken from the
            // sessions, the uploaded and the carried-forward ones, so that a repository keeps the flag statuses of
            // the flags it received before this migration. CommitFlagTotals is not the source: it also holds the
            // flags that are only declared in the configuration, which a repository does not know. A flag is
            // known since the first finalization that carried it.
            migrationBuilder.Sql(
                """
                INSERT INTO "RepositoryFlags" ("OrganizationId", "RepositoryId", "Flag", "CreatedAt")
                SELECT c."OrganizationId", c."RepositoryId", f."Flag", MIN(COALESCE(c."CompletedAt", c."CreatedAt"))
                FROM "CommitSessions" s
                INNER JOIN "Commits" c ON c."Id" = s."CommitId"
                CROSS JOIN LATERAL jsonb_array_elements_text(s."Flags"::jsonb) AS f("Flag")
                GROUP BY c."OrganizationId", c."RepositoryId", f."Flag";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryFlags");
        }
    }
}
