// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Repositories;

/// <summary>
///   The refusals of the repositories, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the
///   message of each.
/// </summary>
internal static class RepositoryGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>Throws when a repository was not found in the organization of the scope.</summary>
    /// <param name="repository">The repository that was looked for.</param>
    /// <exception cref="NotFoundException"><paramref name="repository" /> is <see langword="null" />.</exception>
    public static void ThrowIfRepositoryNotFound([NotNull] TrackedRepository? repository)
    {
      if (repository is null)
      {
        throw new NotFoundException(RepositoryErrors.NotFound, "The repository does not exist or cannot be read.");
      }
    }

    /// <summary>Throws when the user may not read a repository, with the refusal of a missing one (ADR-013).</summary>
    /// <param name="readable">Whether the user may read the repository.</param>
    /// <exception cref="NotFoundException"><paramref name="readable" /> is <see langword="false" />.</exception>
    public static void ThrowIfRepositoryNotReadable([DoesNotReturnIf(false)] bool readable)
    {
      if (!readable)
      {
        throw new NotFoundException(RepositoryErrors.NotFound, "The repository does not exist or cannot be read.");
      }
    }
  }

  #endregion
}
