// ------------------------------------------------------------------------------------------------
// <copyright file="AccessGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
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
  }

  #endregion
}
