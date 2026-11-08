// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeRepositoryInfo.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What a forge reports about a repository.</summary>
/// <param name="ExternalId">The identifier the forge gives the repository.</param>
/// <param name="Owner">The login of the account that holds the repository.</param>
/// <param name="Namespace">
///   The levels between the account and the repository: the project on Azure DevOps, the subgroups on GitLab,
///   written <c>sub/sub2</c>, and an empty text on the other forges.
/// </param>
/// <param name="Name">The name of the repository.</param>
/// <param name="DefaultBranch">The default branch of the repository.</param>
/// <param name="IsPrivate">A value indicating whether the repository is private.</param>
public sealed record ForgeRepositoryInfo(
  string ExternalId,
  string Owner,
  string Namespace,
  string Name,
  string DefaultBranch,
  bool IsPrivate);
