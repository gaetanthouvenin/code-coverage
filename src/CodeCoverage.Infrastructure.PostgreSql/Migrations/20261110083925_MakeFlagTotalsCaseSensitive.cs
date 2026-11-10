// ------------------------------------------------------------------------------------------------
// <copyright file="20261110083925_MakeFlagTotalsCaseSensitive.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class MakeFlagTotalsCaseSensitive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The default collation of PostgreSQL is deterministic: two flags are equal only when they are
            // the same text, although its ORDER BY follows the locale. Only the SQL Server model gets a collation.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
