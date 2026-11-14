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

  /// <summary>Creates a completed check run on a commit, in place of a commit status (M5.05).</summary>
  /// <param name="repository">The repository, on GitHub.</param>
  /// <param name="run">The check run, with its first annotations (50 at most).</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns>
  ///   The identifier of the check run, or <see langword="null" /> when GitHub refuses it: the installation lacks the
  ///   Checks permission (the App was not updated, or the organization did not accept it), the repository or the commit
  ///   is unknown. The caller publishes the status instead; it is never a failure of the job.
  /// </returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  Task<long?> CreateCheckRunAsync(ForgeRepositoryRef repository, GitHubCheckRun run, CancellationToken cancellationToken);

  /// <summary>Updates a check run: its conclusion and summary, and adds annotations (50 at most per call).</summary>
  /// <param name="repository">The repository, on GitHub.</param>
  /// <param name="checkRunId">The identifier of the check run.</param>
  /// <param name="run">The new state of the check run and the annotations of this call.</param>
  /// <param name="cancellationToken">A token to cancel the call.</param>
  /// <returns><see langword="false" /> when GitHub no longer has the check run or refuses the update (permission withdrawn).</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="checkRunId" /> is zero or negative.</exception>
  Task<bool> UpdateCheckRunAsync(ForgeRepositoryRef repository, long checkRunId, GitHubCheckRun run, CancellationToken cancellationToken);

  #endregion
}
