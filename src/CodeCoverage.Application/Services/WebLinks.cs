// ------------------------------------------------------------------------------------------------
// <copyright file="WebLinks.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Services;

/// <summary>The addresses of the pages of one repository in the Web console (ARCHITECTURE §10bis.3).</summary>
/// <remarks>
///   The one place that builds them for what is published on a forge: the statuses and the comment. Every
///   address is <see langword="null" /> when the instance has no public address, and the caller then writes
///   plain text instead of a link.
/// </remarks>
internal sealed class WebLinks
{
  #region Fields

  /// <summary>The address of the overview of the repository, without a trailing slash, or <see langword="null" />.</summary>
  private readonly string? _repository;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="WebLinks" /> class.</summary>
  /// <param name="webPublicUrl">The public address of the Web console, or <see langword="null" /> or empty before it is set.</param>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="repository">The repository.</param>
  /// <exception cref="ArgumentNullException"><paramref name="repository" /> is <see langword="null" />.</exception>
  public WebLinks(string? webPublicUrl, Forge forge, TrackedRepository repository)
  {
    ArgumentNullException.ThrowIfNull(repository);

    if (string.IsNullOrEmpty(webPublicUrl))
    {
      return;
    }

    // The levels between the account and the repository are joined by colons, as an address of the console spells them.
    var owner = repository.Namespace.Length == 0 ? repository.Owner : $"{repository.Owner}:{repository.Namespace.Replace('/', ':')}";

    _repository = $"{webPublicUrl.TrimEnd('/')}/{ForgeSegments.ToSegment(forge)}/{owner}/{repository.Name}";
  }

  #endregion

  #region Methods

  /// <summary>Gives the address of the page of a pull request.</summary>
  /// <param name="number">The number of the pull request.</param>
  /// <returns>The address, or <see langword="null" /> without a public address.</returns>
  public string? Pull(int number) =>
    _repository is null ? null : string.Create(CultureInfo.InvariantCulture, $"{_repository}/pull/{number}");

  /// <summary>Gives the address of the page of a commit.</summary>
  /// <param name="sha">The SHA of the commit.</param>
  /// <returns>The address, or <see langword="null" /> without a public address.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sha" /> is <see langword="null" />.</exception>
  public string? Commit(GitSha sha)
  {
    ArgumentNullException.ThrowIfNull(sha);

    return _repository is null ? null : $"{_repository}/commit/{sha.Value}";
  }

  /// <summary>Gives the address of the annotated source of a file at a commit.</summary>
  /// <param name="sha">The SHA of the commit.</param>
  /// <param name="path">The path of the file from the root of the repository.</param>
  /// <param name="pullNumber">The pull request whose patch the page highlights, or <see langword="null" />.</param>
  /// <returns>The address, or <see langword="null" /> without a public address.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sha" /> or <paramref name="path" /> is <see langword="null" />.</exception>
  public string? File(GitSha sha, string path, int? pullNumber)
  {
    ArgumentNullException.ThrowIfNull(sha);
    ArgumentNullException.ThrowIfNull(path);

    if (_repository is null)
    {
      return null;
    }

    var escaped = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
    var query = pullNumber is { } number ? string.Create(CultureInfo.InvariantCulture, $"?pull={number}") : string.Empty;

    return $"{_repository}/commit/{sha.Value}/blob/{escaped}{query}";
  }

  #endregion
}
