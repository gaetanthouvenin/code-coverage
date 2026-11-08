// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationSettingConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Infrastructure.Persistence.Organizations;
using CodeCoverage.Infrastructure.Settings;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="StoredOrganizationSetting" /> to the <c>OrganizationSettings</c> table.</summary>
/// <remarks>
///   The first table owned by an organization. <c>Value</c> is an unbounded text column, since the
///   organization YAML does not fit a bounded one.
/// </remarks>
internal sealed class OrganizationSettingConfiguration : IEntityTypeConfiguration<StoredOrganizationSetting>
{
  #region Fields

  /// <summary>The unique index of the settings of an organization.</summary>
  public const string KeyIndexName = "UX_OrganizationSettings_OrganizationId_Key";

  /// <summary>The name of the <c>OrganizationSettings</c> table.</summary>
  private const string TableName = "OrganizationSettings";

  #endregion

  #region Implementation of IEntityTypeConfiguration<StoredOrganizationSetting>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<StoredOrganizationSetting> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static s => s.Id);
    builder.IsOrganizationOwned();

    builder.Property(static s => s.Key).IsRequired().HasMaxLength(StoredSetting.KeyMaxLength);
    builder.Property(static s => s.Value).IsRequired();
    builder.Property(static s => s.UpdatedAt).IsRequired();
    builder.Property(static s => s.UpdatedBy).IsRequired().HasMaxLength(AuditEntry.ActorMaxLength);

    builder.HasIndex(OrganizationOwnedExtensions.ColumnName, nameof(StoredOrganizationSetting.Key))
           .IsUnique()
           .HasDatabaseName(KeyIndexName);
    builder.HasOrganizationForeignKey();
  }

  #endregion
}
