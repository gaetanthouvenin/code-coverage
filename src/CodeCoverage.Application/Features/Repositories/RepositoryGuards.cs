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

    /// <summary>Throws when something a repository needs cannot be reached, with the refusal of a missing one (ADR-013).</summary>
    /// <typeparam name="T">The type of what was looked for.</typeparam>
    /// <param name="found">What was looked for: the connection, the organization, the repository on its forge.</param>
    /// <exception cref="NotFoundException"><paramref name="found" /> is <see langword="null" /> (<see cref="RepositoryErrors.NotFound" />).</exception>
    public static void ThrowIfRepositoryUnreachable<T>([NotNull] T? found)
      where T : class
    {
      if (found is null)
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

  extension(UnprocessableEntityException)
  {
    /// <summary>Throws when the coverage of a repository is not processed: its installation left it, or an administrator disabled it.</summary>
    /// <param name="repository">The repository.</param>
    /// <exception cref="ArgumentNullException"><paramref name="repository" /> is <see langword="null" />.</exception>
    /// <exception cref="UnprocessableEntityException">The repository is not tracked (<see cref="RepositoryErrors.Inactive" />).</exception>
    public static void ThrowIfRepositoryNotTracked(TrackedRepository repository)
    {
      ArgumentNullException.ThrowIfNull(repository);

      if (!repository.IsTracked)
      {
        throw new UnprocessableEntityException(RepositoryErrors.Inactive, "The coverage of this repository is not tracked.");
      }
    }
  }
  #endregion
}
