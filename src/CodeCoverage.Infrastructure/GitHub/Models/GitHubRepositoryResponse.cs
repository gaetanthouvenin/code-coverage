// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubRepositoryResponse.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.GitHub.Models;

/// <summary>The answer of <c>GET /repositories/{id}</c> and of <c>GET /repos/{owner}/{repo}</c>.</summary>
/// <param name="Id">The identifier of the repository.</param>
/// <param name="Owner">The account that holds the repository.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="DefaultBranch">The default branch of the repository.</param>
/// <param name="Private">A value indicating whether the repository is private.</param>
internal sealed record GitHubRepositoryResponse(
  long Id,
  GitHubRepositoryOwner Owner,
  string Name,
  string DefaultBranch,
  bool Private);
