// ------------------------------------------------------------------------------------------------
// <copyright file="20261109160104_AddRawUploads.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeCoverage.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRawUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UploadRawFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    DetectedFormat = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Sha256 = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    CompressedSize = table.Column<long>(type: "bigint", nullable: false),
                    UncompressedSize = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadRawFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UploadRawChunks",
                columns: table => new
                {
                    RawFileId = table.Column<long>(type: "bigint", nullable: false),
                    ChunkIndex = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadRawChunks", x => new { x.RawFileId, x.ChunkIndex });
                    table.ForeignKey(
                        name: "FK_UploadRawChunks_UploadRawFiles_RawFileId",
                        column: x => x.RawFileId,
                        principalTable: "UploadRawFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UploadRawFiles_ExpiresAt",
                table: "UploadRawFiles",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_UploadRawFiles_UploadPublicId",
                table: "UploadRawFiles",
                column: "UploadPublicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UploadRawChunks");

            migrationBuilder.DropTable(
                name: "UploadRawFiles");
        }
    }
}
