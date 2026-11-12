// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations;

/// <summary>
///   The refusals of the organizations, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the message of each.
/// </summary>
internal static class OrganizationGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>
    ///   Throws when the user holds no role in an organization: it is unknown, not active, or the user does not belong
    ///   to it, and the answer does not tell which (ADR-013).
    /// </summary>
    /// <param name="role">The role of the user, or <see langword="null" />.</param>
    /// <exception cref="NotFoundException"><paramref name="role" /> is <see langword="null" />.</exception>
    public static void ThrowIfOrganizationNotFound([NotNull] OrganizationRole? role)
    {
      if (role is null)
      {
        throw new NotFoundException(OrganizationErrors.NotFound, "The organization does not exist or cannot be read.");
      }
    }
  }

  #endregion
}
