// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>ForgeConnections</c> table, which is global (ADR-017).</summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="secretProtector">The protector the secrets of a connection must come from.</param>
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
internal sealed class ForgeConnectionRepository(CodeCoverageDbContext context, ISecretProtector secretProtector) : IForgeConnectionRepository
{
  #region Fields

  /// <summary>The name of the primary key, which reports a connection added twice.</summary>
  private const string PrimaryKeyName = "PK_ForgeConnections";

  /// <summary>The message of the refusal of a connection that this context did not read.</summary>
  private const string NotReadMessage = "A connection must be read in this scope before it can be updated.";

  /// <summary>The message of the refusal of a secret that is not protected.</summary>
  private const string NotProtectedMessage = "The secrets of a connection must be protected by ISecretProtector before they are stored.";

  #endregion

  #region Methods

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

      throw new ConflictException(GeneralErrors.ConcurrentChange, "A connection of that identifier exists.", exception);
    }
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
