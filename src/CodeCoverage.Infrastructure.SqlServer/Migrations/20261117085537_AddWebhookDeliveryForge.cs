// ------------------------------------------------------------------------------------------------
// <copyright file="20261117085537_AddWebhookDeliveryForge.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookDeliveryForge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_WebhookDeliveries",
                table: "WebhookDeliveries");

            // The identifiers keep their values: on GitHub, the connection is the installation and the repository its
            // numeric identifier, now in text (ADR-017).
            migrationBuilder.RenameColumn(
                name: "InstallationId",
                table: "WebhookDeliveries",
                newName: "ConnectionId");

            migrationBuilder.RenameColumn(
                name: "RepositoryGitHubId",
                table: "WebhookDeliveries",
                newName: "RepositoryExternalId");

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionId",
                table: "WebhookDeliveries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RepositoryExternalId",
                table: "WebhookDeliveries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Forge",
                table: "WebhookDeliveries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Every delivery received so far came from GitHub.
            migrationBuilder.Sql(
                """
                UPDATE [WebhookDeliveries] SET [Forge] = 'GitHub';
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_WebhookDeliveries",
                table: "WebhookDeliveries",
                columns: new[] { "Forge", "DeliveryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The former table only knows the deliveries of GitHub, identified by numbers.
            migrationBuilder.Sql(
                """
                DELETE FROM [WebhookDeliveries] WHERE [Forge] <> 'GitHub';
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_WebhookDeliveries",
                table: "WebhookDeliveries");

            migrationBuilder.DropColumn(
                name: "Forge",
                table: "WebhookDeliveries");

            migrationBuilder.AlterColumn<long>(
                name: "ConnectionId",
                table: "WebhookDeliveries",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "RepositoryExternalId",
                table: "WebhookDeliveries",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "ConnectionId",
                table: "WebhookDeliveries",
                newName: "InstallationId");

            migrationBuilder.RenameColumn(
                name: "RepositoryExternalId",
                table: "WebhookDeliveries",
                newName: "RepositoryGitHubId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WebhookDeliveries",
                table: "WebhookDeliveries",
                column: "DeliveryId");
        }
    }
}
