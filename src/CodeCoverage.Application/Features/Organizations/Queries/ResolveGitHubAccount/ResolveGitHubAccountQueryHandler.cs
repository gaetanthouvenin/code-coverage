// ------------------------------------------------------------------------------------------------
// <copyright file="ResolveGitHubAccountQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Organizations.Queries.ResolveGitHubAccount;

/// <summary>Handler for the <see cref="ResolveGitHubAccountQuery" /> query.</summary>
/// <param name="appGateway">The gateway of the GitHub App, which reads the public accounts.</param>
/// <remarks>
///   Nothing is saved: the page shows « login → identifier » and the administrator confirms the admission, which
///   stores the identifier GitHub gave.
/// </remarks>
internal sealed class ResolveGitHubAccountQueryHandler(IGitHubAppGateway appGateway) : IQueryHandler<ResolveGitHubAccountQuery, GitHubAccountInfo>
{
  #region Implementation of IQueryHandler<ResolveGitHubAccountQuery,GitHubAccountInfo>

  /// <inheritdoc />
  /// <exception cref="UnprocessableEntityException">GitHub knows no account of this login.</exception>
  public async ValueTask<GitHubAccountInfo> Handle(ResolveGitHubAccountQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var account = await appGateway.GetAccountByLoginAsync(query.Login, cancellationToken).ConfigureAwait(false);

    UnprocessableEntityException.ThrowIfUnknownGitHubLogin(account, query.Login);

    return account;
  }

  #endregion
}
