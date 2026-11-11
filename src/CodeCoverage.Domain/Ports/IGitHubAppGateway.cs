// ------------------------------------------------------------------------------------------------
// <copyright file="IGitHubAppGateway.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Domain.Ports;

/// <summary>What only GitHub offers an App, beyond the readings common to every forge.</summary>
public interface IGitHubAppGateway
{
  #region Methods

  /// <summary>Turns the code of a manifest into the App it created.</summary>
  /// <param name="code">The temporary code GitHub gave after the manifest was accepted.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The App, or <see langword="null" /> when GitHub does not know this code or it has expired.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="code" /> is <see langword="null" />.</exception>
  Task<GitHubAppCredentials?> ConvertManifestAsync(string code, CancellationToken cancellationToken);

  /// <summary>Lists every installation of the App, whichever account holds it.</summary>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The installations.</returns>
  Task<IReadOnlyList<GitHubInstallationInfo>> ListInstallationsAsync(CancellationToken cancellationToken);

  /// <summary>Reads one installation of the App.</summary>
  /// <param name="installationId">The identifier of the installation.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The installation, or <see langword="null" /> when GitHub no longer knows it.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="installationId" /> is not positive.</exception>
  Task<GitHubInstallationInfo?> GetInstallationAsync(long installationId, CancellationToken cancellationToken);

  /// <summary>Lists the repositories an installation serves.</summary>
  /// <param name="installationId">The identifier of the installation.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>The repositories.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="installationId" /> is not positive.</exception>
  Task<IReadOnlyList<ForgeRepositoryInfo>> ListInstallationRepositoriesAsync(long installationId, CancellationToken cancellationToken);

  /// <summary>Reads the membership of a user in the organization an installation serves.</summary>
  /// <param name="installationId">The identifier of the installation of that organization.</param>
  /// <param name="organizationLogin">The login of the organization.</param>
  /// <param name="userLogin">The login of the user.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The membership, or <see langword="null" /> when the user is not a member of the organization: an outside
  ///   collaborator of one of its repositories, for instance.
  /// </returns>
  /// <remarks>
  ///   Read with the token of the installation (permission <c>Organization: Members: Read</c>), not with the one
  ///   of the user: the role does not depend on what the user let the App see.
  /// </remarks>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="installationId" /> is zero or negative.</exception>
  /// <exception cref="ArgumentException">A login is <see langword="null" />, empty or white space.</exception>
  Task<GitHubOrganizationMembership?> GetOrganizationMembershipAsync(
    long installationId,
    string organizationLogin,
    string userLogin,
    CancellationToken cancellationToken);

  #endregion
}
