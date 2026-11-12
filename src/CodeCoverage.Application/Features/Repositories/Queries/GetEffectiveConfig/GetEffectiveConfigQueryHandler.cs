// ------------------------------------------------------------------------------------------------
// <copyright file="GetEffectiveConfigQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;

/// <summary>Handler for the <see cref="GetEffectiveConfigQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="branches">The branches of the organization.</param>
/// <param name="commits">The commits of the organization.</param>
/// <param name="configs">The configurations kept for the commits.</param>
/// <param name="resolver">Merges the layers of the configuration.</param>
internal sealed class GetEffectiveConfigQueryHandler(
  CommitLocator locator,
  IBranchRepository branches,
  ICommitRepository commits,
  ICommitConfigRepository configs,
  ConfigResolver resolver) : IQueryHandler<GetEffectiveConfigQuery, EffectiveConfig>
{
  #region Implementation of IQueryHandler<GetEffectiveConfigQuery,EffectiveConfig>

  /// <inheritdoc />
  public async ValueTask<EffectiveConfig> Handle(GetEffectiveConfigQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);
    var branch = await branches.FindAsync(repository.Id, repository.DefaultBranch, cancellationToken).ConfigureAwait(false);
    var head = branch?.CoveredCommitId is { } headId ? await commits.FindAsync(headId, cancellationToken).ConfigureAwait(false) : null;
    var kept = head is null ? null : await configs.FindAsync(repository.Id, head.Sha, cancellationToken).ConfigureAwait(false);

    return await resolver.ReadAsync(repository, kept, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
