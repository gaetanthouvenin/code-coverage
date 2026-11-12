// ------------------------------------------------------------------------------------------------
// <copyright file="SignInUserCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Access.Commands.SignInUser;

/// <summary>Handler for the <see cref="SignInUserCommand" /> command.</summary>
/// <param name="userGateway">The gateway that calls GitHub in the name of a user.</param>
/// <param name="users">The repository of the users.</param>
/// <param name="protector">The protector of the stored secrets.</param>
/// <param name="updater">What writes the memberships of a user.</param>
/// <param name="jobQueue">The job queue.</param>
/// <param name="accessOptions">The settings of the access.</param>
/// <param name="bootstrapOptions">The bootstrap values of the access.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The account is read from GitHub with the access token, never taken from the caller. The user row is created on
///     the first sign-in, then refreshed: login, avatar, date of the sign-in and the protected tokens. A sign-in of the
///     same account that adds the row first, or a renewal of the tokens saved in the meantime, is met by reading the row
///     again, once.
///   </para>
///   <para>
///     The memberships follow the rule of <see cref="MembershipUpdater" />. An account linked to no active organization
///     is refused, unless <c>Access:AdminUserIds</c> or <c>CodeCoverage__Bootstrap__AdminUserIds</c> names it:
///     an administrator of the instance may enter without any organization, and gains no access to one by it. The stored
///     <see cref="AppUser.IsAdmin" /> flag is not written.
///   </para>
///   <para>
///     A signed-in user gets one <c>RefreshUserMemberships</c> job on the lane of the instance, which keeps its links up
///     to date from then on.
///   </para>
/// </remarks>
internal sealed class SignInUserCommandHandler(
  IGitHubUserGateway userGateway,
  IUserRepository users,
  ISecretProtector protector,
  MembershipUpdater updater,
  IJobQueue jobQueue,
  IOptionsMonitor<AccessOptions> accessOptions,
  IOptionsMonitor<BootstrapOptions> bootstrapOptions,
  IClock clock) : ICommandHandler<SignInUserCommand, long>
{
  #region Fields

  /// <summary>How many times the user row is saved when another save changed it first.</summary>
  private const int SaveAttempts = 2;

  #endregion

  #region Methods

  /// <summary>Creates or refreshes the user row of an account.</summary>
  /// <param name="account">The account, as GitHub reports it.</param>
  /// <param name="command">The command, which carries the tokens.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The user, saved.</returns>
  private async Task<AppUser> SaveAsync(GitHubUser account, SignInUserCommand command, CancellationToken cancellationToken)
  {
    for (var attempt = 1; ; attempt++)
    {
      try
      {
        return await TrySaveAsync(account, command, cancellationToken).ConfigureAwait(false);
      }
      catch (ConflictException) when (attempt < SaveAttempts)
      {
        // A renewal of the tokens, or another sign-in, saved the row first: it is read again.
      }
    }
  }

  /// <summary>Creates or refreshes the user row of an account, once.</summary>
  /// <param name="account">The account, as GitHub reports it.</param>
  /// <param name="command">The command, which carries the tokens.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The user, saved.</returns>
  /// <exception cref="ConflictException">Another save changed the user in the meantime.</exception>
  private async Task<AppUser> TrySaveAsync(GitHubUser account, SignInUserCommand command, CancellationToken cancellationToken)
  {
    if (await users.FindByExternalIdAsync(Forge.GitHub, account.ExternalId, cancellationToken).ConfigureAwait(false) is { } known)
    {
      Refresh(known, account, command);
      await users.UpdateAsync(known, cancellationToken).ConfigureAwait(false);

      return known;
    }

    var created = AppUser.Create(Forge.GitHub, account.ExternalId, account.Login, account.AvatarUrl);

    Refresh(created, account, command);

    if (await users.TryAddAsync(created, cancellationToken).ConfigureAwait(false))
    {
      return created;
    }

    // Another sign-in of the same account added the row first: it is the one refreshed.
    var existing = await users.FindByExternalIdAsync(Forge.GitHub, account.ExternalId, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException("The user of the account was neither added nor found.");

    Refresh(existing, account, command);
    await users.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

    return existing;
  }

  /// <summary>Records the sign-in and the tokens on a user.</summary>
  /// <param name="user">The user.</param>
  /// <param name="account">The account, as GitHub reports it.</param>
  /// <param name="command">The command, which carries the tokens.</param>
  private void Refresh(AppUser user, GitHubUser account, SignInUserCommand command)
  {
    var now = clock.UtcNow;

    user.RecordSignIn(account.Login, account.AvatarUrl, now);
    user.ReplaceTokens(
      protector,
      command.AccessToken,
      now + command.AccessTokenLifetime,
      command.RefreshToken,
      now + command.RefreshTokenLifetime
    );
  }

  /// <summary>Tells whether a user administers the instance.</summary>
  /// <param name="user">The user.</param>
  /// <returns><see langword="true" /> when one of the two lists of administrators names its account.</returns>
  private bool IsInstanceAdministrator(AppUser user) =>
    AdministratorList.Contains(user.Forge, user.ExternalId, accessOptions.CurrentValue.AdminUserIds, bootstrapOptions.CurrentValue.AdminUserIds);

  #endregion

  #region Implementation of ICommandHandler<SignInUserCommand,long>

  /// <inheritdoc />
  /// <exception cref="ForbiddenException">
  ///   The account reaches no repository of an active organization and does not administer the instance
  ///   (<see cref="AccessErrors.NoRepository" />).
  /// </exception>
  public async ValueTask<long> Handle(SignInUserCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);
    ArgumentException.ThrowIfNullOrWhiteSpace(command.AccessToken, nameof(command));

    var account = await userGateway.GetUserAsync(command.AccessToken, cancellationToken).ConfigureAwait(false);
    var user = await SaveAsync(account, command, cancellationToken).ConfigureAwait(false);

    var installations = await userGateway.ListInstallationsAsync(command.AccessToken, cancellationToken).ConfigureAwait(false);
    var roles = await updater.ReplaceAsync(user, installations, cancellationToken).ConfigureAwait(false);

    // The row and its tokens stay: a later refresh may let the user in, without asking GitHub to consent again.
    ForbiddenException.ThrowIfNoRepository(roles.Count == 0 && !IsInstanceAdministrator(user));

    await jobQueue.EnqueueAsync(RefreshUserMembershipsJobs.Create(user.Id), cancellationToken).ConfigureAwait(false);

    return user.Id;
  }

  #endregion
}
