// ------------------------------------------------------------------------------------------------
// <copyright file="20261109152808_AddOrganizationToAuditEntries.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationToAuditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OrganizationId",
                table: "AuditEntries",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OrganizationId_At",
                table: "AuditEntries",
                columns: new[] { "OrganizationId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_OrganizationId_At",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AuditEntries");
        }
    }
}
