// ------------------------------------------------------------------------------------------------
// <copyright file="PrincipalKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Commits;

/// <summary>The kind of credential that sent an upload.</summary>
public enum PrincipalKind
{
  /// <summary>An upload token of the repository.</summary>
  RepositoryToken,

  /// <summary>An upload token of the organization.</summary>
  OrganizationToken,

  /// <summary>An OIDC token issued by GitHub Actions.</summary>
  GitHubOidc,
}
