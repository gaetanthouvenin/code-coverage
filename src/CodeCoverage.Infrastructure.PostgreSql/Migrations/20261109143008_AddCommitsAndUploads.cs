// ------------------------------------------------------------------------------------------------
// <copyright file="20261109143008_AddCommitsAndUploads.cs" company="Gaëtan Thouvenin">
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
    public partial class AddCommitsAndUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Commits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RepositoryId = table.Column<long>(type: "bigint", nullable: false),
                    Sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PullNumber = table.Column<int>(type: "integer", nullable: true),
                    State = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadSequence = table.Column<int>(type: "integer", nullable: false),
                    Files = table.Column<int>(type: "integer", nullable: false),
                    Lines = table.Column<int>(type: "integer", nullable: false),
                    Hits = table.Column<int>(type: "integer", nullable: false),
                    Misses = table.Column<int>(type: "integer", nullable: false),
                    Partials = table.Column<int>(type: "integer", nullable: false),
                    Branches = table.Column<int>(type: "integer", nullable: false),
                    Methods = table.Column<int>(type: "integer", nullable: false),
                    Coverage = table.Column<decimal>(type: "numeric(9,5)", precision: 9, scale: 5, nullable: true),
                    SessionCount = table.Column<int>(type: "integer", nullable: false),
                    UploadedSessionCount = table.Column<int>(type: "integer", nullable: false),
                    ReportRevision = table.Column<int>(type: "integer", nullable: false),
                    CarryforwardStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConfigSource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastUploadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Commits_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GitCommits",
                columns: table => new
                {
                    RepositoryId = table.Column<long>(type: "bigint", nullable: false),
                    Sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ParentShas = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AuthorLogin = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitCommits", x => new { x.RepositoryId, x.Sha });
                    table.ForeignKey(
                        name: "FK_GitCommits_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Uploads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", unicode: false, maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Flags = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CiService = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BuildId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    JobId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BuildUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RepoRoot = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    UploaderVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TokenId = table.Column<long>(type: "bigint", nullable: true),
                    PrincipalKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReportFileCount = table.Column<int>(type: "integer", nullable: false),
                    ResolvedFileCount = table.Column<int>(type: "integer", nullable: false),
                    UnresolvedFileCount = table.Column<int>(type: "integer", nullable: false),
                    Lines = table.Column<int>(type: "integer", nullable: false),
                    Hits = table.Column<int>(type: "integer", nullable: false),
                    Misses = table.Column<int>(type: "integer", nullable: false),
                    Partials = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    ProcessingLog = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Uploads_Commits_CommitId",
                        column: x => x.CommitId,
                        principalTable: "Commits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_CoveredCommitId",
                table: "Branches",
                column: "CoveredCommitId");

            migrationBuilder.CreateIndex(
                name: "IX_Commits_RepositoryId_Branch_CompletedAt",
                table: "Commits",
                columns: new[] { "RepositoryId", "Branch", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_Commits_RepositoryId_Sha",
                table: "Commits",
                columns: new[] { "RepositoryId", "Sha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_CommitId_State",
                table: "Uploads",
                columns: new[] { "CommitId", "State" });

            migrationBuilder.CreateIndex(
                name: "UX_Uploads_CommitId_IdempotencyKey",
                table: "Uploads",
                columns: new[] { "CommitId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Uploads_PublicId",
                table: "Uploads",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Commits_CoveredCommitId",
                table: "Branches",
                column: "CoveredCommitId",
                principalTable: "Commits",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Commits_CoveredCommitId",
                table: "Branches");

            migrationBuilder.DropTable(
                name: "GitCommits");

            migrationBuilder.DropTable(
                name: "Uploads");

            migrationBuilder.DropTable(
                name: "Commits");

            migrationBuilder.DropIndex(
                name: "IX_Branches_CoveredCommitId",
                table: "Branches");
        }
    }
}
