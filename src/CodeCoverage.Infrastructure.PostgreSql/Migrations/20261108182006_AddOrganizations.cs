// ------------------------------------------------------------------------------------------------
// <copyright file="20261108182006_AddOrganizations.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CodeCoverage.Infrastructure.PostgreSql.Migrations
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
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Forge = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Login = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NormalizedLogin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SuspensionReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AdmittedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
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
