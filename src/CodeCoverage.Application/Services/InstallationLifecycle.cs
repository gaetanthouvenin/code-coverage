// ------------------------------------------------------------------------------------------------
// <copyright file="InstallationLifecycle.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Services;

/// <summary>What an installation of the GitHub App does to the organization of its account.</summary>
/// <param name="directory">The directory of the organizations.</param>
/// <param name="installations">The repository of the installations.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     Shared by the deliveries of the <c>installation</c> event and by the reconciliation of the installations,
///     which repairs the deliveries that were lost. It writes only global tables, so it runs inside an
///     organization as well as on the lane of the instance.
///   </para>
///   <para>
///     Nothing here admits an organization: an unknown account gets a pending one, and a suspension is lifted
///     only when the removal of the application caused it (<see cref="Organization.Reactivate" />).
///   </para>
/// </remarks>
internal sealed class InstallationLifecycle(
  IOrganizationDirectory directory,
  IInstallationRepository installations,
  IClock clock)
{
  #region Methods

  /// <summary>Records an installation under the organization of its account, which is created pending when it is unknown.</summary>
  /// <param name="installation">The installation, as GitHub describes it.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>
  ///   The organization of the account, with the login and the kind GitHub reports, or
  ///   <see langword="null" /> for an account whose login cannot be the one of an organization.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="installation" /> is <see langword="null" />.</exception>
  public async Task<Organization?> AttachAsync(GitHubInstallationInfo installation, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(installation);

    // An enterprise has no login, and a login this instance cannot address resolves to nothing.
    if (!Organization.IsWellFormedLogin(installation.AccountLogin))
    {
      return null;
    }

    // Read before the upsert, which may suspend it; kept as a value, since a directory may give the same instance.
    var wasActive = await directory.FindByAccountIdAsync(Forge.GitHub, installation.AccountId, cancellationToken).ConfigureAwait(false)
                      is { State: OrganizationState.Active };
    var organization = await directory.UpsertFromForgeAsync(
                           Forge.GitHub,
                           installation.AccountId,
                           installation.AccountLogin,
                           installation.AccountType,
                           cancellationToken
                         )
                         .ConfigureAwait(false);

    if (await installations.FindOwnerAsync(installation.Id, cancellationToken).ConfigureAwait(false) is not null)
    {
      return organization;
    }

    try
    {
      await installations.AddAsync(Installation.Create(installation.Id, organization.Id, clock.UtcNow), cancellationToken).ConfigureAwait(false);
    }
    catch (ConflictException exception) when (exception.ErrorCode == GeneralErrors.ConcurrentChange)
    {
      // Recorded in the meantime, by an upload or by another delivery: its row is the same.
    }

    return organization;
  }

  /// <summary>Suspends the organization of an account whose installation was deleted or suspended.</summary>
  /// <param name="accountId">The identifier GitHub gives the account.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The organization as saved, or <see langword="null" /> when the account has none.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="accountId" /> is <see langword="null" />.</exception>
  public async Task<Organization?> SuspendAsync(string accountId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(accountId);

    var now = clock.UtcNow;

    return await directory.ChangeAsync(
                            Forge.GitHub,
                            accountId,
                            organization => organization.Suspend(SuspensionReason.AppUninstalled, now),
                            cancellationToken
                          )
                          .ConfigureAwait(false);
  }

  /// <summary>Lifts the suspension of an organization whose application is installed again, when its removal caused it.</summary>
  /// <param name="organization">The organization, as last read.</param>
  /// <param name="cancellationToken">A token to cancel the work.</param>
  /// <returns>The organization as saved, or as given when it was not suspended for that reason.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="organization" /> is <see langword="null" />.</exception>
  public async Task<Organization> ReactivateAsync(Organization organization, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(organization);

    if (organization is not { State: OrganizationState.Suspended, SuspensionReason: SuspensionReason.AppUninstalled })
    {
      return organization;
    }

    var now = clock.UtcNow;

    // The row is read again by the directory: a suspension an administrator decided meanwhile stays.
    var saved = await directory.ChangeAsync(Forge.GitHub, organization.ExternalAccountId, row => row.Reactivate(now), cancellationToken)
                               .ConfigureAwait(false);

    return saved ?? organization;
  }

  #endregion
}
