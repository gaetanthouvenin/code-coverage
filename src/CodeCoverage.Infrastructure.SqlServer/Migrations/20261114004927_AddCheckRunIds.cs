// ------------------------------------------------------------------------------------------------
// <copyright file="20261114004927_AddCheckRunIds.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckRunIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CheckRunId",
                table: "PostedStatuses",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckRunId",
                table: "PostedStatuses");
        }
    }
}
