// ------------------------------------------------------------------------------------------------
// <copyright file="20261120193542_RenameGitHubQuotasToForgeQuotas.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class RenameGitHubQuotasToForgeQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GitHubQuotas",
                table: "GitHubQuotas");

            // The rows are kept: on GitHub, the connection of a quota is its installation, or "app" for the App itself.
            migrationBuilder.RenameTable(
                name: "GitHubQuotas",
                newName: "ForgeQuotas");

            migrationBuilder.RenameColumn(
                name: "Installation",
                table: "ForgeQuotas",
                newName: "Connection");

            migrationBuilder.AlterColumn<string>(
                name: "Connection",
                table: "ForgeQuotas",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "Forge",
                table: "ForgeQuotas",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Every quota recorded so far came from GitHub.
            migrationBuilder.Sql(
                """
                UPDATE [ForgeQuotas] SET [Forge] = 'GitHub';
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ForgeQuotas",
                table: "ForgeQuotas",
                columns: new[] { "Forge", "Connection" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The former table only knows the quotas of GitHub, under keys of at most 32 characters.
            migrationBuilder.Sql(
                """
                DELETE FROM [ForgeQuotas] WHERE [Forge] <> 'GitHub' OR LEN([Connection]) > 32;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_ForgeQuotas",
                table: "ForgeQuotas");

            migrationBuilder.DropColumn(
                name: "Forge",
                table: "ForgeQuotas");

            migrationBuilder.AlterColumn<string>(
                name: "Connection",
                table: "ForgeQuotas",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.RenameColumn(
                name: "Connection",
                table: "ForgeQuotas",
                newName: "Installation");

            migrationBuilder.RenameTable(
                name: "ForgeQuotas",
                newName: "GitHubQuotas");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GitHubQuotas",
                table: "GitHubQuotas",
                column: "Installation");
        }
    }
}
