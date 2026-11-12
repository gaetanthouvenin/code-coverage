// ------------------------------------------------------------------------------------------------
// <copyright file="IRepositoryAccessService.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Users;

namespace CodeCoverage.Domain.Ports;

/// <summary>Tells what a user may do on a repository of the organization of the scope, failing closed.</summary>
/// <remarks>
///   <para>
///     The answer comes from the cache of the permissions GitHub reported (<c>UserRepositoryAccess</c>), refreshed by the
///     <c>RefreshUserAccess</c> job. A row older than <c>Access:CacheMinutes</c> still answers, and queues its refresh; a
///     row older than <c>Access:HardExpiryMinutes</c> grants nothing, even when the refresh could not run.
///   </para>
///   <para>
///     Reading a repository requires the row only, not a membership of the organization. The administration of the
///     instance grants nothing here (ADR-013).
///   </para>
/// </remarks>
public interface IRepositoryAccessService
{
  #region Methods

  /// <summary>Tells whether a user holds a permission on a repository.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="repositoryExternalId">The identifier the forge gives the repository, whatever its case.</param>
  /// <param name="permission">The permission required; a higher one meets it.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns><see langword="true" /> when the user holds the permission, or a higher one.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="repositoryExternalId" /> is <see langword="null" />.</exception>
  Task<bool> CanAsync(long userId, string repositoryExternalId, RepositoryPermission permission, CancellationToken cancellationToken);

  /// <summary>Lists the repositories of the organization of the scope that a user may read now.</summary>
  /// <param name="userId">The identifier of the user.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The identifiers the forge gives these repositories, in lower case.</returns>
  /// <remarks>One read for a whole list; a stale cache queues its refresh, as <see cref="CanAsync" /> does.</remarks>
  Task<IReadOnlySet<string>> ListReadableAsync(long userId, CancellationToken cancellationToken);

  #endregion
}
