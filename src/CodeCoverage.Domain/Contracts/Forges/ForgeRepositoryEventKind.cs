// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeRepositoryEventKind.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>What happened to a repository, as a <see cref="ForgeRepositoryEvent" /> reports it.</summary>
public enum ForgeRepositoryEventKind
{
  /// <summary>The repository was created.</summary>
  Created,

  /// <summary>The repository was renamed, or changed in place, its visibility for instance: its new description applies.</summary>
  Renamed,

  /// <summary>The repository was deleted.</summary>
  Deleted,

  /// <summary>The repository was transferred to another account.</summary>
  Transferred,
}
