// ------------------------------------------------------------------------------------------------
// <copyright file="20261109165818_AddPullRequests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPullRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PullRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepositoryId = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AuthorLogin = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    HeadSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    HeadBranch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BaseBranch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BaseShaGitHub = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ForkPointSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ComparedToSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CommentId = table.Column<long>(type: "bigint", nullable: true),
                    CommentBodyHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: true),
                    CommentedHeadSha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PullRequests_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_RepositoryId_ForkPointSha",
                table: "PullRequests",
                columns: new[] { "RepositoryId", "ForkPointSha" });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_RepositoryId_HeadSha",
                table: "PullRequests",
                columns: new[] { "RepositoryId", "HeadSha" });

            migrationBuilder.CreateIndex(
                name: "UX_PullRequests_RepositoryId_Number",
                table: "PullRequests",
                columns: new[] { "RepositoryId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PullRequests");
        }
    }
}
