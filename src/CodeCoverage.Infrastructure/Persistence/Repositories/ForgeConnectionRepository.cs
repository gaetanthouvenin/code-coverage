// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Jobs;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>ForgeConnections</c> table, which is global (ADR-017).</summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="secretProtector">The protector the secrets of a connection must come from.</param>
/// <param name="jobQueue">The queue that takes the jobs of a new connection in the transaction of the change.</param>
/// <param name="wakeUpSignal">The signal that wakes the worker of this process once a job is committed.</param>
/// <remarks>
///   <para>
///     The table has no query filter, so nothing here calls <c>IgnoreQueryFilters</c>: a reading by organization filters
///     on its column, and a reading by identifier serves before a scope enters an organization.
///   </para>
///   <para>
///     The secrets are protected by <c>ISecretProtector</c> before they reach the connection, like the token of a
///     Codecov import. A write refuses a secret that the protector cannot read back, so that no plain value reaches the
///     database by mistake. An update checks only the secrets it writes: a connection whose stored secrets can no longer
///     be decrypted, after the key ring was lost, can still be suspended or record its failed check.
///   </para>
/// </remarks>
internal sealed class ForgeConnectionRepository(
  CodeCoverageDbContext context,
  ISecretProtector secretProtector,
  SqlJobQueue jobQueue,
  JobWakeUpSignal wakeUpSignal) : IForgeConnectionRepository
{
  #region Fields

  /// <summary>The name of the primary key, which reports a connection added twice.</summary>
  private const string PrimaryKeyName = "PK_ForgeConnections";

  /// <summary>The message of the refusal of an organization that is already saved.</summary>
  private const string SavedOrganizationMessage = "The organization of a new connection must not be saved yet.";

  /// <summary>The message of the refusal of a connection that does not belong to the new organization.</summary>
  private const string OtherOrganizationMessage = "The connection must name the new organization and its forge.";

  /// <summary>The message of the refusal of a job that does not belong to the new organization.</summary>
  private const string OtherLaneMessage = "A job of a new connection names its new organization or runs on the lane of the instance.";

  /// <summary>The message of the conflict of an organization or a connection that exists.</summary>
  private const string TakenMessage = "An organization of that account or login, or a connection of that identifier, exists.";

  /// <summary>The message of the refusal of a connection that this context did not read.</summary>
  private const string NotReadMessage = "A connection must be read in this scope before it can be updated.";

  /// <summary>The message of the refusal of a secret that is not protected.</summary>
  private const string NotProtectedMessage = "The secrets of a connection must be protected by ISecretProtector before they are stored.";

  #endregion

  #region Methods

  /// <summary>Tells whether a failed save met an organization or a connection that exists.</summary>
  /// <param name="exception">The failure of the save.</param>
  /// <returns><see langword="true" /> for an account, a login or a connection identifier taken.</returns>
  private static bool IsTaken(Exception exception) =>
    UniqueIndexViolation.Is(exception, OrganizationConfiguration.AccountIndexName)
    || UniqueIndexViolation.Is(exception, OrganizationConfiguration.LoginIndexName)
    || UniqueIndexViolation.Is(exception, PrimaryKeyName);

  /// <summary>Stops tracking the entities of a rolled back transaction: nothing is left for a later save of the scope.</summary>
  /// <param name="added">The entities added to the context.</param>
  /// <remarks>
  ///   An organization gives back the identifier its rolled back row received, so the instance of the caller reads as not
  ///   saved again.
  /// </remarks>
  private void Detach(List<object> added)
  {
    foreach (var entity in added)
    {
      var entry = context.Entry(entity);
      entry.State = EntityState.Detached;

      if (entity is Organization)
      {
        entry.Property(nameof(Organization.Id)).CurrentValue = 0L;
      }
    }
  }

  /// <summary>Saves an organization, its connection and their jobs in the open transaction of the context.</summary>
  /// <param name="organization">The organization, not saved yet.</param>
  /// <param name="connectionFactory">Builds the connection once the organization has its identifier.</param>
  /// <param name="jobsFactory">Builds the jobs once the organization has its identifier.</param>
  /// <param name="added">Collects the entities added to the context, detached on a failure.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>The connection and the identifiers of the queued jobs.</returns>
  /// <exception cref="InvalidOperationException">The connection or a job does not belong to the new organization.</exception>
  private async Task<(ForgeConnection Connection, List<long> JobIds)> SaveWithOrganizationAsync(
    Organization organization,
    Func<Organization, ForgeConnection> connectionFactory,
    Func<Organization, IReadOnlyList<JobRequest>> jobsFactory,
    List<object> added,
    CancellationToken cancellationToken)
  {
    context.Organizations.Add(organization);
    added.Add(organization);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    var connection = connectionFactory(organization);
    InvalidOperationException.ThrowIf(connection.OrganizationId != organization.Id || connection.Forge != organization.Forge, OtherOrganizationMessage);
    ThrowIfNotProtected(connection);
    context.ForgeConnections.Add(connection);
    added.Add(connection);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    var jobIds = new List<long>();

    foreach (var job in jobsFactory(organization))
    {
      InvalidOperationException.ThrowIf(!job.IsInstance && job.OrganizationId != organization.Id, OtherLaneMessage);
      jobIds.Add(await jobQueue.EnqueueInTransactionAsync(context, job, cancellationToken).ConfigureAwait(false));
    }

    return (connection, jobIds);
  }

  /// <summary>Refuses a connection whose secrets the protector cannot read back.</summary>
  /// <param name="connection">The connection.</param>
  /// <exception cref="InvalidOperationException">A secret is not a value protected by <see cref="ISecretProtector" />.</exception>
  private void ThrowIfNotProtected(ForgeConnection connection)
  {
    InvalidOperationException.ThrowIf(!IsProtected(connection.CredentialProtected), NotProtectedMessage);
    InvalidOperationException.ThrowIf(!IsProtected(connection.WebhookSecretProtected), NotProtectedMessage);
    InvalidOperationException.ThrowIf(connection.WebhookSecretPreviousProtected is { } previous && !IsProtected(previous), NotProtectedMessage);
  }

  /// <summary>Refuses an update that writes a secret the protector cannot read back.</summary>
  /// <param name="entry">The tracked entry of the connection, whose changes are detected.</param>
  /// <remarks>
  ///   A secret left unchanged, or moved to another column as by <see cref="ForgeConnection.RotateWebhookSecret" />, is
  ///   a value already stored, so it is not decrypted again.
  /// </remarks>
  /// <exception cref="InvalidOperationException">A written secret is not a value protected by <see cref="ISecretProtector" />.</exception>
  private void ThrowIfWrittenNotProtected(EntityEntry<ForgeConnection> entry)
  {
    var credential = entry.Property(static c => c.CredentialProtected);
    var webhookSecret = entry.Property(static c => c.WebhookSecretProtected);
    var previous = entry.Property(static c => c.WebhookSecretPreviousProtected);
    string?[] stored = [credential.OriginalValue, webhookSecret.OriginalValue, previous.OriginalValue];

    InvalidOperationException.ThrowIf(IsWrittenNotProtected(credential.IsModified, credential.CurrentValue, stored), NotProtectedMessage);
    InvalidOperationException.ThrowIf(IsWrittenNotProtected(webhookSecret.IsModified, webhookSecret.CurrentValue, stored), NotProtectedMessage);
    InvalidOperationException.ThrowIf(IsWrittenNotProtected(previous.IsModified, previous.CurrentValue, stored), NotProtectedMessage);
  }

  /// <summary>Tells whether an update writes a new secret that the protector cannot read back.</summary>
  /// <param name="isModified">Whether the column of the secret is modified.</param>
  /// <param name="value">The value the update writes.</param>
  /// <param name="stored">The secrets of the connection as they were read.</param>
  /// <returns><see langword="true" /> when the value is new and not protected; otherwise <see langword="false" />.</returns>
  private bool IsWrittenNotProtected(bool isModified, string? value, string?[] stored) =>
    isModified && value is not null && !stored.Contains(value, StringComparer.Ordinal) && !IsProtected(value);

  /// <summary>Tells whether a value was produced by the protector of the instance.</summary>
  /// <param name="value">The value.</param>
  /// <returns><see langword="true" /> when the protector reads it back; otherwise <see langword="false" />.</returns>
  private bool IsProtected(string value)
  {
    try
    {
      _ = secretProtector.Unprotect(value);

      return true;
    }
    catch (CryptographicException)
    {
      return false;
    }
  }

  #endregion

  #region Implementation of IForgeConnectionRepository

  /// <inheritdoc />
  public async Task<ForgeConnectionOwner?> FindOwnerAsync(string id, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(id);

    return await context.ForgeConnections
                        .AsNoTracking()
                        .Where(c => c.Id == id)
                        .Join(context.Organizations, static c => c.OrganizationId, static o => o.Id, static (c, o) => new ForgeConnectionOwner(o.Id, c.Forge, o.State, c.Status))
                        .SingleOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<ForgeConnection?> FindAsync(string id, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(id);

    return await context.ForgeConnections.SingleOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<ForgeConnection>> ListForOrganizationAsync(long organizationId, CancellationToken cancellationToken) =>
    await context.ForgeConnections
                 .AsNoTracking()
                 .Where(c => c.OrganizationId == organizationId)
                 .OrderBy(static c => c.Id)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<IReadOnlyList<ForgeConnection>> ListActiveAsync(CancellationToken cancellationToken) =>
    await context.ForgeConnections
                 .AsNoTracking()
                 .Where(static c => c.Status != ForgeConnectionStatus.Suspended)
                 .OrderBy(static c => c.Id)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task AddAsync(ForgeConnection connection, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(connection);

    ThrowIfNotProtected(connection);
    context.ForgeConnections.Add(connection);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, PrimaryKeyName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(connection).State = EntityState.Detached;

      throw ConflictException.ConcurrentChange("A connection of that identifier exists.", exception);
    }
  }

  /// <inheritdoc />
  public async Task<ForgeConnection> AddWithOrganizationAsync(
    Organization organization,
    Func<Organization, ForgeConnection> connection,
    Func<Organization, IReadOnlyList<JobRequest>> jobs,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(organization);
    ArgumentNullException.ThrowIfNull(connection);
    ArgumentNullException.ThrowIfNull(jobs);
    ArgumentException.ThrowIf(organization.Id != 0, SavedOrganizationMessage, nameof(organization));

    var added = new List<object>();
    (ForgeConnection Connection, List<long> JobIds) saved;
    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      try
      {
        saved = await SaveWithOrganizationAsync(organization, connection, jobs, added, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
      }
      catch (DbUpdateException exception) when (IsTaken(exception))
      {
        Detach(added);

        throw ConflictException.ConcurrentChange(TakenMessage, exception);
      }
      catch
      {
        Detach(added);

        throw;
      }
    }

    // Committed: a worker of this process may claim the jobs at once instead of at their next poll.
    foreach (var jobId in saved.JobIds)
    {
      wakeUpSignal.Notify(jobId);
    }

    return saved.Connection;
  }

  /// <inheritdoc />
  public async Task UpdateAsync(ForgeConnection connection, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(connection);

    var entry = context.Entry(connection);
    InvalidOperationException.ThrowIf(entry.State is EntityState.Detached, NotReadMessage);
    ThrowIfWrittenNotProtected(entry);

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
