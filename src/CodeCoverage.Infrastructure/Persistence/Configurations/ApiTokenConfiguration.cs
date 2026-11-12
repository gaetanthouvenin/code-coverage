// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ApiToken" /> to the <c>ApiTokens</c> table.</summary>
/// <remarks>
///   A global table (ADR-013): a token is found by the hash of its value before any organization is entered, and it is
///   the token that tells which organization the request may enter. Its <c>OrganizationId</c> is an ordinary column,
///   without the query filter of the organization tables, and null for a token of the instance.
/// </remarks>
internal sealed class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
  #region Fields

  /// <summary>The name of the unique index of the tokens by hash.</summary>
  public const string HashIndexName = "UX_ApiTokens_Hash";

  /// <summary>
  ///   The unique index that starts with the organization, so that it serves the foreign key to it, as on every table
  ///   that references an organization; the identifier makes it unique.
  /// </summary>
  public const string OrganizationIndexName = "UX_ApiTokens_OrganizationId_Id";

  #endregion

  #region Implementation of IEntityTypeConfiguration<ApiToken>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<ApiToken> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("ApiTokens");
    builder.HasKey(static t => t.Id);
    builder.Ignore(static t => t.IsInstanceToken);

    builder.Property(static t => t.UserId).IsRequired();
    builder.Property(static t => t.OrganizationId);
    builder.Property(static t => t.Name).IsRequired().HasMaxLength(ApiToken.NameMaxLength);
    builder.Property(static t => t.Hint).IsRequired().HasMaxLength(ApiToken.HintMaxLength);
    builder.Property(static t => t.Hash).IsRequired().HasMaxLength(ApiToken.HashLength).IsFixedLength();
    builder.Property(static t => t.CreatedAt).IsRequired();
    builder.Property(static t => t.ExpiresAt);
    builder.Property(static t => t.LastUsedAt);
    builder.Property(static t => t.RevokedAt);

    builder.HasIndex(static t => t.Hash).IsUnique().HasDatabaseName(HashIndexName);
    builder.HasIndex(static t => new { t.OrganizationId, t.Id }).IsUnique().HasDatabaseName(OrganizationIndexName);
    builder.HasOne<AppUser>().WithMany().HasForeignKey(static t => t.UserId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Organization>().WithMany().HasForeignKey(static t => t.OrganizationId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
