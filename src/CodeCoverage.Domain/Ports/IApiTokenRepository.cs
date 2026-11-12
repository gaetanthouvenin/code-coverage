// ------------------------------------------------------------------------------------------------
// <copyright file="IApiTokenRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Domain.Ports;

/// <summary>The personal tokens of the API: a global table, read before any organization is entered.</summary>
public interface IApiTokenRepository
{
  #region Methods

  /// <summary>Adds a token, saved at once.</summary>
  /// <param name="token">The token.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the token is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="token" /> is <see langword="null" />.</exception>
  Task AddAsync(ApiToken token, CancellationToken cancellationToken);

  /// <summary>Finds a token of a user, which can then be updated.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="id">The identifier of the token.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The token, or <see langword="null" /> when the user owns no such token.</returns>
  Task<ApiToken?> FindOfUserAsync(long userId, long id, CancellationToken cancellationToken);

  /// <summary>Finds a token by the hash of its value, for reading only.</summary>
  /// <param name="hash">The SHA-256 of the value.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The token, or <see langword="null" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="hash" /> is <see langword="null" />.</exception>
  Task<ApiToken?> FindByHashAsync(byte[] hash, CancellationToken cancellationToken);

  /// <summary>Lists the tokens of a user, the most recent first.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The tokens, revoked ones included.</returns>
  Task<IReadOnlyList<ApiToken>> ListOfUserAsync(long userId, CancellationToken cancellationToken);

  /// <summary>Records the use of a token, saved at once, at most once a minute.</summary>
  /// <param name="id">The identifier of the token.</param>
  /// <param name="at">The current time.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the use is recorded, or skipped.</returns>
  Task RecordUseAsync(long id, DateTimeOffset at, CancellationToken cancellationToken);

  /// <summary>Saves the changes of a token read in this scope.</summary>
  /// <param name="token">The token.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the token is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="token" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The token was not read in this scope.</exception>
  Task UpdateAsync(ApiToken token, CancellationToken cancellationToken);

  #endregion
}
