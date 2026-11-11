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

  #endregion
}
