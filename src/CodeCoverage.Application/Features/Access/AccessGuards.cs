// ------------------------------------------------------------------------------------------------
// <copyright file="AccessGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Features.Access;

/// <summary>
///   The refusals of the access, in the form of <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one
///   place holds the code and the message of each.
/// </summary>
internal static class AccessGuards
{
  #region Methods

  extension(UnauthorizedException)
  {
    /// <summary>Throws when the principal has no user row, or its row no longer exists.</summary>
    /// <param name="user">The user of the principal, or <see langword="null" />.</param>
    /// <exception cref="UnauthorizedException"><paramref name="user" /> is <see langword="null" />.</exception>
    public static void ThrowIfSignedOut([NotNull] AppUser? user)
    {
      if (user is null)
      {
        throw new UnauthorizedException(SecurityErrors.Unauthenticated, "No user is signed in.");
      }
    }
  }

  extension(ForbiddenException)
  {
    /// <summary>Throws when an account reaches no repository of an active organization and does not administer the instance.</summary>
    /// <param name="refused">Whether the sign-in is refused.</param>
    /// <exception cref="ForbiddenException"><paramref name="refused" /> is <see langword="true" />.</exception>
    public static void ThrowIfNoRepository([DoesNotReturnIf(true)] bool refused)
    {
      if (refused)
      {
        throw new ForbiddenException(AccessErrors.NoRepository, "The account reaches no repository of an active organization.");
      }
    }

    /// <summary>Throws when a user who may read a repository lacks the permission a message requires on it.</summary>
    /// <param name="missing">Whether the permission is missing.</param>
    /// <param name="messageName">The name of the message.</param>
    /// <param name="required">The permission the message requires.</param>
    /// <exception cref="ForbiddenException"><paramref name="missing" /> is <see langword="true" />.</exception>
    public static void ThrowIfRepositoryPermissionMissing([DoesNotReturnIf(true)] bool missing, string messageName, RepositoryPermission required)
    {
      if (missing)
      {
        throw new ForbiddenException(SecurityErrors.Forbidden, $"{messageName} requires the permission {required} on the repository.");
      }
    }

    /// <summary>Throws when a participant of an organization sends a message that requires an administrator of it.</summary>
    /// <param name="missing">Whether the role is missing.</param>
    /// <param name="messageName">The name of the message.</param>
    /// <exception cref="ForbiddenException"><paramref name="missing" /> is <see langword="true" />.</exception>
    public static void ThrowIfOrganizationRoleMissing([DoesNotReturnIf(true)] bool missing, string messageName)
    {
      if (missing)
      {
        throw new ForbiddenException(SecurityErrors.Forbidden, $"{messageName} requires an administrator of the organization.");
      }
    }
  }

  #endregion
}
