// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>ApiTokens</c> table, a global table without query filter.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks>
///   <see cref="FindOfUserAsync" /> tracks what it returns, so that <see cref="UpdateAsync" /> saves it. The use of a
///   token is written by a guarded update, at most once a minute, never by a read then a save.
/// </remarks>
internal sealed class ApiTokenRepository(CodeCoverageDbContext context) : IApiTokenRepository
{
  #region Fields

  /// <summary>The message of the refusal of a token that this context did not read.</summary>
  private const string NotReadMessage = "A token must be read in this scope before it can be updated.";

  /// <summary>How often the use of a token is written.</summary>
  private static readonly TimeSpan MinimumUseInterval = TimeSpan.FromMinutes(1);

  #endregion

  #region Implementation of IApiTokenRepository

  /// <inheritdoc />
  public async Task AddAsync(ApiToken token, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(token);

    context.ApiTokens.Add(token);

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<ApiToken?> FindOfUserAsync(long userId, long id, CancellationToken cancellationToken) =>
    await context.ApiTokens
                 .SingleOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<ApiToken?> FindByHashAsync(byte[] hash, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(hash);

    return await context.ApiTokens
                        .AsNoTracking()
                        .SingleOrDefaultAsync(t => t.Hash == hash, cancellationToken)
                        .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<ApiToken>> ListOfUserAsync(long userId, CancellationToken cancellationToken) =>
    await context.ApiTokens
                 .AsNoTracking()
                 .Where(t => t.UserId == userId)
                 .OrderByDescending(static t => t.Id)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task RecordUseAsync(long id, DateTimeOffset at, CancellationToken cancellationToken)
  {
    var used = at.ToUniversalTime();
    var limit = used - MinimumUseInterval;

    await context.ApiTokens
                 .Where(t => t.Id == id && (t.LastUsedAt == null || t.LastUsedAt <= limit))
                 .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.LastUsedAt, used), cancellationToken)
                 .ConfigureAwait(false);
  }

  /// <inheritdoc />
  /// <exception cref="InvalidOperationException">The token was not read in this scope.</exception>
  public async Task UpdateAsync(ApiToken token, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(token);

    InvalidOperationException.ThrowIf(context.Entry(token).State is EntityState.Detached, NotReadMessage);

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
