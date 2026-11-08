// ------------------------------------------------------------------------------------------------
// <copyright file="SettingConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Infrastructure.Settings;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="StoredSetting" /> to the <c>Settings</c> table.</summary>
/// <remarks>
///   The key is the configuration key itself. <c>Value</c> is an unbounded text column: a private key
///   in PEM or the organization YAML do not fit a bounded one. The table and column names are also
///   read in plain SQL by <see cref="DatabaseSettingsConfigurationProvider" />.
/// </remarks>
internal sealed class SettingConfiguration : IEntityTypeConfiguration<StoredSetting>
{
  #region Implementation of IEntityTypeConfiguration<StoredSetting>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<StoredSetting> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(DatabaseSettingsConfigurationProvider.TableName);
    builder.HasKey(static s => s.Key);

    builder.Property(static s => s.Key).HasMaxLength(StoredSetting.KeyMaxLength).ValueGeneratedNever();
    builder.Property(static s => s.Value);
    builder.Property(static s => s.IsSecret).IsRequired();
    builder.Property(static s => s.UpdatedAt).IsRequired();
    builder.Property(static s => s.UpdatedBy).IsRequired().HasMaxLength(AuditEntry.ActorMaxLength);
  }

  #endregion
}
