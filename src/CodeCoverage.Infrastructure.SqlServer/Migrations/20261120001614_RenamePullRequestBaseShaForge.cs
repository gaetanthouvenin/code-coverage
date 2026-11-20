// ------------------------------------------------------------------------------------------------
// <copyright file="20261120001614_RenamePullRequestBaseShaForge.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class RenamePullRequestBaseShaForge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BaseShaGitHub",
                table: "PullRequests",
                newName: "BaseShaForge");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BaseShaForge",
                table: "PullRequests",
                newName: "BaseShaGitHub");
        }
    }
}
