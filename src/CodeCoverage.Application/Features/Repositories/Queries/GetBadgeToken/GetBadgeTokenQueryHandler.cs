// ------------------------------------------------------------------------------------------------
// <copyright file="GetBadgeTokenQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Repositories.Queries.GetBadgeToken;

/// <summary>Handler for the <see cref="GetBadgeTokenQuery" /> query.</summary>
/// <param name="locator">Finds the repository of the address.</param>
/// <param name="protector">Reads the stored value back.</param>
/// <remarks>A public repository serves its badge without a token, so none is given for it.</remarks>
internal sealed class GetBadgeTokenQueryHandler(CommitLocator locator, ISecretProtector protector) : IQueryHandler<GetBadgeTokenQuery, string?>
{
  #region Implementation of IQueryHandler<GetBadgeTokenQuery,string?>

  /// <inheritdoc />
  public async ValueTask<string?> Handle(GetBadgeTokenQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var repository = await locator.FindRepositoryAsync(query.Owner, query.Name, cancellationToken).ConfigureAwait(false);

    return repository.IsPrivate ? protector.Unprotect(repository.BadgeTokenProtected) : null;
  }

  #endregion
}
