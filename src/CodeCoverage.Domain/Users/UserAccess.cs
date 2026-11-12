// ------------------------------------------------------------------------------------------------
// <copyright file="UserAccess.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Users;

/// <summary>The signed-in user and whether it administers the instance, as the navigation reads it.</summary>
/// <param name="User">The user.</param>
/// <param name="IsInstanceAdministrator">
///   Whether it administers the instance, from <c>Access:AdminUserIds</c> and the bootstrap list, read at each request.
/// </param>
/// <remarks>A read model: it only hides or shows entries, the server stays the authority.</remarks>
public sealed record UserAccess(AppUser User, bool IsInstanceAdministrator);
