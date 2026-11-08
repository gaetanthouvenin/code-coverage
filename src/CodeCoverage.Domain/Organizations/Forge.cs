// ------------------------------------------------------------------------------------------------
// <copyright file="Forge.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Organizations;

/// <summary>The source code host of an organization, stored as text.</summary>
/// <remarks>
///   A login and an account identifier only mean something on their forge: the same value on two forges
///   designates two distinct organizations.
/// </remarks>
public enum Forge
{
  /// <summary>GitHub.</summary>
  GitHub,

  /// <summary>Azure DevOps.</summary>
  AzureDevOps,

  /// <summary>Bitbucket.</summary>
  Bitbucket,

  /// <summary>GitLab.com; self-managed instances are not supported.</summary>
  GitLab,
}
