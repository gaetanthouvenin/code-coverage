// ------------------------------------------------------------------------------------------------
// <copyright file="20261110083925_MakeFlagTotalsCaseSensitive.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class MakeFlagTotalsCaseSensitive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server refuses to change the collation of a column of a key or an index: both are
            // dropped first and created again afterwards.
            DropKeys(migrationBuilder);

            migrationBuilder.AlterColumn<string>(
                name: "Flag",
                table: "CommitFlagTotals",
                type: "nvarchar(446)",
                maxLength: 446,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(446)",
                oldMaxLength: 446);

            CreateKeys(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropKeys(migrationBuilder);

            migrationBuilder.AlterColumn<string>(
                name: "Flag",
                table: "CommitFlagTotals",
                type: "nvarchar(446)",
                maxLength: 446,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(446)",
                oldMaxLength: 446,
                oldCollation: "Latin1_General_100_BIN2");

            CreateKeys(migrationBuilder);
        }

        private static void DropKeys(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommitFlagTotals_RepositoryId_Branch_Flag_CompletedAt",
                table: "CommitFlagTotals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CommitFlagTotals",
                table: "CommitFlagTotals");
        }

        private static void CreateKeys(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddPrimaryKey(
                name: "PK_CommitFlagTotals",
                table: "CommitFlagTotals",
                columns: new[] { "CommitId", "Flag" });

            migrationBuilder.CreateIndex(
                name: "IX_CommitFlagTotals_RepositoryId_Branch_Flag_CompletedAt",
                table: "CommitFlagTotals",
                columns: new[] { "RepositoryId", "Branch", "Flag", "CompletedAt" });
        }
    }
}
