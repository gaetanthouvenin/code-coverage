// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>UploadTokens</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks><see cref="FindAsync" /> tracks what it returns: a row can only be written by the context that read it (ADR-013).</remarks>
internal sealed class UploadTokenRepository(CodeCoverageDbContext context) : IUploadTokenRepository
{
  #region Fields

  /// <summary>The message of the refusal of a token that this context did not read.</summary>
  private const string NotReadMessage = "A token must be read in this scope before it can be updated.";

  /// <summary>How often the use of a token is written.</summary>
  private static readonly TimeSpan MinimumUseInterval = TimeSpan.FromMinutes(1);

  #endregion

  #region Implementation of IUploadTokenRepository

  /// <inheritdoc />
  public async Task<bool> TryAddAsync(UploadToken token, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(token);

    context.UploadTokens.Add(token);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

      return true;
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, UploadTokenConfiguration.HashIndexName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(token).State = EntityState.Detached;

      return false;
    }
  }

  /// <inheritdoc />
  public async Task<UploadToken?> FindAsync(long id, CancellationToken cancellationToken) =>
    await context.UploadTokens
                 .SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<UploadToken?> FindByHashAsync(byte[] hash, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(hash);

    return await context.UploadTokens
                        .AsNoTracking()
                        .SingleOrDefaultAsync(t => t.Hash == hash, cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task RecordUseAsync(long id, DateTimeOffset at, CancellationToken cancellationToken)
  {
    var used = at.ToUniversalTime();
    var limit = used - MinimumUseInterval;

    await context.UploadTokens
                 .Where(t => t.Id == id && (t.LastUsedAt == null || t.LastUsedAt <= limit))
                 .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.LastUsedAt, used), cancellationToken)
                 .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<UploadToken>> ListOfOrganizationAsync(CancellationToken cancellationToken) =>
    await context.UploadTokens
                 .AsNoTracking()
                 .Where(static t => t.Scope == UploadTokenScope.Organization)
                 .OrderByDescending(static t => t.Id)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<IReadOnlyList<UploadToken>> ListOfRepositoryAsync(long repositoryId, CancellationToken cancellationToken) =>
    await context.UploadTokens
                 .AsNoTracking()
                 .Where(t => t.Scope == UploadTokenScope.Repository && t.RepositoryId == repositoryId)
                 .OrderByDescending(static t => t.Id)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  /// <exception cref="InvalidOperationException">The token was not read in this scope.</exception>
  public async Task UpdateAsync(UploadToken token, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(token);

    InvalidOperationException.ThrowIf(context.Entry(token).State is EntityState.Detached, NotReadMessage);

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
