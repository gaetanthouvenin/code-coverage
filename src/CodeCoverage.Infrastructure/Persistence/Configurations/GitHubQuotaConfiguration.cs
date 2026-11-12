// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubQuotaConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps the <see cref="GitHubQuota" /> to the operational table <c>GitHubQuotas</c>, one row per installation.</summary>
internal sealed class GitHubQuotaConfiguration : IEntityTypeConfiguration<GitHubQuota>
{
  #region Fields

  /// <summary>The name of the table.</summary>
  public const string TableName = "GitHubQuotas";

  /// <summary>The name of the primary key.</summary>
  public const string PrimaryKeyName = "PK_GitHubQuotas";

  #endregion

  #region Implementation of IEntityTypeConfiguration<GitHubQuota>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<GitHubQuota> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static q => q.Installation).HasName(PrimaryKeyName);
    builder.Property(static q => q.Installation).HasMaxLength(GitHubQuota.InstallationMaxLength).ValueGeneratedNever();
    builder.Property(static q => q.Remaining).IsRequired();
    builder.Property(static q => q.Limit);
    builder.Property(static q => q.ResetAt);
    builder.Property(static q => q.ObservedAt).IsRequired();
  }

  #endregion
}
