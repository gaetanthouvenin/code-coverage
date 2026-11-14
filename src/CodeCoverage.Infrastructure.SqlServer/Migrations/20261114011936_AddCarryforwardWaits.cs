// ------------------------------------------------------------------------------------------------
// <copyright file="20261114011936_AddCarryforwardWaits.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCarryforwardWaits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarryforwardWaits",
                columns: table => new
                {
                    CommitId = table.Column<long>(type: "bigint", nullable: false),
                    AncestorId = table.Column<long>(type: "bigint", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarryforwardWaits", x => new { x.CommitId, x.AncestorId });
                    table.ForeignKey(
                        name: "FK_CarryforwardWaits_Commits_AncestorId",
                        column: x => x.AncestorId,
                        principalTable: "Commits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CarryforwardWaits_Commits_CommitId",
                        column: x => x.CommitId,
                        principalTable: "Commits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarryforwardWaits_AncestorId",
                table: "CarryforwardWaits",
                column: "AncestorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarryforwardWaits");
        }
    }
}
