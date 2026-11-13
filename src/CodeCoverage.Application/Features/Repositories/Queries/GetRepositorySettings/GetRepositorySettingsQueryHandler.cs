// ------------------------------------------------------------------------------------------------
// <copyright file="GetRepositorySettingsQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetRepositorySettings;

/// <summary>Handler for the <see cref="GetRepositorySettingsQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="branches">The branches of the organization.</param>
/// <param name="commits">The commits of the organization.</param>
/// <param name="configs">The configurations kept for the commits.</param>
/// <param name="resolver">Merges the layers of the configuration.</param>
/// <remarks>
///   The configuration is the one of the covered head of the default branch, as the overview reads it: the page shows
///   the YAML as it is written, with its source, its path and its SHA.
/// </remarks>
internal sealed class GetRepositorySettingsQueryHandler(
  CommitLocator locator,
  IBranchRepository branches,
  ICommitRepository commits,
  ICommitConfigRepository configs,
  ConfigResolver resolver) : IQueryHandler<GetRepositorySettingsQuery, RepositorySettings>
{
  #region Implementation of IQueryHandler<GetRepositorySettingsQuery,RepositorySettings>

  /// <inheritdoc />
  public async ValueTask<RepositorySettings> Handle(GetRepositorySettingsQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);
    var branch = await branches.FindAsync(repository.Id, repository.DefaultBranch, cancellationToken).ConfigureAwait(false);
    var head = branch?.CoveredCommitId is { } headId ? await commits.FindAsync(headId, cancellationToken).ConfigureAwait(false) : null;
    var kept = head is null ? null : await configs.FindAsync(repository.Id, head.Sha, cancellationToken).ConfigureAwait(false);
    var config = await resolver.ReadAsync(repository, kept, cancellationToken).ConfigureAwait(false);

    return new RepositorySettings(repository, config);
  }

  #endregion
}
