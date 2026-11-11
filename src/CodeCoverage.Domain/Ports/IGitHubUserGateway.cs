// ------------------------------------------------------------------------------------------------
// <copyright file="IGitHubUserGateway.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Domain.Ports;

/// <summary>Calls GitHub in the name of a user, with the token GitHub issued for that user.</summary>
/// <remarks>
///   Particular to GitHub (ADR-014): it serves the sign-in and the access of a user to repositories. The token is
///   given by the caller, which gets it from <see cref="IGitHubUserTokenProvider" /> or from the sign-in itself;
///   it is never kept here.
/// </remarks>
public interface IGitHubUserGateway
{
  #region Methods

  /// <summary>Reads the account a user token belongs to.</summary>
  /// <param name="accessToken">The user token.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The account.</returns>
  /// <exception cref="ArgumentException"><paramref name="accessToken" /> is <see langword="null" />, empty or white space.</exception>
  Task<GitHubUser> GetUserAsync(string accessToken, CancellationToken cancellationToken);

  /// <summary>Lists the installations of the App that the user can reach, read to the last page.</summary>
  /// <param name="accessToken">The user token.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The installations, suspended ones included, whatever the state of their organization in this instance.</returns>
  /// <exception cref="ArgumentException"><paramref name="accessToken" /> is <see langword="null" />, empty or white space.</exception>
  Task<IReadOnlyList<GitHubInstallationInfo>> ListInstallationsAsync(string accessToken, CancellationToken cancellationToken);

  /// <summary>Lists the repositories of an installation that the user can reach, read to the last page.</summary>
  /// <param name="accessToken">The user token.</param>
  /// <param name="installationId">The identifier of the installation.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repositories; empty when the user cannot reach the installation.</returns>
  /// <exception cref="ArgumentException"><paramref name="accessToken" /> is <see langword="null" />, empty or white space.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="installationId" /> is zero or negative.</exception>
  Task<IReadOnlyList<GitHubUserRepository>> ListInstallationRepositoriesAsync(string accessToken, long installationId, CancellationToken cancellationToken);

  #endregion
}
