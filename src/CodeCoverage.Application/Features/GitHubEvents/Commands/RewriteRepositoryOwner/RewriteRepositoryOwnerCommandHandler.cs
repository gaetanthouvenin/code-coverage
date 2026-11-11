// ------------------------------------------------------------------------------------------------
// <copyright file="RewriteRepositoryOwnerCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.GitHubEvents.Commands.RewriteRepositoryOwner;

/// <summary>Handler for the <see cref="RewriteRepositoryOwnerCommand" /> command.</summary>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="repositories">The repository of the tracked repositories.</param>
/// <remarks>
///   The login is read from the row of the organization when the job runs, never from what queued it: after two
///   renames in a row, the job that runs last writes the last login. The repositories are rewritten in batches,
///   each in its own statement, so that an organization with many of them holds no long transaction. A job that
///   is retried starts again from the first batch and changes nothing more.
/// </remarks>
internal sealed class RewriteRepositoryOwnerCommandHandler(IOrganizationDirectory directory, IRepositoryRepository repositories)
  : ICommandHandler<RewriteRepositoryOwnerCommand>
{
  #region Fields

  /// <summary>How many repositories one statement rewrites.</summary>
  internal const int BatchSize = 500;

  #endregion

  #region Implementation of ICommandHandler<RewriteRepositoryOwnerCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(RewriteRepositoryOwnerCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

    // A login given up to another account is a tombstone, not a login: the next synchronization restores it.
    if (organization is null || !Organization.IsWellFormedLogin(organization.Login))
    {
      return Unit.Value;
    }

    long? last = 0;

    while (last is { } afterId)
    {
      last = await repositories.RewriteOwnerAsync(organization.Login, afterId, BatchSize, cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
