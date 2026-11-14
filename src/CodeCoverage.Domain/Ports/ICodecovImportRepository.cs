// ------------------------------------------------------------------------------------------------
// <copyright file="ICodecovImportRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Codecov;

namespace CodeCoverage.Domain.Ports;

/// <summary>Reads and writes the imports of the history of Codecov, in the organization of the scope (ADR-013).</summary>
public interface ICodecovImportRepository
{
  #region Methods

  /// <summary>Saves a new import and queues its job, in one transaction.</summary>
  /// <param name="import">The import, not saved yet.</param>
  /// <param name="cancellationToken">A token to cancel the writing.</param>
  /// <returns>A task that completes when the import and its job are saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="import" /> is <see langword="null" />.</exception>
  Task StartAsync(CodecovImport import, CancellationToken cancellationToken);

  /// <summary>Finds an import, tracked so that its progress can be saved.</summary>
  /// <param name="id">The identifier of the import.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The import, or <see langword="null" /> when the organization of the scope has none of that identifier.</returns>
  Task<CodecovImport?> FindAsync(long id, CancellationToken cancellationToken);

  /// <summary>Saves the progress of an import read in this scope.</summary>
  /// <param name="import">The import.</param>
  /// <param name="cancellationToken">A token to cancel the writing.</param>
  /// <returns>A task that completes when the import is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="import" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The import was not read in this scope.</exception>
  Task UpdateAsync(CodecovImport import, CancellationToken cancellationToken);

  /// <summary>Deletes an import and its credential.</summary>
  /// <param name="id">The identifier of the import.</param>
  /// <param name="cancellationToken">A token to cancel the writing.</param>
  /// <returns><see langword="true" /> when a row was deleted.</returns>
  Task<bool> DeleteAsync(long id, CancellationToken cancellationToken);

  /// <summary>Deletes the imports asked before a time, with their credentials.</summary>
  /// <param name="before">The time before which an import is expired.</param>
  /// <param name="cancellationToken">A token to cancel the writing.</param>
  /// <returns>The number of imports deleted.</returns>
  Task<int> PurgeAsync(DateTimeOffset before, CancellationToken cancellationToken);

  #endregion
}
