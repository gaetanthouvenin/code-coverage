// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationDirectory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and updates the <c>Organizations</c> table.</summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="clock">The clock that dates the changes.</param>
/// <remarks>
///   The logins are compared on <c>NormalizedLogin</c>, in lower case, so that the result does not depend
///   on the collation of the engine.
/// </remarks>
internal sealed class OrganizationDirectory(CodeCoverageDbContext context, IClock clock) : IOrganizationDirectory
{
  #region Fields

  /// <summary>How many times a save that lost a race against another save is attempted.</summary>
  private const int MaxUpsertAttempts = 3;

  #endregion

  #region Methods

  /// <summary>Tells whether a failed save lost a race and is worth attempting again.</summary>
  /// <param name="exception">The failure of the save.</param>
  /// <returns><see langword="true" /> for a concurrent write or a login or account taken meanwhile.</returns>
  private static bool IsLostRace(Exception exception) =>
    exception is DbUpdateConcurrencyException
    || UniqueIndexViolation.Is(exception, OrganizationConfiguration.AccountIndexName)
    || UniqueIndexViolation.Is(exception, OrganizationConfiguration.LoginIndexName);

  /// <summary>Applies an upsert once, in its own transaction.</summary>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="externalAccountId">The identifier the forge gives the account.</param>
  /// <param name="login">The login reported by the forge.</param>
  /// <param name="accountType">The kind of account reported by the forge.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The organization of the account.</returns>
  private async Task<Organization> UpsertOnceAsync(
    Forge forge,
    string externalAccountId,
    string login,
    OrganizationAccountType accountType,
    CancellationToken cancellationToken)
  {
    var normalizedId = Organization.NormalizeExternalAccountId(externalAccountId);
    var normalizedLogin = Organization.NormalizeLogin(login);
    var now = clock.UtcNow;
    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      var holder = await context.Organizations
                                .SingleOrDefaultAsync(
                                  o => o.Forge == forge
                                       && o.NormalizedLogin == normalizedLogin
                                       && o.ExternalAccountId != normalizedId,
                                  cancellationToken
                                )
                                .ConfigureAwait(false);

      if (holder is not null)
      {
        // Saved first: the unique index has to be free before this account takes the login.
        holder.Tombstone(now);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
      }

      var organization = await context.Organizations
                                      .SingleOrDefaultAsync(
                                        o => o.Forge == forge && o.ExternalAccountId == normalizedId,
                                        cancellationToken
                                      )
                                      .ConfigureAwait(false);

      if (organization is null)
      {
        organization = Organization.CreatePending(forge, externalAccountId, login, accountType, now);
        context.Organizations.Add(organization);
      }
      else
      {
        organization.Rename(login, now);
        organization.ChangeAccountType(accountType, now);
      }

      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
      await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

      return organization;
    }
  }

  /// <summary>Stops tracking the organizations: nothing is left for a later save of the scope.</summary>
  private void DetachOrganizations()
  {
    foreach (var entry in context.ChangeTracker.Entries<Organization>().ToList())
    {
      entry.State = EntityState.Detached;
    }
  }

  #endregion

  #region Implementation of IOrganizationDirectory

  /// <inheritdoc />
  public async Task<Organization?> FindByLoginAsync(Forge forge, string login, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(login);

    if (!Organization.IsWellFormedLogin(login))
    {
      return null;
    }

    var normalizedLogin = Organization.NormalizeLogin(login);

    return await context.Organizations
                        .AsNoTracking()
                        .SingleOrDefaultAsync(o => o.Forge == forge && o.NormalizedLogin == normalizedLogin, cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<Organization?> FindByAccountIdAsync(
    Forge forge,
    string externalAccountId,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(externalAccountId);

    var normalizedId = Organization.NormalizeExternalAccountId(externalAccountId);

    return await context.Organizations
                        .AsNoTracking()
                        .SingleOrDefaultAsync(o => o.Forge == forge && o.ExternalAccountId == normalizedId, cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<Organization>> ListActiveAsync(CancellationToken cancellationToken) =>
    await context.Organizations
                 .AsNoTracking()
                 .Where(static o => o.State == OrganizationState.Active)
                 .OrderBy(static o => o.Id)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<Organization> UpsertFromForgeAsync(
    Forge forge,
    string externalAccountId,
    string login,
    OrganizationAccountType accountType,
    CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(externalAccountId);
    ArgumentNullException.ThrowIfNull(login);

    if (!Organization.IsWellFormedLogin(login))
    {
      throw new ArgumentException($"The login '{login}' is not well formed.", nameof(login));
    }

    for (var attempt = 1; ; attempt++)
    {
      try
      {
        return await UpsertOnceAsync(forge, externalAccountId, login, accountType, cancellationToken).ConfigureAwait(false);
      }
      catch (Exception exception) when (attempt < MaxUpsertAttempts && IsLostRace(exception))
      {
        // Another save won: the next attempt reads what it wrote, in a new transaction.
      }
      finally
      {
        DetachOrganizations();
      }
    }
  }

  #endregion
}
