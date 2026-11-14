// ------------------------------------------------------------------------------------------------
// <copyright file="ICodecovApiClient.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Codecov;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Ports;

/// <summary>Reads the history of a repository from the API v2 of Codecov (M5.06).</summary>
/// <remarks>
///   It is called by the import job only, never in an HTTP request (rule 3). The API token of Codecov is given at each
///   call and never logged, nor written in an exception message.
/// </remarks>
public interface ICodecovApiClient
{
  #region Methods

  /// <summary>Reads one page of the commits of a repository, the most recent first.</summary>
  /// <param name="forge">The forge of the repository.</param>
  /// <param name="owner">The owner of the repository on its forge.</param>
  /// <param name="namespace">
  ///   The levels between the owner and the repository (GitLab subgroups, <c>sub/sub2</c>), empty when there is none.
  /// </param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="token">The API token of Codecov, in clear.</param>
  /// <param name="page">The page, from 1.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>
  ///   The page, or <see langword="null" /> when Codecov refuses for good: the token is not valid or not allowed, or
  ///   Codecov does not have the repository.
  /// </returns>
  /// <exception cref="ArgumentNullException">A text is <see langword="null" />.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="page" /> is zero or negative.</exception>
  /// <exception cref="ArgumentException"><paramref name="forge" /> has no counterpart on Codecov.</exception>
  /// <exception cref="HttpRequestException">Codecov failed or does not answer: the job runs again.</exception>
  Task<CodecovCommitPage?> ListCommitsAsync(
    Forge forge,
    string owner,
    string @namespace,
    string name,
    string token,
    int page,
    CancellationToken cancellationToken);

  #endregion
}
