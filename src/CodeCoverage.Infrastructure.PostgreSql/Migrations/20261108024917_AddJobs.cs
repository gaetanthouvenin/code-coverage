// ------------------------------------------------------------------------------------------------
// <copyright file="20261108024917_AddJobs.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CodeCoverage.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    LockKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DedupKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    State = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    RunAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FirstQueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_State_RunAfter_Id",
                table: "Jobs",
                columns: new[] { "State", "RunAfter", "Id" });

            // Written by hand: the filtered unique indexes carry the rules of the queue and their filter
            // is engine-specific, so they are kept out of the model (JobRecordConfiguration).
            // One queued job at most per DedupKey: a running twin never blocks a new job.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "UX_Jobs_DedupKey_Queued" ON "Jobs" ("DedupKey")
                WHERE "State" = 'Queued' AND "DedupKey" IS NOT NULL;
                """);

            // One running job at most per LockKey: the jobs of a key are serialized.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "UX_Jobs_LockKey_Running" ON "Jobs" ("LockKey")
                WHERE "State" = 'Running' AND "LockKey" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jobs");
        }
    }
}
