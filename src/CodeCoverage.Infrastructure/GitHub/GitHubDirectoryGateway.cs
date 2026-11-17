// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubDirectoryGateway.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Infrastructure.GitHub;

/// <summary>
///   <see cref="IForgeDirectoryGateway" /> of GitHub: a connection is an installation of the App, read and listed by
///   <see cref="IGitHubAppGateway" />.
/// </summary>
/// <param name="gitHub">The gateway of the App.</param>
/// <remarks>Registered as the <see cref="IForgeDirectoryGateway" /> keyed by <see cref="Forge.GitHub" />.</remarks>
internal sealed class GitHubDirectoryGateway(IGitHubAppGateway gitHub) : IForgeDirectoryGateway
{
  #region Methods

  /// <summary>Reads the installation a connection of GitHub names.</summary>
  /// <param name="connection">The connection.</param>
  /// <returns>The identifier of the installation.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="connection" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="connection" /> is not an installation of GitHub.</exception>
  private static long InstallationOf(ForgeConnectionRef connection)
  {
    ArgumentNullException.ThrowIfNull(connection);
    ArgumentException.ThrowIf(connection.Forge is not Forge.GitHub, $"The connection is one of {connection.Forge}, not of GitHub.", nameof(connection));

    return TrackedRepository.InstallationIdOf(connection.ConnectionId);
  }

  #endregion

  #region Implementation of IForgeDirectoryGateway

  /// <inheritdoc />
  public async Task<ForgeAccountInfo?> GetAccountAsync(ForgeConnectionRef connection, CancellationToken cancellationToken)
  {
    var installation = await gitHub.GetInstallationAsync(InstallationOf(connection), cancellationToken).ConfigureAwait(false);

    return installation is null ? null : new ForgeAccountInfo(installation.AccountId, installation.AccountLogin);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<ForgeRepositoryInfo>> ListRepositoriesAsync(ForgeConnectionRef connection, CancellationToken cancellationToken) =>
    await gitHub.ListInstallationRepositoriesAsync(InstallationOf(connection), cancellationToken).ConfigureAwait(false);

  #endregion
}
