// ------------------------------------------------------------------------------------------------
// <copyright file="ResolveSetupAdministratorsQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Queries.ResolveSetupAdministrators;

/// <summary>Handler for the <see cref="ResolveSetupAdministratorsQuery" /> query.</summary>
/// <param name="issuer">The issuer of the setup token.</param>
/// <param name="appGateway">The gateway of the GitHub App, which reads the public accounts without a token.</param>
/// <remarks>
///   Nothing is saved: the wizard shows « login → identifier » and the operator confirms, which ends the setup with
///   the identifiers alone. The accounts are read without a token, so the App does not have to exist yet.
/// </remarks>
internal sealed class ResolveSetupAdministratorsQueryHandler(ISetupTokenIssuer issuer, IGitHubAppGateway appGateway)
  : IQueryHandler<ResolveSetupAdministratorsQuery, IReadOnlyList<SetupAdministrator>>
{
  #region Methods

  /// <summary>Tells whether an entry of the list is a numeric identifier rather than a login.</summary>
  /// <param name="entry">The entry, trimmed.</param>
  /// <returns><see langword="true" /> when the entry holds only digits.</returns>
  internal static bool IsIdentifier(string entry) => entry.Length > 0 && entry.All(char.IsAsciiDigit);

  /// <summary>Splits the list typed by the operator into its entries.</summary>
  /// <param name="list">The list, or <see langword="null" />.</param>
  /// <returns>The trimmed entries, the empty ones included.</returns>
  internal static string[] EntriesOf(string? list) => list?.Split(',', StringSplitOptions.TrimEntries) ?? [];

  #endregion

  #region Implementation of IQueryHandler<ResolveSetupAdministratorsQuery,IReadOnlyList<SetupAdministrator>>

  /// <inheritdoc />
  /// <remarks>An administrator named twice, by its login and by its identifier, is kept once, where it first appears.</remarks>
  /// <exception cref="UnauthorizedException">The token is not valid.</exception>
  /// <exception cref="UnprocessableEntityException">GitHub knows no account of a login (<see cref="SettingErrors.UnknownGitHubLogin" />).</exception>
  public async ValueTask<IReadOnlyList<SetupAdministrator>> Handle(ResolveSetupAdministratorsQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    SetupRules.ThrowIfInvalidToken(issuer, query.SetupToken);

    var administrators = new List<SetupAdministrator>();

    foreach (var entry in EntriesOf(query.AdminUserIds))
    {
      SetupAdministrator administrator;

      if (IsIdentifier(entry))
      {
        administrator = new SetupAdministrator(entry, null);
      }
      else
      {
        var account = await appGateway.GetAccountByLoginAsync(entry, cancellationToken).ConfigureAwait(false);

        UnprocessableEntityException.ThrowIfUnknownAdministratorLogin(account, entry);

        administrator = new SetupAdministrator(account.ExternalId, account.Login);
      }

      if (!administrators.Exists(a => a.UserId == administrator.UserId))
      {
        administrators.Add(administrator);
      }
    }

    return administrators;
  }

  #endregion
}
