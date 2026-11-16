// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubInstallationAdapter.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Application.Services;

/// <summary>Gives the accounts a user reaches on a forge the shape of the GitHub installations that <see cref="MembershipUpdater" /> reads.</summary>
/// <remarks>
///   A bridge while <see cref="MembershipUpdater" /> still reads GitHub types: it disappears once the memberships are read
///   from neutral contracts (M7.17). The connection of an account is then the identifier of its installation.
/// </remarks>
internal static class GitHubInstallationAdapter
{
  #region Methods

  /// <summary>Converts the accounts of a user into GitHub installations.</summary>
  /// <param name="accounts">The accounts, as the user gateway of GitHub reports them.</param>
  /// <returns>One installation per account, in the same order.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="accounts" /> is <see langword="null" />.</exception>
  /// <exception cref="FormatException">The connection of an account is not the numeric identifier of an installation.</exception>
  public static IReadOnlyList<GitHubInstallationInfo> ToGitHubInstallations(this IReadOnlyList<ForgeUserAccount> accounts)
  {
    ArgumentNullException.ThrowIfNull(accounts);

    return
    [
      .. accounts.Select(static account => new GitHubInstallationInfo(
                           long.Parse(account.ConnectionId, NumberStyles.None, CultureInfo.InvariantCulture),
                           account.AccountId,
                           account.AccountLogin,
                           account.AccountType,
                           account.Suspended
                         )
      ),
    ];
  }

  #endregion
}
