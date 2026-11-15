// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryLinks.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Web.Mapping;

/// <summary>
///   The one place where the pages write the address of a repository, of one of its commits and of a free comparison:
///   each segment escaped, relative to the base of the console.
/// </summary>
internal static class RepositoryLinks
{
  #region Methods

  /// <summary>Writes the address of a repository, <c>{forge}/{owner}/{name}</c>.</summary>
  /// <param name="forge">The forge segment of the address.</param>
  /// <param name="owner">The owner of the repository.</param>
  /// <param name="name">The name of the repository.</param>
  /// <returns>The relative address.</returns>
  /// <exception cref="ArgumentNullException">A parameter is <see langword="null" />.</exception>
  public static string HrefOf(string forge, string owner, string name)
  {
    ArgumentNullException.ThrowIfNull(forge);
    ArgumentNullException.ThrowIfNull(owner);
    ArgumentNullException.ThrowIfNull(name);

    return $"{Uri.EscapeDataString(forge)}/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}";
  }

  /// <summary>Writes the address of a commit of a repository, <c>{root}/commit/{sha}</c>.</summary>
  /// <param name="root">The address of the repository, from <see cref="HrefOf" />.</param>
  /// <param name="sha">The commit.</param>
  /// <returns>The relative address.</returns>
  /// <exception cref="ArgumentNullException">A parameter is <see langword="null" />.</exception>
  public static string CommitHrefOf(string root, GitSha sha)
  {
    ArgumentNullException.ThrowIfNull(root);
    ArgumentNullException.ThrowIfNull(sha);

    return $"{root}/commit/{Uri.EscapeDataString(sha.Value)}";
  }

  /// <summary>Writes the address of the free comparison of two commits, <c>{root}/compare/{base}...{head}</c>.</summary>
  /// <param name="root">The address of the repository, from <see cref="HrefOf" />.</param>
  /// <param name="baseSha">The base of the comparison.</param>
  /// <param name="headSha">The head of the comparison.</param>
  /// <returns>The relative address.</returns>
  /// <exception cref="ArgumentNullException">A parameter is <see langword="null" />.</exception>
  public static string CompareHrefOf(string root, GitSha baseSha, GitSha headSha)
  {
    ArgumentNullException.ThrowIfNull(root);
    ArgumentNullException.ThrowIfNull(baseSha);
    ArgumentNullException.ThrowIfNull(headSha);

    return $"{root}/compare/{Uri.EscapeDataString(baseSha.Value)}{ComparisonMapper.RangeSeparator}{Uri.EscapeDataString(headSha.Value)}";
  }

  #endregion
}
