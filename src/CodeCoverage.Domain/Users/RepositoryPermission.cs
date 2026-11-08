// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryPermission.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Users;

/// <summary>
///   The access level a user holds on a tracked repository, mirrored from its GitHub permission.
/// </summary>
/// <remarks>
///   The levels are ordered: each one includes the levels below it, so that
///   <c>granted &gt;= required</c> answers an access check.
/// </remarks>
public enum RepositoryPermission
{
  /// <summary>The user can read the coverage of the repository.</summary>
  Read = 0,

  /// <summary>The user can push to the repository; includes <see cref="Read" />.</summary>
  Write = 1,

  /// <summary>
  ///   The user administers the repository (tokens, notifications, reprocessing); includes
  ///   <see cref="Write" />.
  /// </summary>
  Admin = 2,
}
