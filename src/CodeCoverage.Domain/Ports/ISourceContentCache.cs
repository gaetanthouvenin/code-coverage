// ------------------------------------------------------------------------------------------------
// <copyright file="ISourceContentCache.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Ports;

/// <summary>
///   Reads the text of a source file from its forge, through a cache bounded in size and in time, for the annotated
///   source (ARCHITECTURE §10bis.3): the only forge call a request makes, after the access check, never stored.
/// </summary>
/// <remarks>
///   The port owns the forge call, the size bound and the cache verdict, so the use case only merges. A file the forge
///   does not have, one too large or binary, a forge with no adapter and a rate limit all answer « unavailable »; the
///   first three are kept in the cache, a rate limit never is. A refusal of the forge for another reason is raised as
///   it is.
/// </remarks>
public interface ISourceContentCache
{
  #region Methods

  /// <summary>Gets the text of a file at a commit, reading it from the forge on a miss.</summary>
  /// <param name="repository">The repository on its forge.</param>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="path">The path of the file, one the report of the commit holds.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The text, or <see langword="null" /> when the source cannot be shown.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="repository" />, <paramref name="sha" /> or <paramref name="path" /> is <see langword="null" />.
  /// </exception>
  Task<string?> GetSourceAsync(ForgeRepositoryRef repository, GitSha sha, string path, CancellationToken cancellationToken);

  #endregion
}
