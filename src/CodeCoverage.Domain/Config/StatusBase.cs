// ------------------------------------------------------------------------------------------------
// <copyright file="StatusBase.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>What a status compares the head to (<c>base</c>).</summary>
public enum StatusBase
{
  /// <summary>The base of the pull request if there is one, otherwise the parent commit.</summary>
  Auto,

  /// <summary>The parent commit.</summary>
  Parent,

  /// <summary>The base of the pull request; without a pull request, no status is sent.</summary>
  Pr,
}
