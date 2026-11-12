// ------------------------------------------------------------------------------------------------
// <copyright file="AddressSegments.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Api.Mapping;

/// <summary>
///   Reads the segments of an address of the API: an unknown forge or a malformed SHA is a missing resource, refused
///   from this one place.
/// </summary>
internal static class AddressSegments
{
  #region Methods

  /// <summary>Reads the forge segment of the address of a repository: an unknown forge is an unknown repository.</summary>
  /// <param name="forge">The segment.</param>
  /// <returns>The forge.</returns>
  /// <exception cref="NotFoundException">The segment names no forge (<see cref="RepositoryErrors.NotFound" />).</exception>
  public static Forge ToRepositoryForge(string forge) =>
    ForgeSegments.TryParse(forge, out var parsed)
      ? parsed
      : throw new NotFoundException(RepositoryErrors.NotFound, "The repository does not exist or cannot be read.");

  /// <summary>Reads the forge segment of the address of an organization: an unknown forge is an unknown organization.</summary>
  /// <param name="forge">The segment.</param>
  /// <returns>The forge.</returns>
  /// <exception cref="NotFoundException">The segment names no forge (<see cref="OrganizationErrors.NotFound" />).</exception>
  public static Forge ToOrganizationForge(string forge) =>
    ForgeSegments.TryParse(forge, out var parsed)
      ? parsed
      : throw new NotFoundException(OrganizationErrors.NotFound, "The organization does not exist or cannot be read.");

  /// <summary>Reads the SHA segment of an address: a malformed or short SHA is an unknown commit.</summary>
  /// <param name="sha">The segment.</param>
  /// <returns>The SHA.</returns>
  /// <exception cref="NotFoundException">The segment is not a full SHA (<see cref="CommitErrors.NotFound" />).</exception>
  public static GitSha ToCommitSha(string? sha) =>
    GitSha.TryParse(sha, out var parsed)
      ? parsed
      : throw new NotFoundException(CommitErrors.NotFound, "The commit does not exist.");

  #endregion
}
