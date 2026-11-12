// ------------------------------------------------------------------------------------------------
// <copyright file="AccessGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;
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
    /// <summary>Throws when a message that requires a signed-in user is sent by nobody.</summary>
    /// <param name="isAuthenticated">Whether a user is signed in.</param>
    /// <param name="messageName">The name of the message.</param>
    /// <exception cref="UnauthorizedException"><paramref name="isAuthenticated" /> is <see langword="false" />.</exception>
    public static void ThrowIfAnonymous([DoesNotReturnIf(false)] bool isAuthenticated, string messageName)
    {
      if (!isAuthenticated)
      {
        throw new UnauthorizedException(SecurityErrors.Unauthenticated, $"{messageName} requires a signed-in user.");
      }
    }

    /// <summary>Throws when the principal names no user.</summary>
    /// <param name="userId">The identifier of the user, or <see langword="null" />.</param>
    /// <exception cref="UnauthorizedException"><paramref name="userId" /> is <see langword="null" />.</exception>
    public static void ThrowIfNoUser([NotNull] long? userId)
    {
      if (userId is null)
      {
        throw new UnauthorizedException(SecurityErrors.Unauthenticated, "No user is signed in.");
      }
    }

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
    /// <summary>Throws when a message that only the system sends comes from anyone else.</summary>
    /// <param name="refused">Whether the sender is not the system.</param>
    /// <param name="messageName">The name of the message.</param>
    /// <exception cref="ForbiddenException"><paramref name="refused" /> is <see langword="true" />.</exception>
    public static void ThrowIfNotSystem([DoesNotReturnIf(true)] bool refused, string messageName)
    {
      if (refused)
      {
        throw new ForbiddenException(SecurityErrors.Forbidden, $"{messageName} can only be sent by the system.");
      }
    }

    /// <summary>
    ///   Throws when a message of the administration of the instance comes from a user who does not administer it, or
    ///   through a personal token of an organization.
    /// </summary>
    /// <param name="refused">Whether the sender may not administer the instance.</param>
    /// <param name="messageName">The name of the message.</param>
    /// <exception cref="ForbiddenException"><paramref name="refused" /> is <see langword="true" />.</exception>
    public static void ThrowIfNotInstanceAdministrator([DoesNotReturnIf(true)] bool refused, string messageName)
    {
      if (refused)
      {
        throw new ForbiddenException(SecurityErrors.Forbidden, $"{messageName} requires an administrator.");
      }
    }

    /// <summary>Throws when a personal token of the instance is asked by someone who may not create one.</summary>
    /// <param name="refused">Whether the creation is refused.</param>
    /// <exception cref="ForbiddenException"><paramref name="refused" /> is <see langword="true" />.</exception>
    public static void ThrowIfInstanceApiTokenRefused([DoesNotReturnIf(true)] bool refused)
    {
      if (refused)
      {
        throw new ForbiddenException(SecurityErrors.Forbidden, "Only an administrator of the instance, outside a token of an organization, creates a token of the instance.");
      }
    }

    /// <summary>Throws when a personal token of an organization is asked through a token of the instance.</summary>
    /// <param name="refused">Whether the creation is refused.</param>
    /// <exception cref="ForbiddenException"><paramref name="refused" /> is <see langword="true" />.</exception>
    public static void ThrowIfOrganizationApiTokenRefused([DoesNotReturnIf(true)] bool refused)
    {
      if (refused)
      {
        throw new ForbiddenException(SecurityErrors.Forbidden, "A token of the instance cannot create a token of an organization.");
      }
    }

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

  extension(NotFoundException)
  {
    /// <summary>Throws when the user owns no such personal token, or it is out of reach of the token of the request.</summary>
    /// <param name="token">The token, or <see langword="null" />.</param>
    /// <exception cref="NotFoundException"><paramref name="token" /> is <see langword="null" />.</exception>
    public static void ThrowIfApiTokenNotFound([NotNull] ApiToken? token)
    {
      if (token is null)
      {
        throw new NotFoundException(AccessErrors.ApiTokenNotFound, "The token does not exist.");
      }
    }
  }

  #endregion
}
