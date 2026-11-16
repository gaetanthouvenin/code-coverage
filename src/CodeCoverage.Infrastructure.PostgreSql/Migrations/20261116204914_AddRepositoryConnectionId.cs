// ------------------------------------------------------------------------------------------------
// <copyright file="20261116204914_AddRepositoryConnectionId.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryConnectionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "InstallationId",
                table: "Repositories",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "ConnectionId",
                table: "Repositories",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Every repository tracked so far is a GitHub repository: its connection is its installation, in text
            // (ADR-017), so its ForgeRepositoryRef does not change.
            migrationBuilder.Sql(
                """
                UPDATE "Repositories" SET "ConnectionId" = CAST("InstallationId" AS character varying(64))
                WHERE "InstallationId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "Repositories");

            migrationBuilder.AlterColumn<long>(
                name: "InstallationId",
                table: "Repositories",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
