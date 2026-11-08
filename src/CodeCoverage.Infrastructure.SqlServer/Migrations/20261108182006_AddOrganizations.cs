// ------------------------------------------------------------------------------------------------
// <copyright file="20261108182006_AddOrganizations.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Forge = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Login = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NormalizedLogin = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SuspensionReason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AdmittedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Organizations_Forge_ExternalAccountId",
                table: "Organizations",
                columns: new[] { "Forge", "ExternalAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Organizations_Forge_NormalizedLogin",
                table: "Organizations",
                columns: new[] { "Forge", "NormalizedLogin" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}
