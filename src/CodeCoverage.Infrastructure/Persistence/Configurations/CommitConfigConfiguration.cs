// ------------------------------------------------------------------------------------------------
// <copyright file="CommitConfigConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CommitConfig" /> to the <c>CommitConfigs</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). The primary key does not
///   hold the organization: a repository has one, so the repository of a new row is checked by
///   <see cref="Repositories.CommitConfigRepository" />.
/// </remarks>
internal sealed class CommitConfigConfiguration : IEntityTypeConfiguration<CommitConfig>
{
  #region Fields

  /// <summary>The primary key of the cached configurations.</summary>
  public const string PrimaryKeyName = "PK_CommitConfigs";

  #endregion

  #region Implementation of IEntityTypeConfiguration<CommitConfig>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<CommitConfig> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("CommitConfigs");
    builder.HasKey(static c => new { c.RepositoryId, c.Sha }).HasName(PrimaryKeyName);
    builder.IsOrganizationOwned();

    builder.Property(static c => c.Sha).IsRequired().HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static c => c.Source).IsRequired();
    builder.Property(static c => c.Path).HasMaxLength(CommitConfig.PathMaxLength);
    builder.Property(static c => c.RawYaml);

    builder.Property(static c => c.Warnings)
           .IsRequired()
           .HasConversion(new JsonListConverter<ConfigWarning>(), JsonListConverter<ConfigWarning>.Comparer);

    builder.Property(static c => c.Errors)
           .IsRequired()
           .HasConversion(new JsonListConverter<string>(), JsonListConverter<string>.Comparer);

    builder.Property(static c => c.FetchedAt).IsRequired();

    builder.HasOne<TrackedRepository>().WithMany().HasForeignKey(static c => c.RepositoryId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
