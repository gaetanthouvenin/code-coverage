// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeQuotaConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps the <see cref="ForgeQuota" /> to the operational table <c>ForgeQuotas</c>, one row per forge and connection.</summary>
/// <remarks>The forge is stored as text, like every enumeration: a new forge needs no migration.</remarks>
internal sealed class ForgeQuotaConfiguration : IEntityTypeConfiguration<ForgeQuota>
{
  #region Fields

  /// <summary>The name of the table.</summary>
  public const string TableName = "ForgeQuotas";

  /// <summary>The name of the primary key.</summary>
  public const string PrimaryKeyName = "PK_ForgeQuotas";

  #endregion

  #region Implementation of IEntityTypeConfiguration<ForgeQuota>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<ForgeQuota> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static q => new { q.Forge, q.Connection }).HasName(PrimaryKeyName);
    builder.Property(static q => q.Forge).IsRequired().ValueGeneratedNever();
    builder.Property(static q => q.Connection).HasMaxLength(ForgeQuota.ConnectionMaxLength).ValueGeneratedNever();
    builder.Property(static q => q.Remaining).IsRequired();
    builder.Property(static q => q.Limit);
    builder.Property(static q => q.ResetAt);
    builder.Property(static q => q.ObservedAt).IsRequired();
  }

  #endregion
}
