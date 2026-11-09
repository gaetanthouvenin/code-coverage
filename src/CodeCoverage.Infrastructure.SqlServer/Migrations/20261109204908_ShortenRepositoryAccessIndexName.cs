// ------------------------------------------------------------------------------------------------
// <copyright file="20261109204908_ShortenRepositoryAccessIndexName.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ShortenRepositoryAccessIndexName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "UX_UserRepositoryAccess_OrganizationId_UserId_RepositoryExternalId",
                table: "UserRepositoryAccess",
                newName: "UX_UserRepositoryAccess_OrganizationId_UserId_ExternalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "UX_UserRepositoryAccess_OrganizationId_UserId_ExternalId",
                table: "UserRepositoryAccess",
                newName: "UX_UserRepositoryAccess_OrganizationId_UserId_RepositoryExternalId");
        }
    }
}
