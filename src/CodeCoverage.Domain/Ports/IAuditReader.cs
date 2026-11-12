// ------------------------------------------------------------------------------------------------
// <copyright file="IAuditReader.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Audit;

namespace CodeCoverage.Domain.Ports;

/// <summary>Reads the audit log of the instance: the entries recorded outside any organization only (ADR-013).</summary>
public interface IAuditReader
{
  #region Methods

  /// <summary>Lists one page of the audit log of the instance.</summary>
  /// <param name="category">The family kept, or <see langword="null" /> for every entry.</param>
  /// <param name="search">A part of the actor, the action or the target to look for, whatever its case; or <see langword="null" />.</param>
  /// <param name="page">The number of the page, from 1.</param>
  /// <param name="pageSize">The number of entries of a page.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The page, the most recent first.</returns>
  /// <remarks>
  ///   An entry of an organization is never read, and an API token event of a token bound to an organization neither,
  ///   even when it was recorded outside that organization before such events were recorded under it.
  /// </remarks>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="page" /> or <paramref name="pageSize" /> is not positive.</exception>
  Task<AuditEntryPage> ListInstanceAsync(AuditCategory? category, string? search, int page, int pageSize, CancellationToken cancellationToken);

  #endregion
}
