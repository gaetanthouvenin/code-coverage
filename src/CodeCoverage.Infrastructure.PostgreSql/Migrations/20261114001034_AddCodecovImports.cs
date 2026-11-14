// ------------------------------------------------------------------------------------------------
// <copyright file="20261114001034_AddCodecovImports.cs" company="Gaëtan Thouvenin">
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
    public partial class AddCodecovImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodecovImports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RepositoryId = table.Column<long>(type: "bigint", nullable: false),
                    ProtectedToken = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextPage = table.Column<int>(type: "integer", nullable: false),
                    ImportedCommits = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodecovImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodecovImports_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodecovImports_CreatedAt",
                table: "CodecovImports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CodecovImports_RepositoryId",
                table: "CodecovImports",
                column: "RepositoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodecovImports");
        }
    }
}
