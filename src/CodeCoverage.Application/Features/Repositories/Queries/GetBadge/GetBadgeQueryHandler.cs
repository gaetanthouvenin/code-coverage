// ------------------------------------------------------------------------------------------------
// <copyright file="GetBadgeQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;

using Mediator;

using CodeCoverage.Domain.Badges;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetBadge;

/// <summary>Handler for the <see cref="GetBadgeQuery" /> query.</summary>
/// <param name="organization">The organization the gate entered, when it is active.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="branches">The branches of the organization.</param>
/// <param name="reports">The totals of the flags of the commits.</param>
/// <param name="protector">Unprotects the badge token of a private repository.</param>
/// <param name="hasher">Compares the tokens in constant time.</param>
/// <param name="parser">Reads the raw YAML of the repository for <c>coverage.range</c>.</param>
/// <remarks>
///   <para>
///     The value is the coverage of the flag (<c>''</c> without one) at the covered head of the branch
///     (<c>Branches.CoveredCommitId</c>, §11.1). An inactive repository still serves its last covered value, as Codecov
///     does; an organization that is not active or a repository that is unknown gives « unknown ».
///   </para>
///   <para>
///     A private repository requires its badge token, compared in constant time over the hashes; a public one ignores
///     the token. The range is <c>coverage.range</c> only when the raw YAML of the default branch names it.
///   </para>
/// </remarks>
internal sealed class GetBadgeQueryHandler(
  ICurrentOrganization organization,
  IRepositoryRepository repositories,
  IBranchRepository branches,
  ICommitReportRepository reports,
  ISecretProtector protector,
  ITokenHasher hasher,
  ICoverageConfigParser parser) : IQueryHandler<GetBadgeQuery, Badge>
{
  #region Methods

  /// <summary>Tells whether a request may read the badge of a repository.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="token">The token of the request, or <see langword="null" />.</param>
  /// <returns><see langword="true" /> for a public repository, or a private one whose badge token is given.</returns>
  private bool MayRead(TrackedRepository repository, string? token)
  {
    if (!repository.IsPrivate)
    {
      return true;
    }

    string stored;

    try
    {
      stored = protector.Unprotect(repository.BadgeTokenProtected);
    }
    catch (CryptographicException)
    {
      // A lost key ring reads no badge: « unknown », never a server error.
      return false;
    }

    // Both sides are hashed first: the comparison takes the same time whatever the length of the token.
    return hasher.FixedTimeEquals(hasher.Hash(token ?? string.Empty), hasher.Hash(stored)) && !string.IsNullOrEmpty(token);
  }

  /// <summary>Reads the range of the badge from the raw YAML of the default branch.</summary>
  /// <param name="yaml">The raw YAML, or <see langword="null" />.</param>
  /// <returns>The range named by the YAML, or <see langword="null" /> for <c>[70, 100]</c>.</returns>
  private CoverageRange? ReadRange(string? yaml)
  {
    if (string.IsNullOrWhiteSpace(yaml))
    {
      return null;
    }

    return parser.Parse(yaml).Root?.Find("coverage", "range") is { } node ? CoverageRange.Read(node) : null;
  }

  #endregion

  #region Implementation of IQueryHandler<GetBadgeQuery,Badge>

  /// <inheritdoc />
  public async ValueTask<Badge> Handle(GetBadgeQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    // The gate leaves the scope unset for an organization that is not active: nothing of it is read.
    if (!organization.IsKnown
        || !RepositorySlug.TryFromAddress(query.Owner, query.Name, out var slug)
        || await repositories.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false) is not { } repository
        || !MayRead(repository, query.Token))
    {
      return Badge.Unknown;
    }

    var branchName = string.IsNullOrEmpty(query.Branch) ? repository.DefaultBranch : query.Branch;

    if (await branches.FindAsync(repository.Id, branchName, cancellationToken).ConfigureAwait(false) is not { CoveredCommitId: { } commitId })
    {
      return Badge.Unknown;
    }

    var flag = query.Flag ?? CommitFlagTotals.ProjectFlag;
    var totals = await reports.ListFlagTotalsAsync(commitId, cancellationToken).ConfigureAwait(false);
    var coverage = totals.FirstOrDefault(t => string.Equals(t.Flag, flag, StringComparison.Ordinal))?.Coverage;

    return BadgeColorScale.Of(coverage, ReadRange(repository.DefaultBranchYaml));
  }

  #endregion
}
