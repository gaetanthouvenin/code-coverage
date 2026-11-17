// ------------------------------------------------------------------------------------------------
// <copyright file="IForgeLinks.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Ports;

/// <summary>Builds the addresses of the pages of a forge: a pull request, a comment of a pull request, a commit.</summary>
/// <remarks>
///   Neutral (ADR-014): each forge implements it under its <see cref="Organizations.Forge" />, which
///   <see cref="IForgeGatewayProvider.GetLinks" /> resolves. Nothing is read from the forge: the addresses are built
///   from the settings of the forge and the path of the repository, each segment escaped.
/// </remarks>
public interface IForgeLinks
{
  #region Methods

  /// <summary>Gets the address of a pull request on the forge.</summary>
  /// <param name="owner">The owner of the repository, as its path on the forge writes it.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <returns>The absolute address of the pull request.</returns>
  /// <exception cref="ArgumentException"><paramref name="owner" /> or <paramref name="name" /> is <see langword="null" />, empty or white space.</exception>
  Uri PullRequestUrl(string owner, string name, int number);

  /// <summary>Gets the address of a comment posted on a pull request.</summary>
  /// <param name="owner">The owner of the repository, as its path on the forge writes it.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <param name="commentId">The identifier the forge gave the comment.</param>
  /// <returns>The absolute address of the comment.</returns>
  /// <exception cref="ArgumentException"><paramref name="owner" /> or <paramref name="name" /> is <see langword="null" />, empty or white space.</exception>
  Uri CommentUrl(string owner, string name, int number, long commentId);

  /// <summary>Gets the address of a commit on the forge.</summary>
  /// <param name="owner">The owner of the repository, as its path on the forge writes it.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="sha">The commit.</param>
  /// <returns>The absolute address of the commit.</returns>
  /// <exception cref="ArgumentException"><paramref name="owner" /> or <paramref name="name" /> is <see langword="null" />, empty or white space.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="sha" /> is <see langword="null" />.</exception>
  Uri CommitUrl(string owner, string name, GitSha sha);

  #endregion
}
