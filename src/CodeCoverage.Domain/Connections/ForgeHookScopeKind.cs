// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookScopeKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Connections;

/// <summary>What a hook created by a forge connection covers, stored as text.</summary>
public enum ForgeHookScopeKind
{
  /// <summary>The whole account of the connection, such as a Bitbucket workspace.</summary>
  Connection,

  /// <summary>A project of the account, such as an Azure DevOps project.</summary>
  Project,

  /// <summary>A single repository, such as a GitLab project.</summary>
  Repository,
}
