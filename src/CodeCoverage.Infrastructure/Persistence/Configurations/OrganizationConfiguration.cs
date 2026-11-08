// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Organization" /> to the <c>Organizations</c> table.</summary>
/// <remarks>
///   <para>
///     The table is global and holds no business data. Both unique indexes are unfiltered: a login given
///     up by an account is replaced with a tombstone, so that no provider-specific filter is needed.
///   </para>
///   <para>
///     <c>Version</c> is a plain integer used as a concurrency token: <c>rowversion</c> and <c>xmin</c>
///     each exist on one engine only.
///   </para>
/// </remarks>
internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
  #region Fields

  /// <summary>The unique index of the organizations by forge account.</summary>
  public const string AccountIndexName = "UX_Organizations_Forge_ExternalAccountId";

  /// <summary>The unique index of the organizations by forge and normalized login.</summary>
  public const string LoginIndexName = "UX_Organizations_Forge_NormalizedLogin";

  /// <summary>The name of the <c>Organizations</c> table.</summary>
  private const string TableName = "Organizations";

  #endregion

  #region Implementation of IEntityTypeConfiguration<Organization>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<Organization> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static o => o.Id);

    builder.Property(static o => o.Forge).IsRequired();
    builder.Property(static o => o.ExternalAccountId).IsRequired().HasMaxLength(Organization.ExternalAccountIdMaxLength);
    builder.Property(static o => o.Login).IsRequired().HasMaxLength(Organization.LoginMaxLength);
    builder.Property(static o => o.NormalizedLogin).IsRequired().HasMaxLength(Organization.LoginMaxLength);
    builder.Property(static o => o.AccountType).IsRequired();
    builder.Property(static o => o.State).IsRequired();
    builder.Property(static o => o.SuspensionReason);
    builder.Property(static o => o.AdmittedBy).HasMaxLength(AuditEntry.ActorMaxLength);
    builder.Property(static o => o.CreatedAt).IsRequired();
    builder.Property(static o => o.UpdatedAt).IsRequired();
    builder.Property(static o => o.Version).IsRequired().IsConcurrencyToken();

    builder.HasIndex(static o => new { o.Forge, o.ExternalAccountId }).IsUnique().HasDatabaseName(AccountIndexName);
    builder.HasIndex(static o => new { o.Forge, o.NormalizedLogin }).IsUnique().HasDatabaseName(LoginIndexName);
  }

  #endregion
}
