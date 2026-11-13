// ------------------------------------------------------------------------------------------------
// <copyright file="20261113162312_AddComparisonBaseDistance.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddComparisonBaseDistance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BehindBy",
                table: "Comparisons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Hops",
                table: "Comparisons",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BehindBy",
                table: "Comparisons");

            migrationBuilder.DropColumn(
                name: "Hops",
                table: "Comparisons");
        }
    }
}
