// ------------------------------------------------------------------------------------------------
// <copyright file="20261120101502_AddForgeHooks.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddForgeHooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ForgeConnections_OrganizationId_Id",
                table: "ForgeConnections");

            migrationBuilder.AddUniqueConstraint(
                name: "UX_ForgeConnections_OrganizationId_Id",
                table: "ForgeConnections",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateTable(
                name: "ForgeHooks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConnectionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ScopeKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ScopeExternalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    State = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeHooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForgeHooks_ForgeConnections_OrganizationId_ConnectionId",
                        columns: x => new { x.OrganizationId, x.ConnectionId },
                        principalTable: "ForgeConnections",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForgeHooks_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ForgeHooks_OrganizationId_ConnectionId_Scope",
                table: "ForgeHooks",
                columns: new[] { "OrganizationId", "ConnectionId", "ScopeKind", "ScopeExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForgeHooks");

            migrationBuilder.DropUniqueConstraint(
                name: "UX_ForgeConnections_OrganizationId_Id",
                table: "ForgeConnections");

            migrationBuilder.CreateIndex(
                name: "UX_ForgeConnections_OrganizationId_Id",
                table: "ForgeConnections",
                columns: new[] { "OrganizationId", "Id" },
                unique: true);
        }
    }
}
