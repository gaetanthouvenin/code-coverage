// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.AzureDevOps.Models;

/// <summary>The answer of <c>GET _apis/git/repositories/{repositoryId}</c>.</summary>
/// <param name="Id">The identifier of the repository, which survives a renaming.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Project">The project that holds the repository.</param>
/// <param name="DefaultBranch">
///   The full name of the default branch, <c>refs/heads/main</c>, or <see langword="null" /> while the repository is
///   empty.
/// </param>
/// <param name="IsDisabled">A value indicating whether the repository is disabled.</param>
internal sealed record AzureDevOpsRepository(
  Guid Id,
  string Name,
  AzureDevOpsProject Project,
  string? DefaultBranch = null,
  bool IsDisabled = false);
