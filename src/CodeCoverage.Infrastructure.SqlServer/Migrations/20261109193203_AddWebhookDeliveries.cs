// ------------------------------------------------------------------------------------------------
// <copyright file="20261109193203_AddWebhookDeliveries.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebhookDeliveries",
                columns: table => new
                {
                    DeliveryId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: true),
                    Event = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    InstallationId = table.Column<long>(type: "bigint", nullable: true),
                    RepositoryGitHubId = table.Column<long>(type: "bigint", nullable: true),
                    Payload = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveries", x => x.DeliveryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_ReceivedAt",
                table: "WebhookDeliveries",
                column: "ReceivedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookDeliveries");
        }
    }
}
