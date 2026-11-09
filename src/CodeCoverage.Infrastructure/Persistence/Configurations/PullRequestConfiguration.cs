// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="PullRequest" /> to the <c>PullRequests</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). The unique index does not
///   hold the organization: a repository has one, so the repository of a new row is checked by
///   <see cref="Repositories.PullRequestRepository" />.
/// </remarks>
internal sealed class PullRequestConfiguration : IEntityTypeConfiguration<PullRequest>
{
  #region Fields

  /// <summary>The unique index of the pull requests of a repository by number.</summary>
  public const string NumberIndexName = "UX_PullRequests_RepositoryId_Number";

  #endregion

  #region Implementation of IEntityTypeConfiguration<PullRequest>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<PullRequest> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("PullRequests");
    builder.HasKey(static p => p.Id);
    builder.IsOrganizationOwned();

    builder.Property(static p => p.RepositoryId).IsRequired();
    builder.Property(static p => p.Number).IsRequired();
    builder.Property(static p => p.State).IsRequired();
    builder.Property(static p => p.Title).IsRequired().HasMaxLength(PullRequest.TitleMaxLength);
    builder.Property(static p => p.AuthorLogin).HasMaxLength(PullRequest.AuthorLoginMaxLength);
    builder.Property(static p => p.HeadSha).IsRequired().HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static p => p.HeadBranch).IsRequired().HasMaxLength(PullRequest.BranchMaxLength);
    builder.Property(static p => p.BaseBranch).IsRequired().HasMaxLength(PullRequest.BranchMaxLength);
    builder.Property(static p => p.BaseShaGitHub).IsRequired().HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static p => p.ForkPointSha).HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static p => p.ComparedToSha).HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static p => p.CommentId);
    builder.Property(static p => p.CommentBodyHash).HasMaxLength(PullRequest.CommentBodyHashLength).IsFixedLength();
    builder.Property(static p => p.CommentedHeadSha).HasMaxLength(GitSha.Length).HasConversion<GitShaConverter>();
    builder.Property(static p => p.UpdatedAt).IsRequired();
    builder.Property(static p => p.Version).IsRequired().IsConcurrencyToken();

    builder.Ignore(static p => p.AcceptsComments);

    builder.HasIndex(static p => new { p.RepositoryId, p.Number }).IsUnique().HasDatabaseName(NumberIndexName);
    builder.HasIndex(static p => new { p.RepositoryId, p.HeadSha }).HasDatabaseName("IX_PullRequests_RepositoryId_HeadSha");
    builder.HasIndex(static p => new { p.RepositoryId, p.ForkPointSha }).HasDatabaseName("IX_PullRequests_RepositoryId_ForkPointSha");

    builder.HasOne<TrackedRepository>().WithMany().HasForeignKey(static p => p.RepositoryId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
