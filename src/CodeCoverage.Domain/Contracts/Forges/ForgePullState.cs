// ------------------------------------------------------------------------------------------------
// <copyright file="ForgePullState.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Forges;

/// <summary>The state of a pull request on its forge.</summary>
public enum ForgePullState
{
  /// <summary>The pull request is open.</summary>
  Open,

  /// <summary>The pull request was closed without being merged.</summary>
  Closed,

  /// <summary>The pull request was merged.</summary>
  Merged,
}
